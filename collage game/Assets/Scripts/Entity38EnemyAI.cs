using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(CharacterController))]
public class Entity38EnemyAI : MonoBehaviour
{
    public enum AIState
    {
        Lurking,           // Stationary menacing surveillance, slow breathing, tracking player
        Stalking,          // Slow unnerving creeping advance
        Chasing,           // Aggressive predatory sprint with twitching movements
        Attacking,         // Claw lunge and strike
        Stunned,           // Brief recovery after attack
        ReturningToCenter  // Naturally walking back to room center after losing target/player retreat
    }

    [Header("Current Status")]
    [SerializeField] private AIState currentState = AIState.Lurking;
    public AIState CurrentState => currentState;
    public Transform TargetPlayer => playerTransform;

    [Header("Testing & Loop Preview")]
    [Tooltip("Keep creature stationary in place facing player so animations can be observed in a continuous seamless loop")]
    public bool loopInPlace = false;
    public bool manualControl = false;
    [Tooltip("Show on-screen Entity 38 animation testing GUI (F10 to toggle)")]
    public bool showDebugGUI = false;

    [Header("Grounding & Height Alignment")]
    [Tooltip("Vertical lift to place feet on the floor (0 for upright model with pivot at floor level)")]
    public float verticalModelOffset = 0.0f;

    [Header("Detection Ranges")]
    [Tooltip("Maximum distance at which creature spots and gets agitated by a standing player with clear Line of Sight")]
    public float agitationRange = 11.5f;
    [Tooltip("Reduced detection range when player is crouching for stealth gameplay")]
    public float crouchAgitationRange = 7.5f;
    [Tooltip("Distance at which creature begins stalking the player")]
    public float detectionRange = 13.0f;
    [Tooltip("Distance at which creature enters full sprint chase")]
    public float chaseRange = 15.0f;
    [Tooltip("Distance required to initiate a melee strike")]
    public float attackRange = 3.2f;

    [Header("Room Confinement & Leash (Area 5 Creature Room)")]
    [Tooltip("Center of Area 5 room where creature naturally returns when de-aggravated")]
    public Vector3 roomCenterPosition = new Vector3(88.0f, 0.05f, 11.0f);
    [Tooltip("West limit (X): denies exit towards the connecting hallway and fuse cell room")]
    public float minRoomX = 73.2f;
    [Tooltip("East limit (X)")]
    public float maxRoomX = 103.5f;
    [Tooltip("South limit (Z)")]
    public float minRoomZ = 0.5f;
    [Tooltip("North limit (Z): denies exit backwards into the passage towards Room 4")]
    public float maxSpawnDirectionZ = 22.8f;
    [Tooltip("North outer wall limit (Z)")]
    public float maxRoomZ = 24.5f;
    [Tooltip("Minimum X coordinate of the north portal doorway leading towards Room 4")]
    public float spawnBoundaryMinX = 97.5f;
    [Tooltip("Natural walking return speed when returning to the room center")]
    public float returnWalkSpeed = 2.4f;
    [Tooltip("Distance threshold to consider creature returned to room center")]
    public float centerArrivalRadius = 1.8f;

    [Header("Speeds")]
    public float stalkSpeed = 2.2f;
    public float chaseSpeed = 4.4f;
    public float rotationSpeed = 10.0f;

    [Header("Animation Speeds")]
    public float lurkAnimSpeed = 0.5f;
    public float stalkAnimSpeed = 1.0f;
    public float chaseAnimSpeed = 1.6f;

    [Header("Combat & Lunge")]
    [Tooltip("Whether the creature can attack the player. Enabled for fatal capture kill sequence.")]
    public bool canAttack = true;
    public float attackCooldown = 1.4f;
    public float lungeForce = 6.5f;
    public float attackDamage = 35.0f;
    public float knockbackStrength = 0.0f;

    [Header("Procedural Horror Look-At")]
    public bool enableHeadTracking = false;
    public float lookAtWeight = 0.65f;
    public float twitchIntensity = 3.5f;

    [Header("Monster Horror Audio")]
    [Tooltip("Notice & Attack Screech (53439420-flying-monster-screech-02-461220)")]
    [SerializeField] private AudioClip noticeAttackScreechClip;
    [Tooltip("Proximity lurking growl (alesiadavina-horror-sound-lurking-horror-monster-189948)")]
    [SerializeField] private AudioClip proximityLurkingClip;
    [Tooltip("Chase ghost breath (audiopapkin-ghost-breath-297876)")]
    [SerializeField] private AudioClip chaseGhostBreathClip;
    [Tooltip("Deep monster random roar while lurking (dragon-studio-deep-sea-monster-roar-329857)")]
    [SerializeField] private AudioClip randomRoarClip;

    private AudioSource proximityAudioSource;
    private AudioSource actionAudioSource;
    private AudioSource chaseBreathAudioSource;
    private float lastNoticeSoundTime = -30f;
    private float lastScreechPlayTime = -99f;
    private float lastRandomRoarTime = -30f;

    [Header("Player Perception & Visibility")]
    [Tooltip("Distance within which Players 1, 2, and 4 can perceive the monster")]
    public float closeRangeVisibilityDistance = 8.0f;
    [Tooltip("How many seconds the monster remains visible after becoming unaggravated or moving away")]
    public float visibilityLingerDuration = 4.0f;

    private Renderer[] cachedRenderers;
    private float lastVisibleTriggerTime = -999f;
    private bool isCurrentlyVisibleToActivePlayer = true;

    // References
    private CharacterController controller;
    private Animator animator;
    private Transform playerTransform;
    private Camera playerCam;
    private Transform headBone;

    // Internal State
    private float lastAttackTime = -10f;
    private Vector3 verticalVelocity = Vector3.zero;
    private Vector3 desiredHorizontalVelocity = Vector3.zero;
    private string currentAnimState = "";
    private float twitchTimer = 0f;
    private float twitchAngle = 0f;

    // Obstacle Avoidance & Anti-Stuck Navigation State
    private float avoidanceSign = 1f;
    private float avoidanceTimer = 0f;
    private Vector3 lastRecordedPos;
    private float stuckCheckTimer = 0f;
    private bool isUnsticking = false;
    private float unstickTimer = 0f;

    // Line of Sight & Agitation Tracking
    private float lostSightTimer = 0f;
    private float lostSightGracePeriod = 2.5f;
    private Vector3 lastKnownPlayerPos;

    // Fatal Kill Execution State
    private bool isPerformingFatalKill = false;

    // Dynamic NavMesh Navigation & Waypoint Pathfinding
    private static bool _navMeshBuilt = false;
    private NavMeshPath _navPath;
    private int _currentPathIndex = 0;
    private float _lastPathCalcTime = -10f;
    private const float PATH_RECALC_INTERVAL = 0.25f;
    private Vector3 _currentSteeringWaypoint = Vector3.zero;

    public bool IsAgitated => (currentState == AIState.Chasing || currentState == AIState.Attacking);

    void OnEnable()
    {
        PlayerIdentityManager.OnIdentityChanged += HandleIdentityChanged;
    }

    void OnDisable()
    {
        PlayerIdentityManager.OnIdentityChanged -= HandleIdentityChanged;
        SetMonsterVisible(true);
    }

    private void HandleIdentityChanged(PlayerIdentity newId, PlayerIdentity oldId)
    {
        UpdatePlayerPerceptionVisibility();
    }

