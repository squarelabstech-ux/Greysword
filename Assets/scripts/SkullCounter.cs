using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SkullCounter — Tracks skull collectibles from skeleton enemies.
/// Part of the 9-skull ritual progression loop.
///
/// SETUP:
/// - Attach to a SkullTracker empty GameObject in the scene.
/// - Assign skullCountText UI Text if you want on-screen display.
/// - Skulls are added by calling SkullCounter.Instance.AddSkull()
///   (e.g., from a SkeletonDeath script or collectible trigger).
/// </summary>
public class SkullCounter : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How many skulls needed to unlock the ritual")]
    public int skullsRequired = 9;

    [Header("UI (optional)")]
    public Text skullCountText;

    // ─── Singleton ─────────────────────────────────────────────────────────────
    
    private static SkullCounter _instance;
    public static SkullCounter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SkullCounter>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SkullCounter");
                    _instance = go.AddComponent<SkullCounter>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    // ─── State ─────────────────────────────────────────────────────────────────

    private int skullCount = 0;
    public  int SkullCount => skullCount;
    public  bool RitualUnlocked => skullCount >= skullsRequired;

    // ─── Events ─────────────────────────────────────────────────────────────────

    public static event System.Action OnRitualUnlocked;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        RefreshUI();
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call this when a skeleton drops a skull.</summary>
    public void AddSkull()
    {
        if (skullCount >= skullsRequired) return;

        skullCount++;
        Debug.Log($"[SkullCounter] Skull collected: {skullCount}/{skullsRequired}");
        RefreshUI();

        if (skullCount >= skullsRequired)
        {
            Debug.Log("[SkullCounter] All skulls collected! Ritual unlocked.");
            OnRitualUnlocked?.Invoke();
        }
    }

    void RefreshUI()
    {
        if (skullCountText != null)
            skullCountText.text = $"Skulls: {skullCount}/{skullsRequired}";
    }

    /// <summary>Deduct skulls (e.g. for Boss Summon).</summary>
    public bool DeductSkulls(int amount)
    {
        if (skullCount >= amount)
        {
            skullCount -= amount;
            Debug.Log($"[SkullCounter] Deducted {amount} skulls. Remaining: {skullCount}");
            RefreshUI();
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void Update()
    {
        // Press S in editor to simulate skull pickup
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
        {
            Debug.Log("[SkullCounter] DEBUG: Adding skull.");
            AddSkull();
        }
    }
#endif
}
