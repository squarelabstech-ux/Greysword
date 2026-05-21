using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerBehaviorTracker — Collects real-time gameplay signals.
/// Provides computed metrics to the AdaptiveDifficultyManager.
///
/// SETUP:
/// - Attach to GameDirector GameObject.
/// - All other systems call into this via FindObjectOfType or singleton.
/// </summary>
public class PlayerBehaviorTracker : MonoBehaviour
{
    // ─── Raw Counters ──────────────────────────────────────────────────────────

    [Header("Live Metrics (Read-Only — Inspector Debug View)")]
    [SerializeField] private int   zombiesKilled   = 0;
    [SerializeField] private int   deathCount      = 0;
    [SerializeField] private float totalDamageTaken = 0f;
    [SerializeField] private float timeSurvived    = 0f;
    [SerializeField] private int   totalAttacks    = 0;
    [SerializeField] private int   attacksHit      = 0;

    // Rolling health snapshot after each fight
    private List<float> healthAfterFights = new List<float>();

    // Distance tracking for kite detection
    [SerializeField] private float avgDistanceFromZombies = 0f;
    private float   distanceSampleTimer = 0f;
    private const float DIST_SAMPLE_RATE = 2f;
    private Transform playerTransform;

    // Session tracking
    private float sessionStartTime;
    private bool  playerDead = false;

    // ─── Singleton ─────────────────────────────────────────────────────────────

    public static PlayerBehaviorTracker Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; } // Destroy COMPONENT only, not the whole GameObject!
        Instance = this;
    }

    void Start()
    {
        sessionStartTime = Time.time;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerTransform = p.transform;

        PlayerHealth.OnPlayerDeath += () => playerDead = true;
    }

    void Update()
    {
        if (!playerDead)
            timeSurvived = Time.time - sessionStartTime;

        // Sample distance to nearest zombie every DIST_SAMPLE_RATE seconds
        distanceSampleTimer -= Time.deltaTime;
        if (distanceSampleTimer <= 0f)
        {
            distanceSampleTimer = DIST_SAMPLE_RATE;
            SampleZombieDistance();
        }
    }

    void SampleZombieDistance()
    {
        if (playerTransform == null) return;

        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        if (zombies.Length == 0) return;

        float minDist = float.MaxValue;
        foreach (ZombieAI z in zombies)
        {
            if (!z.gameObject.activeSelf) continue;
            float d = Vector3.Distance(playerTransform.position, z.transform.position);
            if (d < minDist) minDist = d;
        }

        // Smooth rolling average
        avgDistanceFromZombies = Mathf.Lerp(avgDistanceFromZombies, minDist, 0.2f);
    }

    // ─── Event Receivers (called by other scripts) ─────────────────────────────

    public void OnZombieKilled()
    {
        zombiesKilled++;
    }

    public void OnPlayerDamageTaken(float damage)
    {
        totalDamageTaken += damage;
    }

    public void OnPlayerDied()
    {
        deathCount++;
        playerDead = true;
    }

    public void OnPlayerAttack(bool hit)
    {
        totalAttacks++;
        if (hit) attacksHit++;
    }

    /// <summary>Record health after a fight ends (e.g., after all nearby zombies are dead).</summary>
    public void RecordPostFightHealth(float healthFraction)
    {
        healthAfterFights.Add(healthFraction);
        if (healthAfterFights.Count > 10)
            healthAfterFights.RemoveAt(0); // keep last 10
    }

    // ─── Computed Properties ───────────────────────────────────────────────────

    /// <summary>Kills per minute.</summary>
    public float KillsPerMinute => timeSurvived > 0f ? (zombiesKilled / timeSurvived) * 60f : 0f;

    /// <summary>Damage taken per minute.</summary>
    public float DamagePerMinute => timeSurvived > 0f ? (totalDamageTaken / timeSurvived) * 60f : 0f;

    /// <summary>Average health fraction after fights (0–1). Returns 1 if no fights recorded.</summary>
    public float AverageHealthAfterFight
    {
        get
        {
            if (healthAfterFights.Count == 0) return 1f;
            float sum = 0f;
            foreach (float h in healthAfterFights) sum += h;
            return sum / healthAfterFights.Count;
        }
    }

    /// <summary>Hit accuracy (0–1). Returns 0.5 if no attacks made.</summary>
    public float Accuracy => totalAttacks > 0 ? (float)attacksHit / totalAttacks : 0.5f;

    /// <summary>True if player consistently keeps distance from zombies (kiting strategy).</summary>
    public bool IsKiting => avgDistanceFromZombies > 10f;

    public int   DeathCount     => deathCount;
    public int   ZombiesKilled  => zombiesKilled;
    public float TimeSurvived   => timeSurvived;
    public float TotalDamage    => totalDamageTaken;
    public float AvgDistFromZombies => avgDistanceFromZombies;

    // ─── Snapshot ──────────────────────────────────────────────────────────────

    /// <summary>Returns a data snapshot for the AdaptiveDifficultyManager to evaluate.</summary>
    public BehaviorSnapshot GetSnapshot()
    {
        return new BehaviorSnapshot
        {
            killsPerMinute         = KillsPerMinute,
            damagePerMinute        = DamagePerMinute,
            avgHealthAfterFight    = AverageHealthAfterFight,
            accuracy               = Accuracy,
            deathCount             = deathCount,
            timeSurvived           = timeSurvived,
            isKiting               = IsKiting,
            avgDistFromZombies     = avgDistanceFromZombies
        };
    }
}

/// <summary>Snapshot struct passed to the difficulty evaluator.</summary>
[System.Serializable]
public struct BehaviorSnapshot
{
    public float killsPerMinute;
    public float damagePerMinute;
    public float avgHealthAfterFight;
    public float accuracy;
    public int   deathCount;
    public float timeSurvived;
    public bool  isKiting;
    public float avgDistFromZombies;
}
