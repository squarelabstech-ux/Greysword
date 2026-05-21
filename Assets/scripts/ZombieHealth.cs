using UnityEngine;

/// <summary>
/// ZombieHealth — Manages zombie HP, death, and notifies the behavior tracker.
///
/// SETUP:
/// - Attach to same root GameObject as ZombieAI.
/// - Assign maxHealth in Inspector (default 100).
/// - Player combat script hits TakeDamage() on this component.
/// </summary>
public class ZombieHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [Tooltip("Maximum health points")]
    public float maxHealth = 100f;

    [Tooltip("Show health bar in world space (optional)")]
    public bool showHealthBar = false;

    // ─── Properties ────────────────────────────────────────────────────────────

    public float CurrentHealth { get; private set; }
    public bool  IsDead        { get; private set; }

    // ─── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired when this zombie dies. Used by spawn manager to track active count.</summary>
    public static event System.Action<ZombieHealth> OnZombieDied;

    // ─── References ────────────────────────────────────────────────────────────

    private ZombieAI zombieAI;

    // Runtime multiplier applied by AdaptiveDifficultyManager
    [HideInInspector] public float runtimeHealthMultiplier = 1f;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        zombieAI = GetComponent<ZombieAI>();
    }

    void Start()
    {
        CurrentHealth = maxHealth * runtimeHealthMultiplier;
        IsDead        = false;
    }

    /// <summary>Call this to reinitialize when recycled from object pool.</summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth * runtimeHealthMultiplier;
        IsDead        = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Deals damage to this zombie. Safe to call multiple times.</summary>
    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        CurrentHealth -= damage;
        CurrentHealth  = Mathf.Max(0f, CurrentHealth);

        Debug.Log($"[ZombieHealth] {gameObject.name} took {damage} damage. HP: {CurrentHealth}/{maxHealth * runtimeHealthMultiplier}");

        if (CurrentHealth <= 0f)
        {
            Die();
        }
        else if (zombieAI != null)
        {
            // Apply hit stun and play damage animation
            zombieAI.hitStunTimer = 0.5f;
            if (zombieAI.animator != null)
                zombieAI.animator.CrossFadeInFixedTime("Damage", 0.05f);
        }
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log($"[ZombieHealth] {gameObject.name} died.");
        if (AgenticDecisionLogger.Instance != null)
            AgenticDecisionLogger.Instance.LogRealTimeEvent("Zombie Killed", $"Zombie {gameObject.name} eliminated.");

        // Stop the zombie from moving or thinking
        if (zombieAI != null)
        {
            zombieAI.OnZombieDied();
        }

        // Notify behavior tracker (for kill count)
        PlayerBehaviorTracker tracker = FindObjectOfType<PlayerBehaviorTracker>();
        if (tracker != null)
            tracker.OnZombieKilled();

        // Notify spawn manager so it can track active zombie count
        OnZombieDied?.Invoke(this);

        // Auto-count skull directly to inventory
        if (SkullCounter.Instance != null)
            SkullCounter.Instance.AddSkull();

        // Disable collider so player doesn't walk into a dead body
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Deactivate after 6 seconds (lets death animation finish and body sit on ground briefly)
        Invoke(nameof(Deactivate), 6f);
    }

    void Deactivate()
    {
        gameObject.SetActive(false);
    }

    // ─── Runtime difficulty API ─────────────────────────────────────────────────

    /// <summary>Called by AdaptiveDifficultyManager when difficulty changes.</summary>
    public void ApplyHealthMultiplier(float multiplier)
    {
        float ratio        = CurrentHealth / (maxHealth * runtimeHealthMultiplier);
        runtimeHealthMultiplier = multiplier;
        CurrentHealth      = maxHealth * runtimeHealthMultiplier * ratio; // preserve ratio
    }
}
