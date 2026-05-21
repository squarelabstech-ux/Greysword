using UnityEngine;

/// <summary>
/// AdaptiveDifficultyManager — Multi-signal weighted AI decision system.
/// Antigravity brain component #2: infers player state, decides difficulty change, validates it.
///
/// INFERENCE LABELS:
/// - Struggling   → reduce pressure
/// - Beginner     → gentle ramp
/// - Balanced     → keep current
/// - Skilled      → increase slightly
/// - Bored        → add challenge
/// - Frustrated   → reduce difficulty + increase drops
/// - Kiter        → add flanker behavior
///
/// SETUP:
/// - Attach to GameDirector GameObject alongside AntigravityGameDirector.
/// </summary>
public class AdaptiveDifficultyManager : MonoBehaviour
{
    // ─── Singleton ─────────────────────────────────────────────────────────────
    public static AdaptiveDifficultyManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Header("Thresholds (tuned for hackathon demo)")]
    [Tooltip("Kills per minute considered 'fast killer'")]
    public float fastKillRate = 3.0f;

    [Tooltip("Damage per minute considered 'too much damage'")]
    public float highDamageRate = 40f;

    [Tooltip("Health fraction below which player is struggling")]
    [Range(0f, 1f)]
    public float lowHealthThreshold = 0.4f;

    [Tooltip("Max deaths before frustration kicks in")]
    public int frustrationDeathCount = 3;

    [Tooltip("Max players alive zombies when rejecting difficulty spikes")]
    public int absoluteMaxZombies = 14;

    [Tooltip("Max damage multiplier allowed")]
    public float maxDamageMultiplier = 2.0f;

    [Tooltip("Max speed multiplier allowed (escape threshold)")]
    public float maxSpeedMultiplier = 1.8f;

    // ─── Player Health Reference ────────────────────────────────────────────────

    private PlayerHealth playerHealth;

    void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerHealth = p.GetComponent<PlayerHealth>();
    }

    // ─── Main Evaluation ───────────────────────────────────────────────────────

    /// <summary>
    /// Core AI decision function. Takes a behavior snapshot, infers player state,
    /// proposes a difficulty profile, validates it, and returns safe profile + log data.
    /// </summary>
    public (DifficultyProfile profile, string label, string observation, string decision, string action, bool rejected, string rejectReason)
        Evaluate(BehaviorSnapshot snap, DifficultyProfile current)
    {
        // ── Step 1: Compute Signal Scores ──────────────────────────────────────

        // skillScore: high if player kills quickly, survives long, takes little damage
        float killScore    = Mathf.Clamp01(snap.killsPerMinute / fastKillRate);
        float surviveScore = Mathf.Clamp01(snap.timeSurvived / 300f);  // 5 min = max
        float resistScore  = Mathf.Clamp01(1f - (snap.damagePerMinute / highDamageRate));
        float healthScore  = snap.avgHealthAfterFight;

        float skillScore   = (killScore * 0.4f) + (resistScore * 0.3f) + (surviveScore * 0.2f) + (healthScore * 0.1f);

        // frustrationScore: high if many deaths, high damage taken
        float deathScore = Mathf.Clamp01((float)snap.deathCount / frustrationDeathCount);
        float damageScore = Mathf.Clamp01(snap.damagePerMinute / highDamageRate);
        float frustrationScore = (deathScore * 0.6f) + (damageScore * 0.4f);

        // boredomScore: high if killing fast, staying healthy
        float boredomScore = (killScore * 0.5f) + (healthScore * 0.3f) + (surviveScore * 0.2f);

        // kitingScore: high if player keeps distance
        float kitingScore = snap.isKiting ? 1f : Mathf.Clamp01(snap.avgDistFromZombies / 15f);

        // ── Step 2: Infer Label ────────────────────────────────────────────────

        string label;
        if (frustrationScore > 0.7f)
            label = "Frustrated";
        else if (skillScore < 0.25f && frustrationScore > 0.5f)
            label = "Struggling";
        else if (skillScore < 0.35f)
            label = "Beginner";
        else if (kitingScore > 0.7f)
            label = "Kiter";
        else if (skillScore > 0.65f && boredomScore > 0.6f)
            label = "Bored";
        else if (skillScore > 0.65f)
            label = "Skilled";
        else
            label = "Balanced";

        // ── Step 3: Build Observation String ──────────────────────────────────

        string observation = $"Kills/min={snap.killsPerMinute:F1} " +
                             $"Dmg/min={snap.damagePerMinute:F0} " +
                             $"AvgHealth={snap.avgHealthAfterFight*100:F0}% " +
                             $"Deaths={snap.deathCount} " +
                             $"Survived={snap.timeSurvived:F0}s " +
                             $"Kiting={snap.isKiting}";

        // ── Step 4: Decide Profile Change ─────────────────────────────────────

        DifficultyProfile proposed = current.Clone();
        string decision;
        string action;

        switch (label)
        {
            case "Frustrated":
                proposed.zombieDamageMultiplier = Mathf.Max(0.6f, current.zombieDamageMultiplier - 0.15f);
                proposed.maxAliveZombies        = Mathf.Max(2, current.maxAliveZombies - 1);
                proposed.spawnInterval          = Mathf.Min(25f, current.spawnInterval + 5f);
                proposed.foodDropChance         = Mathf.Min(0.5f, current.foodDropChance + 0.1f);
                decision = "Reduce difficulty — player is frustrated.";
                action   = $"Damage×{proposed.zombieDamageMultiplier:F2}, MaxAlive-1={proposed.maxAliveZombies}, FoodDrop+={proposed.foodDropChance:F2}";
                break;

            case "Struggling":
                proposed.zombieDamageMultiplier = Mathf.Max(0.5f, current.zombieDamageMultiplier - 0.1f);
                proposed.zombieSpeedMultiplier  = Mathf.Max(0.6f, current.zombieSpeedMultiplier - 0.1f);
                proposed.foodDropChance         = Mathf.Min(0.45f, current.foodDropChance + 0.08f);
                decision = "Ease off — player is struggling.";
                action   = $"Speed×{proposed.zombieSpeedMultiplier:F2}, Damage×{proposed.zombieDamageMultiplier:F2}, FoodDrop={proposed.foodDropChance:F2}";
                break;

            case "Beginner":
                // Very gentle ramp — keep similar but slightly increase
                proposed.maxAliveZombies = Mathf.Min(absoluteMaxZombies, current.maxAliveZombies + 1);
                decision = "Gentle ramp — beginner player showing some skill.";
                action   = $"MaxAlive+1={proposed.maxAliveZombies}";
                break;

            case "Kiter":
                // Flanker logic — increase detection range so zombies search harder
                proposed.zombieDetectionRange = Mathf.Min(22f, current.zombieDetectionRange + 2f);
                proposed.zombieChaseRange     = Mathf.Min(30f, current.zombieChaseRange + 3f);
                decision = "Counter kiting — increase search range to challenge ranged strategy.";
                action   = $"DetectionRange={proposed.zombieDetectionRange:F0}, ChaseRange={proposed.zombieChaseRange:F0}";
                break;

            case "Bored":
                proposed.zombieSpeedMultiplier    = Mathf.Min(maxSpeedMultiplier, current.zombieSpeedMultiplier + 0.08f);
                proposed.maxAliveZombies          = Mathf.Min(absoluteMaxZombies, current.maxAliveZombies + 2);
                proposed.specialZombieChance      = Mathf.Min(0.5f, current.specialZombieChance + 0.1f);
                proposed.spawnInterval            = Mathf.Max(5f, current.spawnInterval - 3f);
                decision = "Increase engagement — player seems bored.";
                action   = $"Speed×{proposed.zombieSpeedMultiplier:F2}, MaxAlive+2={proposed.maxAliveZombies}, Special+={proposed.specialZombieChance:F2}, Interval-={proposed.spawnInterval:F0}s";
                break;

            case "Skilled":
                proposed.zombieHealthMultiplier   = Mathf.Min(2.5f, current.zombieHealthMultiplier + 0.1f);
                proposed.maxAliveZombies          = Mathf.Min(absoluteMaxZombies, current.maxAliveZombies + 1);
                decision = "Increase challenge — skilled player.";
                action   = $"HP×{proposed.zombieHealthMultiplier:F2}, MaxAlive+1={proposed.maxAliveZombies}";
                break;

            default: // Balanced
                decision = "No change — player is balanced.";
                action   = "No parameters changed.";
                break;
        }

        // ── Step 5: Quality-Control Validation ────────────────────────────────

        string rejectReason = string.Empty;
        bool   rejected     = false;

        // QC Rule 1: Zombie count too high
        if (proposed.maxAliveZombies > absoluteMaxZombies)
        {
            rejectReason = $"MaxAlive {proposed.maxAliveZombies} exceeds absolute max {absoluteMaxZombies}.";
            proposed.maxAliveZombies = absoluteMaxZombies;
            rejected = true;
        }

        // QC Rule 2: Damage too high per hit
        float playerMaxHP = playerHealth != null ? playerHealth.maxHealth : 100f;
        if (proposed.zombieDamageMultiplier > maxDamageMultiplier)
        {
            rejectReason += $" DamageMult {proposed.zombieDamageMultiplier:F2} exceeds max {maxDamageMultiplier:F2}.";
            proposed.zombieDamageMultiplier = maxDamageMultiplier;
            rejected = true;
        }

        // QC Rule 3: Speed too high (impossible to escape on mobile)
        if (proposed.zombieSpeedMultiplier > maxSpeedMultiplier)
        {
            rejectReason += $" SpeedMult {proposed.zombieSpeedMultiplier:F2} exceeds max {maxSpeedMultiplier:F2}.";
            proposed.zombieSpeedMultiplier = maxSpeedMultiplier;
            rejected = true;
        }

        // QC Rule 4: Player has low health — do NOT increase difficulty
        if (playerHealth != null && playerHealth.HealthFraction < 0.3f)
        {
            if (proposed.maxAliveZombies > current.maxAliveZombies ||
                proposed.zombieDamageMultiplier > current.zombieDamageMultiplier ||
                proposed.zombieSpeedMultiplier > current.zombieSpeedMultiplier)
            {
                rejectReason += " Player health below 30% — difficulty increase rejected.";
                proposed = current.Clone(); // revert to current
                rejected = true;
            }
        }

        if (rejected)
        {
            Debug.LogWarning($"[AdaptiveDifficultyManager] QC REJECTION: {rejectReason}");
            action += $" | QC REJECTED ({rejectReason}) — applied safe values.";
        }

        return (proposed, label, observation, decision, action, rejected, rejectReason);
    }
}
