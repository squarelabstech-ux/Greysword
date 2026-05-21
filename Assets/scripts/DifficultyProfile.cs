using UnityEngine;

/// <summary>
/// DifficultyProfile — Serializable data class representing all difficulty parameters.
/// Used by AdaptiveDifficultyManager to pass changes to zombie and spawn systems.
///
/// No MonoBehaviour — pure data class.
/// </summary>
[System.Serializable]
public class DifficultyProfile
{
    [Tooltip("Zombie movement speed multiplier (1.0 = base speed)")]
    [Range(0.5f, 3.0f)]
    public float zombieSpeedMultiplier = 1.0f;

    [Tooltip("Zombie HP multiplier (1.0 = base HP)")]
    [Range(0.5f, 3.0f)]
    public float zombieHealthMultiplier = 1.0f;

    [Tooltip("Zombie damage multiplier (1.0 = base damage)")]
    [Range(0.5f, 3.0f)]
    public float zombieDamageMultiplier = 1.0f;

    [Tooltip("Zombie detection range in world units")]
    [Range(6f, 25f)]
    public float zombieDetectionRange = 12f;

    [Tooltip("Zombie chase range in world units")]
    [Range(10f, 35f)]
    public float zombieChaseRange = 18f;

    [Tooltip("Zombie attack cooldown in seconds")]
    [Range(0.5f, 4.0f)]
    public float zombieAttackCooldown = 1.5f;

    [Tooltip("How many zombies can be alive at once")]
    [Range(1, 20)]
    public int maxAliveZombies = 6;

    [Tooltip("Seconds between spawn attempts")]
    [Range(5f, 60f)]
    public float spawnInterval = 15f;

    [Tooltip("Chance (0–1) of a special/fast zombie spawning")]
    [Range(0f, 1f)]
    public float specialZombieChance = 0f;

    [Tooltip("Chance (0–1) that a killed zombie drops food/healing")]
    [Range(0f, 1f)]
    public float foodDropChance = 0.15f;

    // ─── Preset Profiles ───────────────────────────────────────────────────────

    public static DifficultyProfile Easy()
    {
        return new DifficultyProfile
        {
            zombieSpeedMultiplier  = 0.7f,
            zombieHealthMultiplier = 0.7f,
            zombieDamageMultiplier = 0.7f,
            zombieDetectionRange   = 10f,
            zombieChaseRange       = 14f,
            zombieAttackCooldown   = 2.0f,
            maxAliveZombies        = 3,
            spawnInterval          = 20f,
            specialZombieChance    = 0f,
            foodDropChance         = 0.35f
        };
    }

    public static DifficultyProfile Normal()
    {
        return new DifficultyProfile
        {
            zombieSpeedMultiplier  = 1.0f,
            zombieHealthMultiplier = 1.0f,
            zombieDamageMultiplier = 1.0f,
            zombieDetectionRange   = 12f,
            zombieChaseRange       = 18f,
            zombieAttackCooldown   = 1.5f,
            maxAliveZombies        = 6,
            spawnInterval          = 15f,
            specialZombieChance    = 0.1f,
            foodDropChance         = 0.15f
        };
    }

    public static DifficultyProfile Hard()
    {
        return new DifficultyProfile
        {
            zombieSpeedMultiplier  = 1.4f,
            zombieHealthMultiplier = 1.5f,
            zombieDamageMultiplier = 1.3f,
            zombieDetectionRange   = 16f,
            zombieChaseRange       = 24f,
            zombieAttackCooldown   = 1.0f,
            maxAliveZombies        = 10,
            spawnInterval          = 8f,
            specialZombieChance    = 0.25f,
            foodDropChance         = 0.08f
        };
    }

    public static DifficultyProfile Bored()
    {
        return new DifficultyProfile
        {
            zombieSpeedMultiplier  = 1.6f,
            zombieHealthMultiplier = 1.8f,
            zombieDamageMultiplier = 1.2f,
            zombieDetectionRange   = 18f,
            zombieChaseRange       = 28f,
            zombieAttackCooldown   = 0.9f,
            maxAliveZombies        = 12,
            spawnInterval          = 6f,
            specialZombieChance    = 0.4f,
            foodDropChance         = 0.1f
        };
    }

    /// <summary>Returns a deep copy of this profile.</summary>
    public DifficultyProfile Clone()
    {
        return (DifficultyProfile)this.MemberwiseClone();
    }

    public override string ToString()
    {
        return $"Speed×{zombieSpeedMultiplier:F2} HP×{zombieHealthMultiplier:F2} " +
               $"Dmg×{zombieDamageMultiplier:F2} MaxAlive:{maxAliveZombies} " +
               $"Interval:{spawnInterval:F1}s Special:{specialZombieChance*100:F0}%";
    }
}