    public void PlayAnim(string stateName, float fadeTime = 0.15f)
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null)
        {
            if (!animator.enabled) animator.enabled = true;
            if (currentAnimState != stateName)
            {
                currentAnimState = stateName;
                animator.CrossFade(stateName, fadeTime);
            }
        }
    }

    public void PlayAnimLoop(string stateName, float fadeTime = 0.15f)
    {
        manualControl = true;
        if (stateName.Contains("Idle")) currentState = AIState.Lurking;
        else if (stateName.Contains("Walk")) currentState = AIState.Stalking;
        else if (stateName.Contains("Chase")) currentState = AIState.Chasing;
        PlayAnim(stateName, fadeTime);
    }

    public void PlayAnimExternal(string stateName, float fadeTime = 0.15f)
    {
        PlayAnimLoop(stateName, fadeTime);
    }

    public void TriggerAttackExternal()
    {
        manualControl = true;
        StopAllCoroutines();
        StartCoroutine(ExecuteAttackRoutine());
    }

    void Awake()
    {
        manualControl = false;
        loopInPlace = false;

        // Enforce room-appropriate ranges:
        // Reduced to 11.5m so creature does not spot and reveal itself
        // when players enter or explore the front area of the room (entrance dist ~18.5m).
        if (chaseRange < 10.0f || chaseRange > 45.0f) chaseRange = 15.0f;
        if (detectionRange < 10.0f || detectionRange > 45.0f) detectionRange = 13.0f;
        if (agitationRange < 8.0f || agitationRange > 45.0f) agitationRange = 11.5f;
        if (crouchAgitationRange <= 0.1f || crouchAgitationRange > 12.0f) crouchAgitationRange = 7.5f;
        if (spawnBoundaryMinX < 90.0f) spawnBoundaryMinX = 97.5f;
        if (maxSpawnDirectionZ > 22.8f || maxSpawnDirectionZ < 21.0f) maxSpawnDirectionZ = 22.8f;
        if (minRoomX < 70.0f || minRoomX > 75.0f) minRoomX = 73.2f;
        if (roomCenterPosition.sqrMagnitude < 1.0f) roomCenterPosition = new Vector3(88.0f, 0.05f, 11.0f);

        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // CharacterController dimensions matching upright creature
        if (controller != null)
        {
            controller.height = 2.2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1.1f, 0f);
            controller.stepOffset = 0.45f;
            controller.slopeLimit = 65.0f;
            controller.skinWidth = 0.04f;
        }

        // Scale-aware attack range default
        if (transform.localScale.y >= 1.5f && attackRange < 2.5f)
        {
            attackRange = 3.2f;
        }

        FindHeadBone();
        EnsureAudioSources();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        EnsureLevelNavMesh();
    }

    void Start()
    {
        EnsureLevelNavMesh();
        EnsureAudioSources();
        if (IsInsideCreatureRoom(transform.position))
        {
            roomCenterPosition = new Vector3(88.0f, transform.position.y, 11.0f);
        }
        LocatePlayer();
        PlayAnimLoop("Entity38_Idle", 0.05f);
    }

    /// <summary>
    /// Builds or ensures a runtime NavMesh is present across the level geometry
    /// so the monster can intelligently navigate around pillars, corners, and bend walls.
    /// </summary>
    public static void EnsureLevelNavMesh()
    {
        if (_navMeshBuilt && NavMesh.CalculateTriangulation().vertices.Length > 0) return;

        try
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 2.2f;
            settings.agentSlope = 50f;
            settings.agentClimb = 0.45f;

            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();

            Bounds fullBounds = new Bounds(new Vector3(50f, 2f, 30f), new Vector3(160f, 30f, 100f));
            NavMeshBuilder.CollectSources(fullBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);

            if (sources.Count > 0)
            {
                var navData = NavMeshBuilder.BuildNavMeshData(settings, sources, fullBounds, Vector3.zero, Quaternion.identity);
                if (navData != null)
                {
                    NavMesh.AddNavMeshData(navData);
                    _navMeshBuilt = true;
                    Debug.Log($"[Entity38EnemyAI] Dynamic Level NavMesh initialized successfully ({sources.Count} colliders).");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[Entity38EnemyAI] NavMesh build warning: " + ex.Message);
        }
    }

    private void EnsureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length >= 3)
        {
            proximityAudioSource = sources[0];
            actionAudioSource = sources[1];
            chaseBreathAudioSource = sources[2];
        }
        else if (sources.Length == 2)
        {
            proximityAudioSource = sources[0];
            actionAudioSource = sources[1];
            chaseBreathAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            proximityAudioSource = sources[0];
            actionAudioSource = gameObject.AddComponent<AudioSource>();
            chaseBreathAudioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            proximityAudioSource = gameObject.AddComponent<AudioSource>();
            actionAudioSource = gameObject.AddComponent<AudioSource>();
            chaseBreathAudioSource = gameObject.AddComponent<AudioSource>();
        }

        // Proximity lurking growl (3D spatial, loops when near player)
        proximityAudioSource.playOnAwake = false;
        proximityAudioSource.loop = true;
        proximityAudioSource.spatialBlend = 1.0f; // 3D
        proximityAudioSource.rolloffMode = AudioRolloffMode.Linear;
        proximityAudioSource.minDistance = 3.0f;
        proximityAudioSource.maxDistance = 32.0f;
        proximityAudioSource.volume = 0f;

        // Action one-shots (notice screech, attack impact, random roar)
        actionAudioSource.playOnAwake = false;
        actionAudioSource.loop = false;
        actionAudioSource.spatialBlend = 1.0f; // 3D
        actionAudioSource.rolloffMode = AudioRolloffMode.Linear;
        actionAudioSource.minDistance = 4.0f;
        actionAudioSource.maxDistance = 35.0f;
        actionAudioSource.volume = 0.95f;

        // Chase ghost breath (loops / spatial while chasing)
        chaseBreathAudioSource.playOnAwake = false;
        chaseBreathAudioSource.loop = true;
        chaseBreathAudioSource.spatialBlend = 1.0f;
        chaseBreathAudioSource.rolloffMode = AudioRolloffMode.Linear;
        chaseBreathAudioSource.minDistance = 3.0f;
        chaseBreathAudioSource.maxDistance = 32.0f;
        chaseBreathAudioSource.volume = 0f;

#if UNITY_EDITOR
        if (noticeAttackScreechClip == null)
        {
            noticeAttackScreechClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Screech_Notice_Attack.mp3");
            if (noticeAttackScreechClip == null)
                noticeAttackScreechClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/53439420-flying-monster-screech-02-461220.mp3");
        }
        if (proximityLurkingClip == null)
        {
            proximityLurkingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Lurking_Proximity.mp3");
            if (proximityLurkingClip == null)
                proximityLurkingClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/alesiadavina-horror-sound-lurking-horror-monster-189948.mp3");
        }
        if (chaseGhostBreathClip == null)
        {
            chaseGhostBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/audiopapkin-ghost-breath-297876.mp3");
            if (chaseGhostBreathClip == null)
                chaseGhostBreathClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Attack_GhostBreath.mp3");
        }
        if (randomRoarClip == null)
        {
            randomRoarClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/dragon-studio-deep-sea-monster-roar-329857.mp3");
            if (randomRoarClip == null)
                randomRoarClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Monster/Monster_Chase_Roar.mp3");
        }
