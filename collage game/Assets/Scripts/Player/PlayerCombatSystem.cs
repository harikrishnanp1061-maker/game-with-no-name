using System;
using UnityEngine;

// =========================================================================
// PlayerCombatSystem - tracks player health, damage, and death.
//
// Pure state and signalling only - no rendering, no input. The HUD and the
// death effect both subscribe to the events so they stay in sync no matter
// which way they're wired in the scene.
//
// Other scripts call TakeDamage() to hurt the player. Call ResetHealth()
// (typically from a respawn handler) to retry a run.
// =========================================================================
[DisallowMultipleComponent]
public class PlayerCombatSystem : MonoBehaviour
{
    // ---------------------------------------------------------------------
    // CONFIG
    // ---------------------------------------------------------------------
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth = 100f;
    [SerializeField] private bool _isInvulnerable = false;
    [SerializeField] private bool _isDead = false;

    // ---------------------------------------------------------------------
    // EVENTS - subscribers update visuals without polling.
    // ---------------------------------------------------------------------
    public event Action<float, float> OnHealthChanged;
    public event Action OnPlayerDied;

    // ---------------------------------------------------------------------
    // ACCESSORS
    // ---------------------------------------------------------------------
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _isDead;
    public bool IsInvulnerable => _isInvulnerable;

    public void SetInvulnerable(bool invulnerable)
    {
        _isInvulnerable = invulnerable;
    }

    // ---------------------------------------------------------------------
    // DAMAGE
    // ---------------------------------------------------------------------
    /// <summary>
    /// Applies damage. Returns true if the player died as a result.
    /// Invulnerable or already-dead players take no damage.
    /// </summary>
    public bool TakeDamage(float amount)
    {
        if (amount <= 0f || _isDead || _isInvulnerable) return _isDead;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
        {
            _isDead = true;
            OnPlayerDied?.Invoke();
            return true;
        }
        return false;
    }

    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    // ---------------------------------------------------------------------
    // RESET
    // ---------------------------------------------------------------------
    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
        _isDead = false;
        _isInvulnerable = false;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void OnEnable()
    {
        if (_currentHealth > _maxHealth) _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void Reset()
    {
        _currentHealth = _maxHealth;
        _isDead = false;
    }
}
