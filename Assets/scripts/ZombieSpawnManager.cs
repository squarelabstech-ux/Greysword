using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ZombieSpawnManager — Handles spawning, pooling, and wave management.
///
/// SETUP:
/// - Create an empty GameObject "ZombieSpawnManager" in the scene.
/// - Attach this script.
/// - Assign zombiePrefab (your zombie prefab with ZombieAI + ZombieHealth + NavMeshAgent).
/// - Create several empty GameObjects around the island as spawn points and assign to spawnPoints[].
/// - Make sure NavMesh is baked before pressing Play.
/// </summary>
public class ZombieSpawnManager : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Zombie prefab — must have ZombieAI, ZombieHealth, NavMeshAgent")]
    public GameObject zombiePrefab;

    [Header("Spawn Points")]
    [Tooltip("List of Transforms around the island where zombies can spawn")]
    public Transform[] spawnPoints;

    [Header("Pool Settings")]
    [Tooltip("Total pool size (max zombies ever in scene at once)")]
    public int poolSize = 12;

    [Header("Wave Settings")]
    [Tooltip("Max zombies alive at the same time (controlled by AdaptiveDifficultyManager)")]
    public int maxAliveZombies = 6;

    [Tooltip("Seconds between spawn attempts")]
    public float spawnInterval = 15f;

    [Tooltip("Minimum distance from player to allow spawning")]
    public float minDistanceFromPlayer = 12f;

    [Header("References")]
    [Tooltip("Player transform — auto-found via Player tag if not set")]
    public Transform playerTransform;

    // ─── Private State ─────────────────────────────────────────────────────────

    private List<GameObject> pool       = new List<GameObject>();
    private int              aliveCount = 0;
    private float            spawnTimer = 0f;
    private bool             isSpawning = true;
    private BossArena        bossArena;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        BuildPool();

        bossArena = FindObjectOfType<BossArena>();

        // Stop spawning when player dies
        PlayerHealth.OnPlayerDeath += () => isSpawning = false;
        ZombieHealth.OnZombieDied  += OnZombieDied;

        // Spawn initial wave immediately
        SpawnWave(Mathf.Min(3, maxAliveZombies));
    }

    void OnDestroy()
    {
        ZombieHealth.OnZombieDied -= OnZombieDied;
    }

    public void AntigravitySpawnUpdate(float deltaTime)
    {
        if (!enabled || !isSpawning) return;

        spawnTimer -= deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnInterval;
            TrySpawnOne();
        }
    }

    // ─── Pool ──────────────────────────────────────────────────────────────────

    void BuildPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = Instantiate(zombiePrefab, Vector3.zero, Quaternion.identity, transform);
            go.SetActive(false);
            pool.Add(go);
        }
        Debug.Log($"[ZombieSpawnManager] Pool built with {poolSize} zombies.");
    }

    GameObject GetFromPool()
    {
        foreach (GameObject go in pool)
        {
            if (!go.activeSelf)
                return go;
        }
        return null; // pool exhausted
    }

    // ─── Spawning ──────────────────────────────────────────────────────────────

    void SpawnWave(int count)
    {
        for (int i = 0; i < count; i++)
            TrySpawnOne();
    }

    void TrySpawnOne()
    {
        if (aliveCount >= maxAliveZombies) return;

        Vector3 spawnPos;
        if (!TryGetValidSpawnPoint(out spawnPos))
        {
            Debug.LogWarning("[ZombieSpawnManager] No valid spawn point found — all too close to player or off NavMesh.");
            return;
        }

        GameObject zombie = GetFromPool();
        if (zombie == null)
        {
            Debug.LogWarning("[ZombieSpawnManager] Pool exhausted.");
            return;
        }

        // Reset zombie state before activating
        zombie.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        ZombieHealth zh = zombie.GetComponent<ZombieHealth>();
        if (zh != null) zh.ResetHealth();

        // Re-enable NavMeshAgent (was disabled on death)
        NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(spawnPos);
        }

        zombie.SetActive(true);
        aliveCount++;

        Debug.Log($"[ZombieSpawnManager] Spawned zombie at {spawnPos}. Alive: {aliveCount}/{maxAliveZombies}");
        if (AgenticDecisionLogger.Instance != null)
            AgenticDecisionLogger.Instance.LogRealTimeEvent("Zombie Spawn", $"Spawned zombie at {spawnPos}. Active Threats: {aliveCount}");
    }

    bool TryGetValidSpawnPoint(out Vector3 result)
    {
        result = Vector3.zero;
        if (playerTransform == null) return false;

        // If player is at the home campfire, completely disable spawning around them
        if (Vector3.Distance(playerTransform.position, new Vector3(659.9f, 1.6f, 680.56f)) < 50f)
        {
            return false;
        }

        Camera cam = Camera.main;

        // Try 15 random positions around the player
        for (int i = 0; i < 15; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minDistanceFromPlayer, minDistanceFromPlayer + 40f);
            Vector3 randomPos = playerTransform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // In camera view? We want believability, spawn behind/offscreen
            if (cam != null)
            {
                Vector3 viewportPoint = cam.WorldToViewportPoint(randomPos);
                bool inView = viewportPoint.z > 0 && viewportPoint.x > -0.1f && viewportPoint.x < 1.1f && viewportPoint.y > -0.1f && viewportPoint.y < 1.1f;
                if (inView) continue;
            }

            // Avoid Boss Arena
            if (bossArena != null && Vector3.Distance(randomPos, bossArena.transform.position) < 30f)
            {
                continue;
            }

            // Avoid Player Home Campfire (radius 50)
            if (Vector3.Distance(randomPos, new Vector3(659.9f, 1.6f, 680.56f)) < 50f)
            {
                continue;
            }

            // On NavMesh?
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPos, out hit, 10f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        // Fallback: If we couldn't find an out-of-sight spawn, just spawn anywhere valid on NavMesh near player
        Vector2 fallbackCircle = Random.insideUnitCircle.normalized * (minDistanceFromPlayer + 10f);
        Vector3 fallbackPos = playerTransform.position + new Vector3(fallbackCircle.x, 0f, fallbackCircle.y);
        
        // Avoid Player Home Campfire in fallback too (radius 50)
        if (Vector3.Distance(fallbackPos, new Vector3(659.9f, 1.6f, 680.56f)) < 50f)
        {
            return false;
        }

        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(fallbackPos, out fallbackHit, 15f, NavMesh.AllAreas))
        {
            result = fallbackHit.position;
            return true;
        }

        return false;
    }

    public void SpawnAmbush(int count = 3)
    {
        if (!isSpawning) return;
        Debug.Log($"[ZombieSpawnManager] AMBUSH COMMANDED! Spawning {count} zombies.");
        SpawnWave(count);
    }

    Transform[] ShuffledSpawnPoints()
    {
        Transform[] copy = (Transform[])spawnPoints.Clone();
        for (int i = copy.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    // ─── Events ────────────────────────────────────────────────────────────────

    void OnZombieDied(ZombieHealth zh)
    {
        aliveCount = Mathf.Max(0, aliveCount - 1);
        Debug.Log($"[ZombieSpawnManager] Zombie died. Alive: {aliveCount}/{maxAliveZombies}");
    }

    // ─── Adaptive Difficulty API ────────────────────────────────────────────────

    /// <summary>Called by AdaptiveDifficultyManager to adjust wave parameters at runtime.</summary>
    public void ApplyDifficultySettings(int newMaxAlive, float newSpawnInterval)
    {
        maxAliveZombies = newMaxAlive;
        spawnInterval   = newSpawnInterval;
        Debug.Log($"[ZombieSpawnManager] Difficulty updated — Max: {maxAliveZombies}, Interval: {spawnInterval}s");
    }

    /// <summary>Apply difficulty profile to all currently alive zombies.</summary>
    public void ApplyDifficultyToActiveZombies(float speedMult, float damageMult, float healthMult)
    {
        foreach (GameObject go in pool)
        {
            if (!go.activeSelf) continue;

            ZombieAI ai = go.GetComponent<ZombieAI>();
            if (ai != null)
            {
                ai.runtimeSpeedMultiplier = speedMult;
                ai.runtimeDamageMultiplier = damageMult;
            }

            ZombieHealth zh = go.GetComponent<ZombieHealth>();
            if (zh != null) zh.ApplyHealthMultiplier(healthMult);
        }
    }
}