#endif

        if (proximityLurkingClip != null && proximityAudioSource.clip == null)
        {
            proximityAudioSource.clip = proximityLurkingClip;
        }

        if (chaseGhostBreathClip != null && chaseBreathAudioSource.clip == null)
        {
            chaseBreathAudioSource.clip = chaseGhostBreathClip;
        }
    }

    private void PlayNoticeScreech()
    {
        PlayScreech(4.0f);
    }

    /// <summary>
    /// Plays the monster notice/attack screech (53439420-flying-monster-screech-02-461220),
    /// ensuring that the currently playing screech audio finishes completely before a new one can trigger.
    /// </summary>
    private void PlayScreech(float cooldown)
    {
        if (noticeAttackScreechClip == null || actionAudioSource == null) return;

        // Clip length is ~1.72s. Enforce that the previous screech must finish completely plus cooldown buffer!
        float clipDuration = noticeAttackScreechClip.length;
        float minSeparation = Mathf.Max(clipDuration + 0.35f, cooldown);
        if (Time.time - lastScreechPlayTime < minSeparation)
        {
            return;
        }

        lastScreechPlayTime = Time.time;
        lastNoticeSoundTime = Time.time;
        actionAudioSource.PlayOneShot(noticeAttackScreechClip, 1.0f);
    }

    /// <summary>
    /// Returns the effective detection range for the specified PlayerCharacter,
    /// respecting stealth crouching (significantly smaller detection cone/range).
    /// </summary>
    public float GetEffectiveDetectionRange(PlayerCharacter pc)
    {
        if (pc == null) return agitationRange;
        FirstPersonController fpc = pc.FirstPersonController;
        if (fpc == null) fpc = pc.GetComponent<FirstPersonController>();
        if (fpc != null && fpc.IsCrouching)
        {
            return crouchAgitationRange;
        }
        return agitationRange;
    }

    /// <summary>
    /// Returns the effective detection range for the specified Transform,
    /// respecting stealth crouching if the target has a FirstPersonController.
    /// </summary>
    public float GetEffectiveDetectionRange(Transform target)
    {
        if (target == null) return agitationRange;
        PlayerCharacter pc = target.GetComponent<PlayerCharacter>();
        if (pc != null) return GetEffectiveDetectionRange(pc);

        FirstPersonController fpc = target.GetComponent<FirstPersonController>();
        if (fpc == null) fpc = target.GetComponentInChildren<FirstPersonController>();
        if (fpc != null && fpc.IsCrouching)
        {
            return crouchAgitationRange;
        }
        return agitationRange;
    }

    /// <summary>
    /// Checks whether the creature has an unobstructed line of sight to the specified target.
    /// Casts rays from creature eye level (~1.9m) to the target's head and chest,
    /// adapting raycast elevations if the target is crouching.
    /// </summary>
    public bool HasLineOfSightTo(Transform target)
    {
        if (target == null) return false;

        if (headBone == null) FindHeadBone();
        float scaleY = Mathf.Max(1.0f, transform.localScale.y);
        Vector3 eyePos = (headBone != null)
            ? headBone.position
            : transform.position + Vector3.up * (1.9f * scaleY);

        bool isCrouched = false;
        FirstPersonController fpc = target.GetComponent<FirstPersonController>();
        if (fpc == null) fpc = target.GetComponentInChildren<FirstPersonController>();
        if (fpc != null && fpc.IsCrouching) isCrouched = true;

        Vector3[] checkPoints = isCrouched
            ? new Vector3[]
            {
                target.position + Vector3.up * 0.9f,
                target.position + Vector3.up * 0.45f
            }
            : new Vector3[]
            {
                target.position + Vector3.up * 1.5f,
                target.position + Vector3.up * 0.8f
            };

        int layerMask = ~LayerMask.GetMask("Ignore Raycast");

        foreach (var pPoint in checkPoints)
        {
            Vector3 diff = pPoint - eyePos;
            float dist = diff.magnitude;
            if (dist < 0.2f) return true;

            Vector3 dir = diff / dist;
            float startOffset = 0.45f * scaleY;
            Vector3 startPos = eyePos + dir * startOffset;
            float castDist = Mathf.Max(0.1f, dist - startOffset);

            RaycastHit hit;
            if (Physics.Raycast(startPos, dir, out hit, castDist, layerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == target || hit.transform.IsChildOf(target) || hit.collider.CompareTag("Player"))
                {
                    return true;
                }
                continue;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the specified position is strictly within Monster 38's room (Area 5 Creature Encounter Room).
    /// Denied exit towards west connecting hallway / fuse room (X < minRoomX)
    /// and north passage towards Room 4 (X >= spawnBoundaryMinX && Z > maxSpawnDirectionZ).
    /// </summary>
    public bool IsInsideCreatureRoom(Vector3 pos)
    {
        // Denied exit to west connecting hallway / fuse cell room:
        if (pos.x < minRoomX) return false;

        // Denied exit north into passage back to Room 4:
        if (pos.x >= spawnBoundaryMinX && pos.z > maxSpawnDirectionZ) return false;

        // Outside room outer perimeter
        if (pos.x > maxRoomX + 1.0f || pos.z < minRoomZ - 1.0f || pos.z > 24.5f) return false;

        return true;
    }

    /// <summary>
    /// Checks whether the specified position is outside the creature's allowed room territory.
    /// Returns true if the player is in the north spawn passage OR in the west connecting hallway / fuse cell room.
    /// </summary>
    public bool IsInSpawnPassage(Vector3 pos)
    {
        return !IsInsideCreatureRoom(pos);
    }

    /// <summary>
    /// Checks if ANY valid player is within immediate melee reach (attackRange + 0.5m) with line of sight.
    /// Used for opportunistic strike execution so the monster immediately attacks anyone who comes too close,
    /// even if it was previously chasing someone else!
    /// </summary>
    public PlayerCharacter FindPlayerInMeleeRange()
    {
        float meleeReach = attackRange + 0.5f;
        PlayerCharacter closestMelee = null;
        float bestMeleeDist = float.MaxValue;

        System.Collections.Generic.IEnumerable<PlayerCharacter> candidates = null;
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.AllPlayers != null)
        {
            candidates = MultiPlayerCharacterManager.Instance.AllPlayers;
        }
        else
        {
            candidates = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude);
        }

        if (candidates != null)
        {
            foreach (var pc in candidates)
            {
                if (pc == null || !pc.gameObject.activeInHierarchy) continue;
                if (IsInSpawnPassage(pc.transform.position)) continue;

                float d = Vector3.Distance(transform.position, pc.transform.position);
                if (d <= meleeReach && (d <= 2.2f || HasLineOfSightTo(pc.transform)))
                {
                    if (d < bestMeleeDist)
                    {
                        bestMeleeDist = d;
                        closestMelee = pc;
                    }
                }
            }
        }

        return closestMelee;
    }

    /// <summary>
    /// Evaluates candidate players and returns the CLOSEST player that meets BOTH proximity (effective detection range)
    /// AND Line of Sight, and has NOT retreated backwards into the spawn portal corridor.
    /// Supports crouch stealth: crouching players have a reduced detection range (crouchAgitationRange).
    /// If prioritizeMelee is true and any valid player is within melee reach, returns that player immediately.
    /// </summary>
    public PlayerCharacter FindAgitatingPlayer(bool prioritizeMelee = true)
    {
        if (prioritizeMelee)
        {
            PlayerCharacter melee = FindPlayerInMeleeRange();
            if (melee != null) return melee;
        }

        PlayerCharacter best = null;
        float bestDist = float.MaxValue;

        System.Collections.Generic.IEnumerable<PlayerCharacter> candidates = null;
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.AllPlayers != null)
        {
            candidates = MultiPlayerCharacterManager.Instance.AllPlayers;
        }
        else
        {
            candidates = UnityEngine.Object.FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Exclude);
        }

        if (candidates != null)
        {
            foreach (var pc in candidates)
            {
                if (pc == null || !pc.gameObject.activeInHierarchy) continue;

                // Must NOT be in the north spawn corridor back towards Room 4
                if (IsInSpawnPassage(pc.transform.position)) continue;

                float maxRange = GetEffectiveDetectionRange(pc);
                float d = Vector3.Distance(transform.position, pc.transform.position);
                if (d <= maxRange && HasLineOfSightTo(pc.transform))
                {
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = pc;
                    }
                }
            }
        }

        return best;
    }

    public Transform FindFallbackAgitatingPlayer()
    {
        var playerObjs = GameObject.FindGameObjectsWithTag("Player");
        Transform best = null;
        float bestDist = float.MaxValue;
        float meleeReach = attackRange + 0.5f;

        foreach (var pObj in playerObjs)
        {
            if (pObj == null || !pObj.activeInHierarchy) continue;
            Transform candidate = pObj.transform;

            if (IsInSpawnPassage(candidate.position)) continue;

            float d = Vector3.Distance(transform.position, candidate.position);

            // Immediate melee reach check
            if (d <= meleeReach && (d <= 2.2f || HasLineOfSightTo(candidate)))
            {
                return candidate;
            }

            float maxRange = GetEffectiveDetectionRange(candidate);
            if (d <= maxRange && HasLineOfSightTo(candidate))
            {
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidate;
                }
            }
        }

        return best;
    }

    private void LocatePlayer()
    {
        // 1. Opportunistic immediate melee check:
        PlayerCharacter meleeTarget = FindPlayerInMeleeRange();
        if (meleeTarget != null)
        {
            playerTransform = meleeTarget.transform;
            lostSightTimer = 0f;
            lastKnownPlayerPos = playerTransform.position;
            UpdatePlayerCameraRef();
            return;
        }

        if (MultiPlayerCharacterManager.Instance != null)
        {
            // Search for the closest agitating player in LOS & within their effective detection range
            PlayerCharacter agitating = FindAgitatingPlayer(prioritizeMelee: true);
            if (agitating != null)
            {
                // Dynamic retargeting: If currently chasing another target, switch to the closer player!
                if (currentState == AIState.Chasing && playerTransform != null && agitating.transform != playerTransform)
                {
                    float currentDist = Vector3.Distance(transform.position, playerTransform.position);
                    float newDist = Vector3.Distance(transform.position, agitating.transform.position);

                    bool currentInSpawn = IsInSpawnPassage(playerTransform.position);
                    bool currentLostLOS = !HasLineOfSightTo(playerTransform);

                    // Switch if candidate is closer by 0.5m margin OR within immediate strike reach OR current lost sight/fled
                    if (newDist < currentDist - 0.5f || newDist <= attackRange + 1.0f || currentInSpawn || currentLostLOS)
                    {
                        playerTransform = agitating.transform;
                        lostSightTimer = 0f;
                        lastKnownPlayerPos = playerTransform.position;
                    }
                }
                else
                {
                    playerTransform = agitating.transform;
                }
            }
            else
            {
                // If not currently chasing, track active player for passive look-at without agitating
                if (currentState != AIState.Chasing && currentState != AIState.Attacking)
                {
                    PlayerCharacter active = MultiPlayerCharacterManager.Instance != null ? MultiPlayerCharacterManager.Instance.ActivePlayer : null;
                    if (active != null)
                    {
                        playerTransform = active.transform;
                    }
                    else
                    {
                        Transform fallback = FindFallbackAgitatingPlayer();
                        if (fallback != null) playerTransform = fallback;
                    }
                }
            }

            UpdatePlayerCameraRef();
            return;
        }

        Transform fallbackAgitating = FindFallbackAgitatingPlayer();
        if (fallbackAgitating != null)
        {
            if (currentState == AIState.Chasing && playerTransform != null && fallbackAgitating != playerTransform)
            {
                float currentDist = Vector3.Distance(transform.position, playerTransform.position);
                float newDist = Vector3.Distance(transform.position, fallbackAgitating.position);
                bool currentInSpawn = IsInSpawnPassage(playerTransform.position);
                bool currentLostLOS = !HasLineOfSightTo(playerTransform);
                if (newDist < currentDist - 0.5f || newDist <= attackRange + 1.0f || currentInSpawn || currentLostLOS)
                {
                    playerTransform = fallbackAgitating;
                    lostSightTimer = 0f;
                    lastKnownPlayerPos = playerTransform.position;
                }
            }
            else
            {
                playerTransform = fallbackAgitating;
            }
        }
        else
        {
            if (currentState != AIState.Chasing && currentState != AIState.Attacking)
            {
                var fps = Object.FindAnyObjectByType<FirstPersonController>();
                if (fps != null)
                {
                    playerTransform = fps.transform;
                    playerCam = fps.playerCamera != null ? fps.playerCamera.GetComponent<Camera>() : Camera.main;
                }
                else
                {
                    var pObj = GameObject.FindWithTag("Player");
                    if (pObj != null) playerTransform = pObj.transform;
                    if (playerCam == null) playerCam = Camera.main;
                }
            }
        }

        UpdatePlayerCameraRef();
    }

    private void UpdatePlayerCameraRef()
    {
        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.MainCamera != null)
        {
            playerCam = MultiPlayerCharacterManager.Instance.MainCamera;
        }
        else if (Camera.main != null)
        {
            playerCam = Camera.main;
        }
    }

    private void FindHeadBone()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("spine.006") || n.Contains("head") || n.Contains("neck"))
            {
                headBone = t;
                break;
            }
        }
    }

    void Update()
    {
        if (isPerformingFatalKill) return;

        if (playerTransform == null || Time.frameCount % 8 == 0)
        {
            LocatePlayer();
            if (playerTransform == null) return;
        }

        UpdatePlayerPerceptionVisibility();
        UpdateAntiStuckCheck();

        // Toggle Debug GUI with F10 (never hijack gameplay keys like Space or 1-4)
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame)
        {
            showDebugGUI = !showDebugGUI;
        }
