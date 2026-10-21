using System;
using UnityEngine;
using UnityEngine.AI;

// =========================================================================
// EnemyAI - drives the Entity38 creature.
//
// Chases the player and hits them when close. Uses NavMesh pathfinding and a
// CharacterController. The two are wired together manually: the agent is
// told not to move the object itself, and we feed its velocity into
// controller.Move() every fixed step.
//
//   NavMeshAgent.updatePosition = false  -> agent computes velocity only
//   controller.Move(agent.velocity * dt) -> controller applies it to physics
//
// The existing Entity38 Animator is driven with these parameters:
//   Float  IsMoving    - 0 idle, 1 walking
//   Float  MoveSpeed   - current speed for blend-tree weight
//   Float  DistanceTo  - normalised distance to the target (0..1)
//
// Missing animator or parameters degrade gracefully - the enemy still moves
// and attacks, it just plays no animation.
// =========================================================================
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class EnemyAI : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // TARGETING
    // ---------------------------------------------------------------------
    [Header("Target")]
    [Tooltip("Auto-detected from the 'Player' tag if empty.")]
    [SerializeField] private Transform _target = null;
    [Tooltip("Distance at which the enemy stops and attacks.")]
    [SerializeField] private float _attackRange = 1.6f;
    [Tooltip("Beyond this distance the enemy loses interest and goes idle.")]
    [SerializeField] private float _disengageRange = 26f;
    [Tooltip("Layers the enemy can path through.")]
    [SerializeField] private LayerMask _walkableMask = ~0;

    // ---------------------------------------------------------------------
    // MOVEMENT
    // ---------------------------------------------------------------------
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2.4f;
    [SerializeField] private float _acceleration = 4f;
    [SerializeField] private float _agentRadius = 0.5f;
    [SerializeField] private float _agentHeight = 2.2f;

    // ---------------------------------------------------------------------
    // ATTACK
    // ---------------------------------------------------------------------
    [Header("Attack")]
    [SerializeField] private float _attackDamage = 12f;
    [SerializeField] private float _attackCooldown = 0.9f;
    [SerializeField] private LayerMask _playerLayerMask = ~0;

    [Header("Knockback")]
    [Tooltip("Push applied to the player on hit. X is the horizontal shove, Y the lift.")]
    [SerializeField] private Vector3 _knockbackForce = new Vector3(6f, 1.6f, 0f);

    // ---------------------------------------------------------------------
    // STATE
    // ---------------------------------------------------------------------
    public enum EnemyState { Idle, Chase, Attack }

    [SerializeField] private EnemyState _state = EnemyState.Chase;
    public EnemyState CurrentState => _state;

    private CharacterController _controller;
    private NavMeshAgent _agent;
    private Animator _animator;
    private Transform _targetT;

    private float _attackTimer;
    private float _speedSmooth;

    // Knockback is a decaying impulse (CharacterController.velocity is read-only).
    private float _knockbackDecay;
    private Vector3 _knockbackImpulse;

    // Animator parameter hashes, resolved once so a missing parameter is cheap.
    private int _hashIsMoving;
    private int _hashMoveSpeed;
    private int _hashDistanceTo;

    // ---------------------------------------------------------------------
    // EVENTS
    // ---------------------------------------------------------------------
    public event Action OnEnemyAttacked;
    public event Action OnEnemyDeath;

    // ---------------------------------------------------------------------
    // LIFECYCLE
    // ---------------------------------------------------------------------
    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();

        _hashIsMoving = Animator.StringToHash("IsMoving");
        _hashMoveSpeed = Animator.StringToHash("MoveSpeed");
        _hashDistanceTo = Animator.StringToHash("DistanceTo");

        ConfigureComponents();
        FindTarget();
        SetAnimation(0f, 1f);
    }

    private void ConfigureComponents()
    {
        _controller.radius = _agentRadius;
        _controller.height = _agentHeight;
        _controller.isTrigger = false;
        _controller.slopeLimit = 50f;
        _controller.stepOffset = 0.4f;

        _agent.speed = _moveSpeed;
        _agent.acceleration = _acceleration;
        _agent.angularSpeed = 400f;
        _agent.autoBraking = true;
        _agent.radius = _agentRadius * 0.9f;
        _agent.height = _agentHeight;
        _agent.stoppingDistance = 0.15f * _attackRange;
        _agent.walkableMask = _walkableMask;

        // The CharacterController owns physics. The agent only pathfinds.
        _agent.updatePosition = false;
        _agent.updateRotation = false;

        _agent.Warp(transform.position);
    }

    private void FindTarget()
    {
        if (_target != null) { _targetT = _target; return; }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var fps = FindAnyObjectByType<FirstPersonController>();
            if (fps != null) player = fps.gameObject;
        }

        if (player == null)
            Debug.LogWarning("[EnemyAI] No player found - the enemy will stay idle.");
        else
            _targetT = player.transform;
    }

    // ---------------------------------------------------------------------
    // UPDATE - decides intent, feeds the agent and the animator
    // ---------------------------------------------------------------------
    private void Update()
    {
        _attackTimer -= Time.deltaTime;

        if (_targetT == null)
        {
            FindTarget();
            if (_targetT == null) { SetAnimation(0f, 1f); return; }
        }

        float dist = HorizontalDistance(transform.position, _targetT.position);

        if (_state == EnemyState.Attack && dist > _attackRange * 1.5f)
            _state = EnemyState.Chase;

        if (dist <= _attackRange)
        {
            SetState(EnemyState.Attack);
            if (_agent != null) _agent.isStopped = true;
        }
        else if (dist > _disengageRange)
        {
            SetState(EnemyState.Idle);
            if (_agent != null) _agent.isStopped = true;
        }
        else
        {
            SetState(EnemyState.Chase);
            if (_agent != null) _agent.isStopped = false;
        }

        if (_agent != null && !_agent.isStopped)
            _agent.SetDestination(_targetT.position);

        if (_state == EnemyState.Attack)
            HandleAttack(dist);

        SmoothAnimation(dist);
    }

    // ---------------------------------------------------------------------
    // FIXED UPDATE - the actual motion
    // ---------------------------------------------------------------------
    private void FixedUpdate()
    {
        ApplyKnockbackToPlayer();

        if (_agent == null || _agent.isStopped || !_agent.isOnNavMesh) return;

        if (!_agent.hasPath) _agent.Warp(transform.position);

        // Agent velocity into the controller. updatePosition is false, so the
        // agent alone would never move us.
        _controller.Move(_agent.velocity * Time.fixedDeltaTime);

        // Turn smoothly toward the direction we are actually travelling.
        Vector3 dir = _agent.velocity;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, look, Time.fixedDeltaTime * 8f);
        }
    }

    // ---------------------------------------------------------------------
    // ATTACK
    // ---------------------------------------------------------------------
    private void HandleAttack(float dist)
    {
        if (_attackTimer > 0f || dist > _attackRange) return;
        if (!HasLineOfSightToPlayer()) return;

        _attackTimer = _attackCooldown;

        var combat = _targetT.GetComponentInParent<PlayerCombatSystem>();
        if (combat == null)
        {
            Debug.LogWarning("[EnemyAI] Player has no PlayerCombatSystem - hit will not register.");
            return;
        }

        if (!combat.IsDead) combat.TakeDamage(_attackDamage);
        ApplyKnockback();
        OnEnemyAttacked?.Invoke();
    }

    // Don't attack through walls.
    private bool HasLineOfSightToPlayer()
    {
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Vector3 to = _targetT.position + Vector3.up * 0.8f;
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.001f) return true;

        RaycastHit hit;
        if (Physics.Raycast(from, dir.normalized, out hit, dist, _playerLayerMask))
            return hit.transform == _targetT || hit.transform.IsChildOf(_targetT);
        return true;
    }

    // ---------------------------------------------------------------------
    // KNOCKBACK - CharacterController.velocity is read-only, so this is a
    // decaying impulse applied through Move().
    // ---------------------------------------------------------------------
    private void ApplyKnockback()
    {
        Vector3 dir = (_targetT.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        else dir.Normalize();

        _knockbackImpulse = dir * _knockbackForce.x + Vector3.up * _knockbackForce.y;
        _knockbackDecay = 0.15f;
    }

    private void ApplyKnockbackToPlayer()
    {
        if (_knockbackDecay <= 0f || _targetT == null) return;

        var playerCC = _targetT.GetComponentInParent<CharacterController>();
        if (playerCC == null) return;

        playerCC.Move(_knockbackImpulse * _knockbackDecay);
        _knockbackDecay = Mathf.Max(0f, _knockbackDecay - Time.fixedDeltaTime);
    }

    // ---------------------------------------------------------------------
    // ANIMATION
    // ---------------------------------------------------------------------
    private void SetAnimation(float isMoving, float distTo)
    {
        if (_animator == null) return;
        _animator.SetFloat(_hashIsMoving, isMoving);
        _animator.SetFloat(_hashDistanceTo, distTo);
    }

    private void SmoothAnimation(float dist)
    {
        if (_animator == null) return;

        float targetSpeed = (_state == EnemyState.Chase) ? 1f : 0f;
        _speedSmooth = Mathf.MoveTowards(_speedSmooth, targetSpeed, Time.deltaTime * 4f);
        _animator.SetFloat(_hashMoveSpeed, _speedSmooth);
        _animator.SetFloat(_hashDistanceTo, Mathf.Clamp01(dist / _disengageRange));
    }

    private void SetState(EnemyState next)
    {
        if (_state == next) return;
        _state = next;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        d.y = 0f;
        return d.magnitude;
    }

    // ---------------------------------------------------------------------
    // PUBLIC API
    // ---------------------------------------------------------------------
    public void SetTarget(Transform target)
    {
        _targetT = target;
        _state = EnemyState.Chase;
        if (_agent != null) _agent.isStopped = false;
    }

    /// <summary>Freeze or resume the enemy.</summary>
    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            SetState(EnemyState.Idle);
            if (_agent != null) _agent.isStopped = true;
            SetAnimation(0f, 1f);
        }
        else
        {
            SetState(EnemyState.Chase);
            if (_agent != null) _agent.isStopped = false;
        }
    }

    /// <summary>Revive a defeated enemy (called from a respawn handler).</summary>
    public void ResetEnemy()
    {
        SetState(EnemyState.Chase);
        _attackTimer = 0f;
        SetAnimation(0f, 1f);
        OnEnemyDeath?.Invoke();
    }

    public bool IsWithinAttackRange()
    {
        return _targetT != null &&
               HorizontalDistance(transform.position, _targetT.position) <= _attackRange;
    }

    public bool IsTargetDead()
    {
        if (_targetT == null) return true;
        var combat = _targetT.GetComponentInParent<PlayerCombatSystem>();
        return combat != null && combat.IsDead;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + Vector3.up * 1f;
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(center, _attackRange);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(center, _disengageRange);
    }
}
