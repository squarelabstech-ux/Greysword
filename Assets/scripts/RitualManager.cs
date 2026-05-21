using UnityEngine;

/// <summary>
/// RitualManager — Trigger zone at the ritual place.
/// When player enters with 9+ skulls, starts the boss summon sequence.
///
/// SETUP:
/// - Create a GameObject "RitualArea" at the ritual location on the island.
/// - Attach this script + a Trigger Collider (Box or Sphere, IsTrigger = true).
/// - Assign summonManager reference or let it auto-find.
/// - Tag ritual area however you like — no tag needed, just position it correctly.
/// </summary>
public class RitualManager : MonoBehaviour
{
    [Header("References")]
    public BossSummonManager summonManager;

    [Header("Visual Feedback (optional)")]
    [Tooltip("Particle effect or GameObject activated when ritual is ready")]
    public GameObject ritualReadyEffect;

    [Tooltip("Particle effect when ritual begins")]
    public GameObject ritualStartEffect;

    // ─── State ─────────────────────────────────────────────────────────────────

    private bool ritualPerformed = false;
    private bool ritualUnlocked  = false;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        if (summonManager == null)
            summonManager = FindObjectOfType<BossSummonManager>();
            
        if (summonManager == null)
        {
            GameObject smGO = new GameObject("BossSummonManager");
            summonManager = smGO.AddComponent<BossSummonManager>();
        }

        // Listen for skull collection
        SkullCounter.OnRitualUnlocked += OnRitualUnlocked;
    }

    void OnDestroy()
    {
        SkullCounter.OnRitualUnlocked -= OnRitualUnlocked;
    }

    void OnRitualUnlocked()
    {
        ritualUnlocked = true;
        Debug.Log("[RitualManager] Ritual zone is now active — enter to summon boss!");

        if (ritualReadyEffect != null)
            ritualReadyEffect.SetActive(true);
    }

    // ─── Trigger ───────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (ritualPerformed) return;

        if (!ritualUnlocked)
        {
            Debug.Log($"[RitualManager] Player entered ritual zone — needs {9 - SkullCounter.Instance?.SkullCount} more skulls.");
            return;
        }

        PerformRitual();
    }

    void PerformRitual()
    {
        ritualPerformed = true;
        Debug.Log("[RitualManager] RITUAL PERFORMED — summoning boss!");

        if (ritualStartEffect != null)
            ritualStartEffect.SetActive(true);

        if (summonManager != null)
            summonManager.SummonBoss();
    }
}