#else
        if (Input.GetKeyDown(KeyCode.F10))
        {
            showDebugGUI = !showDebugGUI;
        }
#endif

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // State Machine evaluation determines desired horizontal velocity
        switch (currentState)
        {
            case AIState.Lurking:
                UpdateLurking(distToPlayer);
                break;
            case AIState.Stalking:
                UpdateStalking(distToPlayer);
                break;
            case AIState.Chasing:
                UpdateChasing(distToPlayer);
                break;
            case AIState.ReturningToCenter:
                UpdateReturningToCenter();
                break;
            case AIState.Attacking:
            case AIState.Stunned:
                desiredHorizontalVelocity = Vector3.zero;
                break;
        }

        // Apply robust gravity with grounded check and terminal velocity cap
        if (controller.isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2.0f; // Firm downward grounding bias
        }
        else
        {
            verticalVelocity.y += -20.0f * Time.deltaTime;
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, -15.0f);
        }

        // Single unified Move call per frame to avoid physics tunneling
        Vector3 totalMovement = (desiredHorizontalVelocity + verticalVelocity) * Time.deltaTime;
        controller.Move(totalMovement);

        // Fallback emergency clamp only if character falls completely through floor into void below -10m
        if (transform.position.y < -10.0f)
        {
            transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            verticalVelocity = Vector3.zero;
        }

        // Territory Boundary: Creature MUST NOT exit Area 5 (denied exit to west fuse room & north passage)
        EnforceRoomContainmentBoundary();

        // Proximity Lurking Growl Volume Update (alesiadavina-horror-sound-lurking-horror-monster-189948)
        if (proximityAudioSource != null && proximityLurkingClip != null)
        {
            float d = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 999f;
            // Only play proximity growl if player is in line of sight or very close (< 6m), and inside the room
            bool canHearGrowl = (playerTransform != null &&
                                 !IsInSpawnPassage(playerTransform.position) &&
                                 (HasLineOfSightTo(playerTransform) || d <= 6.0f));
            float targetVol = (canHearGrowl && d <= agitationRange) ? Mathf.Clamp01(1.0f - (d / agitationRange)) * 0.85f : 0f;
            proximityAudioSource.volume = Mathf.MoveTowards(proximityAudioSource.volume, targetVol, Time.deltaTime * 0.8f);
            if (proximityAudioSource.volume > 0.01f && !proximityAudioSource.isPlaying)
            {
                proximityAudioSource.Play();
            }
            else if (proximityAudioSource.volume <= 0.005f && proximityAudioSource.isPlaying)
            {
                proximityAudioSource.Stop();
            }
        }

        // Chase Ghost Breath Volume Update (audiopapkin-ghost-breath-297876)
        if (chaseBreathAudioSource != null && chaseGhostBreathClip != null)
        {
            float d = playerTransform != null ? Vector3.Distance(transform.position, playerTransform.position) : 999f;
            float targetChaseVol = (currentState == AIState.Chasing && d <= agitationRange + 4.0f) ? Mathf.Clamp01(1.0f - (d / (agitationRange + 4.0f))) * 0.85f : 0f;
            chaseBreathAudioSource.volume = Mathf.MoveTowards(chaseBreathAudioSource.volume, targetChaseVol, Time.deltaTime * 2.0f);
            if (chaseBreathAudioSource.volume > 0.01f && !chaseBreathAudioSource.isPlaying)
            {
                chaseBreathAudioSource.Play();
            }
            else if (chaseBreathAudioSource.volume <= 0.005f && chaseBreathAudioSource.isPlaying)
            {
                chaseBreathAudioSource.Stop();
            }
        }

        // Update twitch timer
        twitchTimer += Time.deltaTime;
        if (twitchTimer > Random.Range(1.5f, 4.0f))
        {
            twitchTimer = 0f;
            twitchAngle = Random.Range(-twitchIntensity, twitchIntensity);
        }
    }

    /// <summary>
    /// Strictly contains Monster 38 inside Area 5 (Creature Room).
    /// Denies exit towards the west hallway (fuse cell room) and north passage (Room 4).
    /// </summary>
    public void EnforceRoomContainmentBoundary()
    {
        Vector3 pos = transform.position;
        bool clamped = false;

        // 1. Deny exit West towards Connecting Hallway & Fuse Cell Room (X < minRoomX)
        if (pos.x < minRoomX)
        {
            pos.x = minRoomX;
            if (desiredHorizontalVelocity.x < 0f) desiredHorizontalVelocity.x = 0f;
            clamped = true;
        }

        // 2. Deny exit North into passage back towards Room 4 (X >= spawnBoundaryMinX && Z > maxSpawnDirectionZ)
        if (pos.x >= spawnBoundaryMinX && pos.z > maxSpawnDirectionZ)
        {
            pos.z = maxSpawnDirectionZ;
            if (desiredHorizontalVelocity.z > 0f) desiredHorizontalVelocity.z = 0f;
            clamped = true;
        }

        // 3. General room limits (East and South walls)
        if (pos.x > maxRoomX)
        {
            pos.x = maxRoomX;
            if (desiredHorizontalVelocity.x > 0f) desiredHorizontalVelocity.x = 0f;
            clamped = true;
        }
        if (pos.z < minRoomZ)
        {
            pos.z = minRoomZ;
            if (desiredHorizontalVelocity.z < 0f) desiredHorizontalVelocity.z = 0f;
            clamped = true;
        }

        if (clamped)
        {
            transform.position = pos;
        }
    }

    public void EnforceSpawnTerritoryBoundary()
    {
        EnforceRoomContainmentBoundary();
    }

    /// <summary>
    /// Begins de-aggravation and naturally walks back towards the center of Room 5
    /// so that players who ran back or sneaked away are not blocked.
    /// </summary>
    public void StartReturningToCenter()
    {
        playerTransform = null;
        lostSightTimer = 0f;

        Vector3 targetCenter = roomCenterPosition;
        targetCenter.y = transform.position.y;

        float distToCenter = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(targetCenter.x, targetCenter.z));

        // If already at or very close to center (within centerArrivalRadius), immediately lurk
        if (distToCenter <= centerArrivalRadius)
        {
            currentState = AIState.Lurking;
            desiredHorizontalVelocity = Vector3.zero;
            PlayAnim("Entity38_Idle", 0.25f);
            return;
        }

        currentState = AIState.ReturningToCenter;
        PlayAnim("Entity38_Walk", 0.25f);
        if (animator != null) animator.speed = stalkAnimSpeed;
    }

    private void UpdateReturningToCenter()
    {
        PlayAnim("Entity38_Walk", 0.25f);
        if (animator != null) animator.speed = stalkAnimSpeed;

        // 1. Perception check: while walking back to the center,
        // if a player inside the room is seen within detection range, re-agitate!
        PlayerCharacter agitatingPc = FindAgitatingPlayer(prioritizeMelee: true);
        if (agitatingPc != null)
        {
            playerTransform = agitatingPc.transform;
            currentState = AIState.Chasing;
            lostSightTimer = 0f;
            lastKnownPlayerPos = playerTransform.position;
            PlayNoticeScreech();
            return;
        }

        // 2. Check distance to room center
        Vector3 targetCenter = roomCenterPosition;
        targetCenter.y = transform.position.y;

        float distToCenter = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(targetCenter.x, targetCenter.z));

        if (distToCenter <= centerArrivalRadius)
        {
            // Reached room center! Settle into Lurking state.
            currentState = AIState.Lurking;
            desiredHorizontalVelocity = Vector3.zero;
            PlayAnim("Entity38_Idle", 0.35f);
            return;
        }

        // 3. Move towards room center using NavMesh pathfinding / steering to avoid pillars
        Vector3 moveDir = GetSteeredMoveDirection(targetCenter);

        // Enforce room exit clamps on moveDir
        if (transform.position.x <= minRoomX + 0.3f && moveDir.x < 0f) moveDir.x = 0f;
        if (transform.position.x >= spawnBoundaryMinX && transform.position.z >= maxSpawnDirectionZ - 0.3f && moveDir.z > 0f) moveDir.z = 0f;
        if (moveDir.sqrMagnitude > 0.001f) moveDir = moveDir.normalized;

        Vector3 lookTarget = (_currentSteeringWaypoint != Vector3.zero)
            ? _currentSteeringWaypoint
            : targetCenter;
        lookTarget.y = transform.position.y;
        FaceTarget(lookTarget, rotationSpeed * 0.8f);

        desiredHorizontalVelocity = moveDir * returnWalkSpeed;
    }

    private void UpdateLurking(float dist)
    {
        // If creature is noticeably away from the room center while lurking, naturally walk back
        Vector3 targetCenter = roomCenterPosition;
        float distToCenter = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(targetCenter.x, targetCenter.z));
        if (distToCenter > 3.5f)
        {
            StartReturningToCenter();
            return;
        }

        PlayAnim("Entity38_Idle", 0.25f);
        if (animator != null) animator.speed = lurkAnimSpeed;
        desiredHorizontalVelocity = Vector3.zero;

        // Search for any candidate player that satisfies BOTH proximity and Line of Sight
        PlayerCharacter agitatingPc = FindAgitatingPlayer(prioritizeMelee: true);
        Transform agitatingTarget = (agitatingPc != null) ? agitatingPc.transform : null;
        if (agitatingTarget == null && MultiPlayerCharacterManager.Instance == null)
        {
            agitatingTarget = FindFallbackAgitatingPlayer();
        }

        if (agitatingTarget != null)
        {
            playerTransform = agitatingTarget;
            currentState = AIState.Chasing;
            lostSightTimer = 0f;
            lastKnownPlayerPos = playerTransform.position;
            PlayNoticeScreech();
            return;
        }

        // When lurking unagitated, face player if within reasonable notice zone
        if (playerTransform != null && !IsInSpawnPassage(playerTransform.position))
        {
            FaceTarget(playerTransform.position, rotationSpeed * 0.4f);
        }

        // Random monster roar while lurking ONLY IF player is inside the room and in line of sight
        if (randomRoarClip != null && actionAudioSource != null && Time.time - lastRandomRoarTime > 25.0f)
        {
            if (playerTransform != null &&
                !IsInSpawnPassage(playerTransform.position) &&
                HasLineOfSightTo(playerTransform))
            {
                if (Random.value < 0.35f)
                {
                    lastRandomRoarTime = Time.time;
                    actionAudioSource.PlayOneShot(randomRoarClip, 0.85f);
                }
                else
                {
                    lastRandomRoarTime = Time.time - 12.0f;
                }
            }
        }

        if (canAttack && dist <= attackRange && Time.time >= lastAttackTime + attackCooldown && HasLineOfSightTo(playerTransform))
        {
            StartCoroutine(ExecuteAttackRoutine());
        }
    }

    private void UpdateStalking(float dist)
    {
        float maxRange = GetEffectiveDetectionRange(playerTransform);
        // Stalking is an intermediate creep state: verify player is still in room and range
        if (playerTransform == null ||
            !IsInsideCreatureRoom(playerTransform.position) ||
            dist > maxRange * 1.2f)
        {
            StartReturningToCenter();
            return;
        }

        // 1. Opportunistic immediate melee check:
        PlayerCharacter stalkMelee = FindPlayerInMeleeRange();
        if (stalkMelee != null)
        {
            playerTransform = stalkMelee.transform;
            dist = Vector3.Distance(transform.position, playerTransform.position);
            FaceTarget(playerTransform.position, rotationSpeed * 2.0f);
            if (canAttack && Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(ExecuteAttackRoutine());
                return;
            }
        }

        // 2. Check if another player is closer
        PlayerCharacter closer = FindAgitatingPlayer(prioritizeMelee: true);
        if (closer != null && closer.transform != playerTransform)
        {
            float candidateDist = Vector3.Distance(transform.position, closer.transform.position);
            if (candidateDist < dist - 0.5f || candidateDist <= attackRange + 1.0f)
            {
                playerTransform = closer.transform;
                dist = candidateDist;
                lostSightTimer = 0f;
                lastKnownPlayerPos = playerTransform.position;
            }
        }

        PlayAnim("Entity38_Walk", 0.2f);
        if (animator != null) animator.speed = stalkAnimSpeed;

        if (dist <= attackRange)
        {
            FaceTarget(playerTransform.position, rotationSpeed);
            desiredHorizontalVelocity = Vector3.zero;
            if (canAttack && Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(ExecuteAttackRoutine());
            }
        }
        else
        {
            Vector3 moveDir = GetSteeredMoveDirection(playerTransform.position);
            // Clamp moveDir if it would push towards spawn passage or west exit
            if (transform.position.x >= spawnBoundaryMinX && transform.position.z >= maxSpawnDirectionZ - 0.3f && moveDir.z > 0f)
            {
                moveDir.z = 0f;
            }
            if (transform.position.x <= minRoomX + 0.35f && moveDir.x < 0f)
            {
                moveDir.x = 0f;
            }
            if (moveDir.sqrMagnitude > 0.001f) moveDir = moveDir.normalized;

            Vector3 lookTarget = (_currentSteeringWaypoint != Vector3.zero && !HasLineOfSightTo(playerTransform))
                ? _currentSteeringWaypoint
                : playerTransform.position;
            FaceTarget(lookTarget, rotationSpeed);

            desiredHorizontalVelocity = moveDir * stalkSpeed;
        }

        // If player is in clear LOS and within agitation range, escalate to Chasing
        if (HasLineOfSightTo(playerTransform) && dist <= GetEffectiveDetectionRange(playerTransform))
        {
            currentState = AIState.Chasing;
            lostSightTimer = 0f;
            lastKnownPlayerPos = playerTransform.position;
            PlayNoticeScreech();
        }
    }

    private void UpdateChasing(float dist)
    {
        PlayAnim("Entity38_Chase", 0.15f);
        if (animator != null) animator.speed = chaseAnimSpeed;

        if (playerTransform == null)
        {
            StartReturningToCenter();
            return;
        }

        // If target player fled the creature room (either north to Room 4 or west to fuse cell room),
        // DO NOT follow outside!
        if (!IsInsideCreatureRoom(playerTransform.position))
        {
            PlayerCharacter altPlayer = FindAgitatingPlayer(prioritizeMelee: true);
            if (altPlayer != null)
            {
                playerTransform = altPlayer.transform;
                lostSightTimer = 0f;
                lastKnownPlayerPos = playerTransform.position;
                dist = Vector3.Distance(transform.position, playerTransform.position);
            }
            else
            {
                StartReturningToCenter();
                return;
            }
        }

        // 1. Opportunistic immediate melee check:
        // Even if chasing someone further away, if ANY valid player is within melee reach,
        // immediately strike the closest player!
        PlayerCharacter meleeStrikeTarget = FindPlayerInMeleeRange();
        if (meleeStrikeTarget != null)
        {
            playerTransform = meleeStrikeTarget.transform;
            dist = Vector3.Distance(transform.position, playerTransform.position);
            FaceTarget(playerTransform.position, rotationSpeed * 2.0f);
            if (canAttack && Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(ExecuteAttackRoutine());
                return;
            }
        }

        // 2. Dynamic closest player retargeting during pursuit:
        // Always check if another player is closer than current target!
        PlayerCharacter closestPlayer = FindAgitatingPlayer(prioritizeMelee: true);
        if (closestPlayer != null && closestPlayer.transform != playerTransform)
        {
            float candidateDist = Vector3.Distance(transform.position, closestPlayer.transform.position);
            bool currentLost = (!HasLineOfSightTo(playerTransform) || !IsInsideCreatureRoom(playerTransform.position));
            if (candidateDist < dist - 0.5f || candidateDist <= attackRange + 1.0f || currentLost)
            {
                playerTransform = closestPlayer.transform;
                dist = candidateDist;
                lostSightTimer = 0f;
                lastKnownPlayerPos = playerTransform.position;
            }
        }

        // 3. Line of Sight verification
        bool hasLOS = HasLineOfSightTo(playerTransform);
        if (hasLOS)
        {
            lostSightTimer = 0f;
            lastKnownPlayerPos = playerTransform.position;
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            // If sight lost for longer than grace period, try reacquiring any other player in LOS
            if (lostSightTimer > lostSightGracePeriod)
            {
                PlayerCharacter altPlayer = FindAgitatingPlayer(prioritizeMelee: true);
                if (altPlayer != null)
                {
                    playerTransform = altPlayer.transform;
                    lostSightTimer = 0f;
                    lastKnownPlayerPos = playerTransform.position;
                    hasLOS = true;
                    dist = Vector3.Distance(transform.position, playerTransform.position);
                }
                else
                {
                    // No visible players in room -> creature loses agitation and naturally walks back to center
                    StartReturningToCenter();
                    return;
                }
            }
        }

        if (dist <= attackRange)
        {
            FaceTarget(playerTransform.position, rotationSpeed * 1.5f);
            desiredHorizontalVelocity = Vector3.zero;
            if (canAttack && Time.time >= lastAttackTime + attackCooldown)
            {
                StartCoroutine(ExecuteAttackRoutine());
                return;
            }
        }
        else
        {
            Vector3 targetPos = hasLOS ? playerTransform.position : lastKnownPlayerPos;
            Vector3 moveDir = GetSteeredMoveDirection(targetPos);

            // Clamp moveDir if it would push towards spawn passage or west exit
            if (transform.position.x >= spawnBoundaryMinX && transform.position.z >= maxSpawnDirectionZ - 0.3f && moveDir.z > 0f)
            {
                moveDir.z = 0f;
            }
            if (transform.position.x <= minRoomX + 0.35f && moveDir.x < 0f)
            {
                moveDir.x = 0f;
            }
            if (moveDir.sqrMagnitude > 0.001f) moveDir = moveDir.normalized;

            Vector3 lookTarget = (_currentSteeringWaypoint != Vector3.zero && !hasLOS)
                ? _currentSteeringWaypoint
                : playerTransform.position;
            FaceTarget(lookTarget, rotationSpeed * 1.5f);

            desiredHorizontalVelocity = moveDir * chaseSpeed;
        }

        float maxRange = GetEffectiveDetectionRange(playerTransform);
        if (dist > maxRange * 1.5f)
        {
            StartReturningToCenter();
        }
    }

    private void UpdateAntiStuckCheck()
    {
        if (unstickTimer > 0f)
        {
            unstickTimer -= Time.deltaTime;
            if (unstickTimer <= 0f) isUnsticking = false;
        }

        if (avoidanceTimer > 0f)
        {
            avoidanceTimer -= Time.deltaTime;
        }

        stuckCheckTimer += Time.deltaTime;
        if (stuckCheckTimer >= 0.25f)
        {
            stuckCheckTimer = 0f;
            Vector3 currentPos = transform.position;
            float horizontalDist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(lastRecordedPos.x, lastRecordedPos.z));

            if ((currentState == AIState.Chasing || currentState == AIState.Stalking) &&
                desiredHorizontalVelocity.sqrMagnitude > 1.0f && horizontalDist < 0.05f)
            {
                // Creature has collided and is stuck against a pillar or wall!
                // Flip avoidance sign and trigger an immediate perpendicular slide impulse
                avoidanceSign = -avoidanceSign;
                avoidanceTimer = 0.9f;
                isUnsticking = true;
                unstickTimer = 0.4f;
            }
            lastRecordedPos = currentPos;
        }
    }

    private bool HasClearDirectPath(Vector3 targetPos)
    {
        float scaleY = Mathf.Max(1.0f, transform.localScale.y);
        Vector3 rayOrigin = transform.position + Vector3.up * (0.8f * scaleY);
        Vector3 target = targetPos + Vector3.up * 1.0f;
        Vector3 dir = (target - rayOrigin);
        float dist = dir.magnitude;
        if (dist < 0.2f) return true;

        if (Physics.SphereCast(rayOrigin, 0.35f, dir.normalized, out RaycastHit hit, dist))
        {
            if (hit.collider != null && !hit.collider.CompareTag("Player") && !hit.collider.isTrigger)
            {
                return false; // Direct path blocked by obstacle/wall
            }
        }
        return true;
    }

    private Vector3 GetNavMeshMoveDirection(Vector3 targetPos)
    {
        if (_navPath == null) _navPath = new NavMeshPath();

        // Recalculate path periodically or on invalid path
        if (Time.time >= _lastPathCalcTime + PATH_RECALC_INTERVAL || _navPath.status != NavMeshPathStatus.PathComplete)
        {
            _lastPathCalcTime = Time.time;
            Vector3 startPos = transform.position;
            if (NavMesh.SamplePosition(startPos, out NavMeshHit startHit, 3.5f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, 3.5f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, _navPath);
                _currentPathIndex = 1;
            }
        }

        if (_navPath != null && _navPath.status == NavMeshPathStatus.PathComplete && _navPath.corners.Length > 1)
        {
            // Advance along path corners as we reach them
            while (_currentPathIndex < _navPath.corners.Length - 1)
            {
                Vector3 toCorner = _navPath.corners[_currentPathIndex] - transform.position;
                toCorner.y = 0;
                if (toCorner.magnitude < 0.85f) // Reached this waypoint
                {
                    _currentPathIndex++;
                }
                else
                {
                    break;
                }
            }

            if (_currentPathIndex < _navPath.corners.Length)
            {
                Vector3 waypoint = _navPath.corners[_currentPathIndex];
                _currentSteeringWaypoint = waypoint;
                Vector3 dir = (waypoint - transform.position);
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    return dir.normalized;
                }
            }
        }

        return Vector3.zero;
    }

    private Vector3 GetSteeredMoveDirection(Vector3 targetPos)
    {
        // 1. Emergency unsticking override if wedged on corner
        if (isUnsticking)
        {
            Vector3 directDir = (targetPos - transform.position);
            directDir.y = 0;
            Vector3 unstickDir = Quaternion.AngleAxis(90f * avoidanceSign, Vector3.up) * directDir;
            return unstickDir.normalized;
        }

        // 2. Direct path forward check - if 100% open, charge straight
        if (HasClearDirectPath(targetPos))
        {
            _currentSteeringWaypoint = Vector3.zero;
            Vector3 directDir = (targetPos - transform.position);
            directDir.y = 0;
            if (directDir.sqrMagnitude > 0.001f)
            {
                return directDir.normalized;
            }
        }

        // 3. Obstacle / bend wall / pillar detected: navigate via NavMesh path
        Vector3 navDir = GetNavMeshMoveDirection(targetPos);
        if (navDir.sqrMagnitude > 0.01f)
        {
            // Clamp if steering towards spawn passage or west exit
            if (transform.position.x >= spawnBoundaryMinX && transform.position.z >= maxSpawnDirectionZ - 0.35f && navDir.z > 0f)
            {
                navDir.z = 0f;
            }
            if (transform.position.x <= minRoomX + 0.35f && navDir.x < 0f)
            {
                navDir.x = 0f;
            }
            if (navDir.sqrMagnitude < 0.001f) return Vector3.zero;
            return navDir.normalized;
        }

        // 4. Fallback to local whisker avoidance
        return GetLocalWhiskerDirection(targetPos);
    }

    private Vector3 GetLocalWhiskerDirection(Vector3 targetPos)
    {
        Vector3 directDir = (targetPos - transform.position);
        directDir.y = 0;
        if (directDir.sqrMagnitude < 0.001f) return Vector3.zero;
        directDir.Normalize();

        float scaleY = Mathf.Max(1.0f, transform.localScale.y);
        Vector3 rayOrigin = transform.position + Vector3.up * (0.6f * scaleY);
        float feelerRadius = 0.38f * scaleY;
        float feelerDist = 2.2f * scaleY;

        // Obstacle (pillar / column / crate) detected!
        // If avoidance direction isn't locked by timer, decide best side based on whisker clearance
        if (avoidanceTimer <= 0f)
        {
            float rightClearance = EvaluateSideClearance(rayOrigin, directDir, 1f, feelerRadius);
            float leftClearance = EvaluateSideClearance(rayOrigin, directDir, -1f, feelerRadius);

            avoidanceSign = (rightClearance >= leftClearance) ? 1f : -1f;
            avoidanceTimer = 0.7f; // Stay committed to this side to smoothly orbit the pillar
        }

        // Search for the closest clear angle around the obstacle
        float[] testAngles = new float[] { 30f, 50f, 75f, 95f, 120f };
        Vector3 bestDetour = Vector3.zero;
        float bestDist = 0f;

        for (int i = 0; i < testAngles.Length; i++)
        {
            Vector3 candidateDir = Quaternion.AngleAxis(testAngles[i] * avoidanceSign, Vector3.up) * directDir;
            if (Physics.SphereCast(rayOrigin, feelerRadius, candidateDir, out RaycastHit whiskerHit, feelerDist))
            {
                if (whiskerHit.collider != null && !whiskerHit.collider.CompareTag("Player") && !whiskerHit.collider.isTrigger)
                {
                    if (whiskerHit.distance > bestDist)
                    {
                        bestDist = whiskerHit.distance;
                        bestDetour = candidateDir;
                    }
                    continue;
                }
            }

            // Candidate direction is 100% open!
            bestDetour = candidateDir;
            break;
        }

        if (bestDetour == Vector3.zero)
        {
            // Fallback: tangent along hit surface
            bestDetour = Quaternion.AngleAxis(90f * avoidanceSign, Vector3.up) * directDir;
        }

        // Smooth steering vector that rounds the pillar without pushing into it
        Vector3 finalDir = Vector3.Slerp(directDir, bestDetour, 0.85f);
        finalDir.y = 0;

        // Prevent final direction from steering creature backwards past spawn territory boundary or west exit
        if (transform.position.x >= spawnBoundaryMinX && transform.position.z >= maxSpawnDirectionZ - 0.35f && finalDir.z > 0f)
        {
            finalDir.z = 0f;
        }
        if (transform.position.x <= minRoomX + 0.35f && finalDir.x < 0f)
        {
            finalDir.x = 0f;
        }
        if (finalDir.sqrMagnitude < 0.001f) return Vector3.zero;

        return finalDir.normalized;
    }

    private float EvaluateSideClearance(Vector3 origin, Vector3 forward, float sign, float radius)
    {
        float totalClearance = 0f;
        float[] sampleAngles = new float[] { 35f, 65f, 95f };
        float maxSampleDist = 3.5f * Mathf.Max(1.0f, transform.localScale.y);
        for (int i = 0; i < sampleAngles.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(sampleAngles[i] * sign, Vector3.up) * forward;
            if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, maxSampleDist))
            {
                if (hit.collider != null && !hit.collider.CompareTag("Player") && !hit.collider.isTrigger)
                {
                    totalClearance += hit.distance;
                    continue;
                }
            }
            totalClearance += maxSampleDist;
        }
        return totalClearance;
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        currentState = AIState.Attacking;
        lastAttackTime = Time.time;
        desiredHorizontalVelocity = Vector3.zero;

        PlayAnim("Entity38_Attack", 0.08f);
        if (animator != null) animator.speed = 1.6f;

        // Play Close-up Attack Screech (53439420-flying-monster-screech-02) without cutting/overlapping existing playback
        PlayScreech(2.5f);

        // Sudden strike lunge forward
        Vector3 lungeDir = (playerTransform.position - transform.position);
        lungeDir.y = 0;
        lungeDir.Normalize();

        float lungeElapsed = 0f;
        float lungeDuration = 0.32f;

        while (lungeElapsed < lungeDuration)
        {
            Vector3 lungeMove = (lungeDir * lungeForce + new Vector3(0f, -2f, 0f)) * Time.deltaTime;
            controller.Move(lungeMove);

            if (transform.position.y < -10.0f)
            {
                transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            }

            lungeElapsed += Time.deltaTime;
            yield return null;
        }

        // Damage & Fatal Kill check
        // Check if our current target or ANY nearby player was struck
        Transform victim = playerTransform;
        float strikeReach = attackRange + (1.2f * Mathf.Max(1.0f, transform.localScale.y));
        float targetDist = (victim != null) ? Vector3.Distance(transform.position, victim.position) : 999f;

        // If primary target managed to evade, check if ANY other player was within claw reach!
        if (targetDist > strikeReach)
        {
            PlayerCharacter meleeHit = FindPlayerInMeleeRange();
            if (meleeHit != null)
            {
                victim = meleeHit.transform;
                targetDist = Vector3.Distance(transform.position, victim.position);
            }
        }

        if (victim != null && targetDist <= strikeReach)
        {
            playerTransform = victim;
            StartCoroutine(FatalKillSequenceRoutine(victim));
            yield break;
        }

        // Brief attack follow-through recovery
        currentState = AIState.Stunned;
        yield return new WaitForSeconds(0.4f);

        // Resume state: must re-verify proximity, Line of Sight, and spawn boundary
        currentAnimState = ""; // Reset so next state re-triggers animation smoothly
        if (manualControl)
        {
            PlayAnimLoop("Entity38_Idle", 0.2f);
        }
        else
        {
            if (playerTransform == null || !IsInsideCreatureRoom(playerTransform.position))
            {
                StartReturningToCenter();
            }
            else
            {
                float distAfterAttack = Vector3.Distance(transform.position, playerTransform.position);
                float maxRange = GetEffectiveDetectionRange(playerTransform);
                if (distAfterAttack <= maxRange && HasLineOfSightTo(playerTransform))
                {
                    currentState = AIState.Chasing;
                    lostSightTimer = 0f;
                }
                else
                {
                    PlayerCharacter altTarget = FindAgitatingPlayer(prioritizeMelee: true);
                    if (altTarget != null)
                    {
                        playerTransform = altTarget.transform;
                        currentState = AIState.Chasing;
                        lostSightTimer = 0f;
                        lastKnownPlayerPos = playerTransform.position;
                    }
                    else
                    {
                        StartReturningToCenter();
                    }
                }
            }
        }
    }

    private void ApplyPlayerImpact(Vector3 attackDir)
    {
        Debug.Log("[Entity38EnemyAI] Strike landed on Player!");
        if (playerTransform != null && !isPerformingFatalKill)
        {
            StartCoroutine(FatalKillSequenceRoutine(playerTransform));
        }
    }

    private IEnumerator FatalKillSequenceRoutine(Transform victim)
    {
        if (victim == null || isPerformingFatalKill) yield break;
        isPerformingFatalKill = true;
        currentState = AIState.Attacking;
        desiredHorizontalVelocity = Vector3.zero;

        Debug.Log("[Entity38EnemyAI] Fatal strike landed on " + victim.name + "! Victim captured - starting 4.5s fatal kill sequence.");

        // 1. Instantly stop victim's movement and lock mouse look
        FirstPersonController fpc = victim.GetComponent<FirstPersonController>();
        CharacterController victimCc = victim.GetComponent<CharacterController>();
        if (fpc != null)
        {
            fpc.CanMove = false;
            fpc.CanLook = false;
        }

        Transform camTrans = null;
        if (fpc != null && fpc.playerCamera != null)
        {
            camTrans = fpc.playerCamera;
        }
        else
        {
            Camera c = victim.GetComponentInChildren<Camera>();
            if (c != null) camTrans = c.transform;
            else if (Camera.main != null) camTrans = Camera.main.transform;
        }

        Vector3 originalCamLocalPos = (camTrans != null) ? camTrans.localPosition : new Vector3(0f, 1.6f, 0.1f);

        // 2. Maximize terror screen effect for victim
        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.SetPitTerror(true, 1.0f, victim);
        }

        // Play lethal attack screech audio
        PlayScreech(4.5f);

        // 3. 4.5-second cinematic fatal mauling sequence
        float killDuration = 4.5f;
        float elapsed = 0f;
        float reAttackTimer = 0f;

        float deathAnimDuration = 3.52f; // Anim_Death_Collapse
        bool deathAnimTriggered = false;

        while (elapsed < killDuration)
        {
            elapsed += Time.deltaTime;
            reAttackTimer += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / killDuration);

            // Play reverse wake-up collapse animation timed so it completes exactly when lethal blow lands
            if (!deathAnimTriggered && elapsed >= (killDuration - deathAnimDuration))
            {
                deathAnimTriggered = true;
                if (fpc != null)
                {
                    fpc.TriggerDeath();
                }
            }

            // Re-trigger claw attack strikes every 1.1s so creature is visibly mauling the victim
            if (reAttackTimer >= 1.1f)
            {
                reAttackTimer = 0f;
                PlayAnim("Entity38_Attack", 0.08f);
                if (animator != null) animator.speed = 1.4f;
            }

            // Creature aggressively faces victim
            FaceTarget(victim.position, rotationSpeed * 2.5f);

            // Camera lock-on & sight terror vibrations
            if (camTrans != null)
            {
                // Locked gaze directly at Monster 38's upper face / torso
                Vector3 monsterHeadPos = transform.position + Vector3.up * 1.85f;
                if (headBone != null)
                {
                    monsterHeadPos = headBone.position;
                }

                Vector3 toMonster = (monsterHeadPos - camTrans.position).normalized;
                if (toMonster.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toMonster);
                    camTrans.rotation = Quaternion.Slerp(camTrans.rotation, targetRot, Time.deltaTime * 14.0f);
                }

                // Escalating horror vibration tremors
                float tremorMagnitude = Mathf.Lerp(0.018f, 0.052f, t);
                float jx = (Mathf.PerlinNoise(Time.time * 55f, 0f) - 0.5f) * 2f * tremorMagnitude;
                float jy = (Mathf.PerlinNoise(0f, Time.time * 55f) - 0.5f) * 2f * tremorMagnitude;
                float jz = (Mathf.PerlinNoise(Time.time * 55f, Time.time * 55f) - 0.5f) * 2f * tremorMagnitude;
                camTrans.localPosition = originalCamLocalPos + new Vector3(jx, jy, jz);

                // Disorienting roll jitter
                float rollJitter = (Mathf.PerlinNoise(Time.time * 42f, 15f) - 0.5f) * Mathf.Lerp(2.2f, 5.5f, t);
                Vector3 angles = camTrans.rotation.eulerAngles;
                camTrans.rotation = Quaternion.Euler(angles.x, angles.y, rollJitter);
            }

            yield return null;
        }

        // 4. Lethal blow: Player dies
        Debug.Log("[Entity38EnemyAI] 4.5s elapsed. Victim executed by Entity 38! Applying lethal damage.");

        // Drop fuse cell at exact death position if player held it
        if (FuseCellPickup.PlayerHasFuseCell)
        {
            FuseCellPickup.DropAtPosition(victim.position);
        }

        PlayerCombatSystem combat = victim.GetComponent<PlayerCombatSystem>();
        if (combat != null)
        {
            combat.TakeDamage(99999f);
        }

        // 5. Respawn victim at safe checkpoint before Creature Room (Room 05 entrance)
        Vector3 respawnPos = new Vector3(101.5f, 0.2f, 25.5f);
        if (fpc != null && fpc.hasLastSafe && fpc.lastSafePosition.z >= 23.2f)
        {
            respawnPos = fpc.lastSafePosition;
        }
        respawnPos.y = Mathf.Max(respawnPos.y, 0.2f);

        if (victimCc != null) victimCc.enabled = false;
        victim.position = respawnPos;
        victim.rotation = Quaternion.Euler(0f, 90f, 0f);
        if (victimCc != null)
        {
            victimCc.enabled = true;
            victimCc.Move(Vector3.zero);
        }

        // Reset camera and controls
        if (camTrans != null)
        {
            camTrans.localPosition = originalCamLocalPos;
            camTrans.localRotation = Quaternion.identity;
        }
        if (fpc != null)
        {
            fpc.ResetLookAngles(0f, 90f);
            fpc.CanMove = true;
            fpc.CanLook = true;
            fpc.TriggerWakeUp();
        }

        if (combat != null)
        {
            combat.ResetHealth();
        }

        // Clear terror effect
        if (MonsterTerrorScreenEffect.Instance != null)
        {
            MonsterTerrorScreenEffect.Instance.ResetTerrorImmediately();
        }

        // 6. Reset creature back to Lurking state
        isPerformingFatalKill = false;
        currentState = AIState.Lurking;
        currentAnimState = "";
        PlayAnimLoop("Entity38_Idle", 0.25f);
        lastAttackTime = Time.time;
    }

    private void FaceTarget(Vector3 targetPos, float speed)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, speed * Time.deltaTime);
        }
    }

    void LateUpdate()
    {
        if (!enableHeadTracking || headBone == null || playerTransform == null) return;

        // Procedural head aim: Look down directly at player camera height (eye-to-eye horror stare)
        Vector3 targetLookPos = playerCam != null ? playerCam.transform.position : playerTransform.position + Vector3.up * 1.6f;
        Vector3 lookDir = targetLookPos - headBone.position;

        if (lookDir.sqrMagnitude > 0.1f)
        {
            Quaternion targetLookRot = Quaternion.LookRotation(lookDir, Vector3.up);
            headBone.rotation = Quaternion.Slerp(headBone.rotation, targetLookRot, lookAtWeight);

            // Add creepy organic twitch tremor
            if (twitchAngle != 0f)
            {
                headBone.Rotate(Vector3.forward, twitchAngle, Space.Self);
                twitchAngle = Mathf.Lerp(twitchAngle, 0f, Time.deltaTime * 8.0f);
            }
        }
    }

    public void UpdatePlayerPerceptionVisibility()
    {
        if (!Application.isPlaying) return;

        PlayerIdentity activeId = PlayerIdentity.Player1;
        Transform activePlayerTrans = playerTransform;

        if (MultiPlayerCharacterManager.Instance != null && MultiPlayerCharacterManager.Instance.ActivePlayer != null)
        {
            activeId = MultiPlayerCharacterManager.Instance.ActivePlayer.Identity;
            activePlayerTrans = MultiPlayerCharacterManager.Instance.ActivePlayer.transform;
        }
        else
        {
            activeId = PlayerIdentityManager.CurrentIdentity;
        }

        bool shouldBeVisible = false;

        // Player 3 possesses clairvoyant supernatural perception and can ALWAYS see the creature
        if (activeId == PlayerIdentity.Player3)
        {
            shouldBeVisible = true;
        }
        else
        {
            float distToActive = float.MaxValue;
            if (activePlayerTrans != null)
            {
                distToActive = Vector3.Distance(transform.position, activePlayerTrans.position);
            }

            bool inCloseRange = (distToActive <= closeRangeVisibilityDistance);
            bool isAggravated = (currentState == AIState.Chasing || currentState == AIState.Attacking);

            if (inCloseRange || isAggravated)
            {
                lastVisibleTriggerTime = Time.time;
                shouldBeVisible = true;
            }
            else if (Time.time - lastVisibleTriggerTime <= visibilityLingerDuration)
            {
                // Monster lingered in sight for a few seconds after unaggravated or out of range
                shouldBeVisible = true;
            }
            else
            {
                shouldBeVisible = false;
            }
        }

        SetMonsterVisible(shouldBeVisible);
    }

    public void SetMonsterVisible(bool visible)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (isCurrentlyVisibleToActivePlayer != visible || Time.frameCount % 30 == 0)
        {
            isCurrentlyVisibleToActivePlayer = visible;
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null && cachedRenderers[i].enabled != visible)
                {
                    cachedRenderers[i].enabled = visible;
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visual debugging in Unity Scene View
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, agitationRange);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, crouchAgitationRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Cyan & Magenta territory boundary lines: Area 5 confinement
        Gizmos.color = Color.cyan;
        // North boundary (passage to Room 4)
        Gizmos.DrawLine(new Vector3(spawnBoundaryMinX, 0f, maxSpawnDirectionZ), new Vector3(maxRoomX, 0f, maxSpawnDirectionZ));
        Gizmos.DrawLine(new Vector3(spawnBoundaryMinX, 3.5f, maxSpawnDirectionZ), new Vector3(maxRoomX, 3.5f, maxSpawnDirectionZ));
        // West boundary (hallway to Fuse Cell Room)
        Gizmos.color = new Color(1.0f, 0.2f, 0.4f, 0.85f);
        Gizmos.DrawLine(new Vector3(minRoomX, 0f, minRoomZ), new Vector3(minRoomX, 0f, maxRoomZ));
        Gizmos.DrawLine(new Vector3(minRoomX, 3.5f, minRoomZ), new Vector3(minRoomX, 3.5f, maxRoomZ));
        // Room Center home position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(roomCenterPosition, centerArrivalRadius);
    }

    void OnGUI()
    {
        if (!showDebugGUI) return;

        float panelW = 290f;
        float panelH = 260f;
        Rect areaRect = new Rect(Screen.width - panelW - 20f, 20f, panelW, panelH);
        GUILayout.BeginArea(areaRect, "ENTITY 38 CONTROLLER [F10]", GUI.skin.window);
        GUILayout.Label($"Mode: {(manualControl ? "MANUAL LOOP PREVIEW" : "ACTIVE AI PURSUIT")}");
        GUILayout.Label($"Loop In-Place: {(loopInPlace ? "YES (Stationary)" : "NO (Moving Forward)")}");
        GUILayout.Label($"Active Clip: {(string.IsNullOrEmpty(currentAnimState) ? "Entity38_Idle" : currentAnimState)}");
        GUILayout.Space(6);

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("[1] Loop Idle (Breathing)", GUILayout.Height(28)))
        {
            PlayAnimLoop("Entity38_Idle", 0.15f);
        }

        GUI.backgroundColor = new Color(0.4f, 0.75f, 1.0f);
        if (GUILayout.Button("[2] Loop Walk (Creepy Stalk)", GUILayout.Height(28)))
        {
            PlayAnimLoop("Entity38_Walk", 0.15f);
        }

        GUI.backgroundColor = new Color(1.0f, 0.65f, 0.25f);
        if (GUILayout.Button("[3] Loop Chase (Predatory Sprint)", GUILayout.Height(28)))
        {
            PlayAnimLoop("Entity38_Chase", 0.15f);
        }

        GUI.backgroundColor = new Color(1.0f, 0.35f, 0.35f);
        if (GUILayout.Button("[4] Play Attack (Windup & Slash)", GUILayout.Height(28)))
        {
            TriggerAttackExternal();
        }

        GUI.backgroundColor = Color.white;
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(loopInPlace ? "Mode: In-Place" : "Mode: Moving", GUILayout.Height(24)))
        {
            loopInPlace = !loopInPlace;
        }
        if (GUILayout.Button("Release Mouse (Tab)", GUILayout.Height(24)))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
