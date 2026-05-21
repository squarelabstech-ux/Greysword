using UnityEngine;

/// <summary>
/// BossSummonManager — Spawns the boss and grants buff on boss death.
///
/// SETUP:
/// - Attach to a BossSummonManager empty GameObject (or same as RitualManager).
/// - Assign bossPrefab (placeholder: Capsule with ZombieHealth, ZombieAI, high HP).
/// - Assign bossSpawnPoint Transform — a position near the ritual area.
/// - Buff types: HealthBoost, DamageBoost, SpeedBoost, Regeneration.
/// </summary>
public class BossSummonManager : MonoBehaviour
{
    [Header("Boss")]
    [Tooltip("Boss prefab — use a high-HP ZombieHealth + ZombieAI GameObject")]
    public GameObject bossPrefab;

    [Tooltip("Where to spawn the boss")]
    public Transform bossSpawnPoint;

    [Header("Buff Settings")]
    public BossBuffType buffType = BossBuffType.HealthBoost;

    [Tooltip("Amount of the buff to apply")]
    public float buffAmount = 25f;

    // ─── State ─────────────────────────────────────────────────────────────────

    private bool bossAlive    = false;
    private GameObject bossGO = null;

    public bool IsBossAlive => bossAlive;

    // ─── Events ─────────────────────────────────────────────────────────────────

    public static event System.Action OnBossDefeated;

    // ─── Public API ────────────────────────────────────────────────────────────

    void Start()
    {
        GameObject existingBoss = FindBossInScene();
        if (existingBoss != null)
        {
            bossGO = existingBoss;
            bossGO.SetActive(false);
            Debug.Log("[BossSummonManager] Located pre-placed Monster10 and deactivated it at startup.");
        }
    }

    public void SummonBoss()
    {
        if (bossAlive) return;

        // 1. Find pre-placed Monster10 in scene
        GameObject existingBoss = FindBossInScene();
        if (existingBoss != null)
        {
            bossGO = existingBoss;
            bossGO.SetActive(true);
            
            // Warp/move to spawn position if set
            if (bossSpawnPoint != null)
            {
                UnityEngine.AI.NavMeshAgent agent = bossGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.Warp(bossSpawnPoint.position);
                else bossGO.transform.position = bossSpawnPoint.position;
            }
            Debug.Log("[BossSummonManager] Found and activated pre-placed Monster10 in scene.");
        }
        else if (bossPrefab != null)
        {
            Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position;
            bossGO = Instantiate(bossPrefab, spawnPos, Quaternion.identity);
            Debug.Log("[BossSummonManager] Instantiating boss prefab.");
        }
        else
        {
            Debug.LogWarning("[BossSummonManager] No pre-placed boss found and no boss prefab assigned! Using placeholder capsule.");
            bossGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bossGO.name = "BOSS_Placeholder";
            bossGO.transform.position = bossSpawnPoint != null
                ? bossSpawnPoint.position + Vector3.up
                : new Vector3(0f, 1f, 0f);
            bossGO.transform.localScale = new Vector3(2f, 3f, 2f);
            var mat = bossGO.GetComponent<Renderer>().material;
            mat.color = Color.red;
        }

        // 2. Ensure health/AI components exist
        UnityEngine.AI.NavMeshAgent bossAgent = bossGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (bossAgent == null)
        {
            bossAgent = bossGO.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }

        ZombieHealth bossHealth = bossGO.GetComponent<ZombieHealth>();
        if (bossHealth == null)
        {
            bossHealth = bossGO.AddComponent<ZombieHealth>();
            bossHealth.maxHealth = 600f;
            bossHealth.ResetHealth();
        }
        else
        {
            bossHealth.maxHealth = 600f;
            bossHealth.ResetHealth();
        }

        bossHealth.ApplyHealthMultiplier(1f); // Keep final health at exactly 600
        ZombieHealth.OnZombieDied += OnBossKilled;

        ZombieAI bossAI = bossGO.GetComponent<ZombieAI>();
        if (bossAI == null)
        {
            bossAI = bossGO.AddComponent<ZombieAI>();
        }

        bossAI.detectionRange = 150f;
        bossAI.chaseRange = 150f;
        bossAI.attackDamage = 25f;
        bossAI.attackRange = 3f;

        // Force disable AI/movement during summon animation
        bossAI.enabled = false;
        if (bossAgent != null) bossAgent.isStopped = true;

        bossAlive = true;

        // 3. Disable normal zombie spawning
        ZombieSpawnManager spawnManager = FindObjectOfType<ZombieSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.enabled = false;
        }

        // 4. Remove all active non-boss zombies
        ZombieAI[] allZombies = FindObjectsOfType<ZombieAI>();
        foreach (ZombieAI z in allZombies)
        {
            if (z.gameObject != bossGO && z.gameObject.activeSelf)
            {
                z.gameObject.SetActive(false);
            }
        }

