using UnityEngine;

/// <summary>
/// PlayerHealth — Manages player HP and fires death events.
///
/// SETUP:
/// - Attach to the Player root GameObject (same as PlayerMovement).
/// - Set maxHealth in Inspector (default 100).
/// - Remove the Rigidbody component from Player — it conflicts with CharacterController!
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [Tooltip("Maximum health points")]
    public float maxHealth = 100f;

    // ─── Properties ────────────────────────────────────────────────────────────

    public float CurrentHealth { get; private set; }

    /// <summary>True once player HP reaches 0. All zombies listen to this.</summary>
    public bool IsDead { get; private set; }

    // ─── Events ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired exactly once when player dies.
    /// All ZombieAI instances subscribe to this in Start() and unsubscribe in OnDestroy().
    /// ZombieSpawnManager also listens to stop spawning.
    /// </summary>
    public static event System.Action OnPlayerDeath;

    /// <summary>Fired whenever health changes (for UI health bar updates).</summary>
    public static event System.Action<float, float> OnHealthChanged; // (current, max)

    // ─── References ────────────────────────────────────────────────────────────

    private PlayerMovement playerMovement;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Start()
    {
        CurrentHealth = maxHealth;
        IsDead        = false;
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by ZombieAI to deal damage. Safe to call after death.</summary>
    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        CurrentHealth -= damage;
        CurrentHealth  = Mathf.Clamp(CurrentHealth, 0f, maxHealth);

        Debug.Log($"[PlayerHealth] Took {damage} damage. HP: {CurrentHealth}/{maxHealth}");
        if (AgenticDecisionLogger.Instance != null)
            AgenticDecisionLogger.Instance.LogRealTimeEvent("Player Took Damage", $"Health decreased to {CurrentHealth}/{maxHealth}");

        // Notify UI
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        // Notify behavior tracker
        PlayerBehaviorTracker tracker = FindObjectOfType<PlayerBehaviorTracker>();
        if (tracker != null)
            tracker.OnPlayerDamageTaken(damage);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
        else if (playerMovement != null && playerMovement.animator != null)
        {
            // Play damage animation if still alive
            playerMovement.animator.SetTrigger("Damage");
        }
    }

    /// <summary>Heal the player (e.g., from food pickup).</summary>
    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"[PlayerHealth] Healed {amount}. HP: {CurrentHealth}/{maxHealth}");
        if (AgenticDecisionLogger.Instance != null)
            AgenticDecisionLogger.Instance.LogRealTimeEvent("Player Healed", $"Health increased to {CurrentHealth}/{maxHealth}");
    }
    
    /// <summary>Set health to a specific large value (e.g., from Health Bounty).</summary>
    public void Overheal(float amount)
    {
        if (IsDead) return;
        maxHealth = amount;
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        Debug.Log($"[PlayerHealth] Overhealed to {amount}!");
    }

    /// <summary>Health as 0–1 fraction, useful for UI.</summary>
    public float HealthFraction => CurrentHealth / maxHealth;

    // ─── Private ────────────────────────────────────────────────────────────────

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log("[PlayerHealth] Player died.");
        if (AgenticDecisionLogger.Instance != null)
            AgenticDecisionLogger.Instance.LogRealTimeEvent("Player Died", "Game Over triggered.");

        // Play death animation
        if (playerMovement != null && playerMovement.animator != null)
            playerMovement.animator.SetTrigger("Dead");

        // Disable movement so player stops sliding
        if (playerMovement != null)
            playerMovement.enabled = false;

        // Notify all zombies and systems
        OnPlayerDeath?.Invoke();

        // Notify behavior tracker
        PlayerBehaviorTracker tracker = FindObjectOfType<PlayerBehaviorTracker>();
        if (tracker != null)
            tracker.OnPlayerDied();

        // TODO: Trigger Game Over UI here
        // GameOverUI.Show();
    }

#if UNITY_EDITOR
    /// <summary>Editor helper — press 'K' in play mode to test kill the player instantly.</summary>
    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.kKey.wasPressedThisFrame && !IsDead)
            {
                Debug.Log("[PlayerHealth] DEBUG: Force kill triggered.");
                TakeDamage(maxHealth);
            }

            if (UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame && !IsDead)
            {
                Debug.Log("[PlayerHealth] DEBUG: Force heal triggered.");
                Heal(maxHealth);
            }
        }
    }
#endif
}