        // 5. Run PlaySummonSequence coroutine
        StartCoroutine(PlaySummonSequence(bossAI, bossAgent));
    }

    private GameObject FindBossInScene()
    {
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t.name.IndexOf("Monster10", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return t.gameObject;
                }
            }
        }
        return null;
    }

    private System.Collections.IEnumerator PlaySummonSequence(ZombieAI bossAI, UnityEngine.AI.NavMeshAgent agent)
    {
        Animator anim = bossGO.GetComponentInChildren<Animator>();
        float summonDuration = 3f;

        if (anim != null)
        {
            anim.Play("bosssummon");
            
            yield return new WaitForSeconds(0.1f);
            var stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName("bosssummon"))
            {
                summonDuration = stateInfo.length;
            }
            else
            {
                // Fallback to Attack04 if bosssummon doesn't exist
                anim.Play("Monster10_Attack04");
                summonDuration = 2.5f;
            }
        }

        yield return new WaitForSeconds(summonDuration);

        // Turn AI and movement back on
        if (bossAI != null)
        {
            bossAI.enabled = true;
        }
        if (agent != null)
        {
            agent.isStopped = false;
        }
        Debug.Log("[BossSummonManager] Boss summon sequence complete. AI activated.");
    }
    
    public void KillBoss()
    {
        if (!bossAlive || bossGO == null) return;
        ZombieHealth zh = bossGO.GetComponent<ZombieHealth>();
        if (zh != null) zh.TakeDamage(99999f);
    }

    public void ResetBoss()
    {
        if (!bossAlive || bossGO == null) return;
        
        Debug.Log("[BossSummonManager] Player left arena. Resetting Boss.");
        
        StopAllCoroutines();
        
        // 1. Reset boss health to max
        ZombieHealth bossHealth = bossGO.GetComponent<ZombieHealth>();
        if (bossHealth != null)
        {
            bossHealth.ResetHealth();
        }
        
        // 2. Return boss to statue position
        if (bossSpawnPoint != null)
        {
            UnityEngine.AI.NavMeshAgent agent = bossGO.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.Warp(bossSpawnPoint.position);
            else bossGO.transform.position = bossSpawnPoint.position;
        }
        
        // 3. Disable boss AI
        ZombieAI bossAI = bossGO.GetComponent<ZombieAI>();
        if (bossAI != null)
        {
            bossAI.enabled = false;
        }
        
        // 4. Boss returns to idle statue state
        Animator anim = bossGO.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Play("Monster10_Idle");
        }
        
        bossAlive = false;

        // Re-enable normal zombie spawning
        ZombieSpawnManager spawnManager = FindObjectOfType<ZombieSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.enabled = true;
        }
    }

    void OnBossKilled(ZombieHealth zh)
    {
        if (zh.gameObject != bossGO) return;

        bossAlive = false;
        ZombieHealth.OnZombieDied -= OnBossKilled;

        Debug.Log("[BossSummonManager] BOSS DEFEATED! Granting buff.");
        ApplyBuff();
        OnBossDefeated?.Invoke();
        
        // Re-enable normal zombie spawning
        ZombieSpawnManager spawnManager = FindObjectOfType<ZombieSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.enabled = true;
        }

        // Win state handling
        // Handled by styled GUI via OnBossDefeated subscription
    }

    void ApplyBuff()
    {
        PlayerHealth ph = FindObjectOfType<PlayerHealth>();
        if (ph == null) return;

        switch (buffType)
        {
            case BossBuffType.HealthBoost:
                ph.maxHealth += buffAmount;
                ph.Heal(buffAmount);
                Debug.Log($"[BossSummonManager] BUFF: Max health +{buffAmount}");
                break;

            case BossBuffType.DamageBoost:
                PlayerCombat pc = FindObjectOfType<PlayerCombat>();
                if (pc != null) pc.attackDamage += buffAmount;
                Debug.Log($"[BossSummonManager] BUFF: Attack damage +{buffAmount}");
                break;

            case BossBuffType.SpeedBoost:
                PlayerMovement pm = FindObjectOfType<PlayerMovement>();
                if (pm != null)
                {
                    pm.walkSpeed += buffAmount * 0.1f;
                    pm.runSpeed  += buffAmount * 0.2f;
                }
                Debug.Log($"[BossSummonManager] BUFF: Movement speed increased");
                break;

            case BossBuffType.Regeneration:
                // Start regen coroutine — heal 1hp/second
                StartCoroutine(RegenRoutine(ph, 1f, 30f));
                Debug.Log("[BossSummonManager] BUFF: Regeneration active for 30s");
                break;
        }
    }

    System.Collections.IEnumerator RegenRoutine(PlayerHealth ph, float hpPerSec, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && !ph.IsDead)
        {
            yield return new WaitForSeconds(1f);
            ph.Heal(hpPerSec);
            elapsed += 1f;
        }
    }
}

public enum BossBuffType
{
    HealthBoost,
    DamageBoost,
    SpeedBoost,
    Regeneration
}
