using UnityEngine;

/// <summary>
/// XPManager — Tracks zombie kills to fill an XP bar and unlocks an Ultimate Attack.
/// Modified to support persistent cooldown loops and state resetting on game restart.
/// </summary>
public class XPManager : MonoBehaviour
{
    public static XPManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("XPManager_Auto");
            go.AddComponent<XPManager>();
            DontDestroyOnLoad(go);
        }
    }

    [Header("XP Settings")]
    [Tooltip("How many zombies to kill to level up")]
    public int maxXP = 10;
    
    // Internal State
    private int currentXP = 0;
    private bool ultimateReady = false;
    private bool ultimateUnlocked = false;
    private bool ultimateActive = false;
    private float ultimateCooldownTimer = 0f;
    private float activeDurationTimer = 0f;
    
    private PlayerCombat playerCombat;

    // Public properties for UI binding
    public int CurrentXP => currentXP;
    public bool IsUltimateReady => ultimateReady;
    public bool IsUltimateUnlocked => ultimateUnlocked;
    public bool IsUltimateActive => ultimateActive;
    public float UltimateCooldownTimer => ultimateCooldownTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        ZombieHealth.OnZombieDied += HandleZombieDied;
        FindPlayerCombatReference();
    }

    void OnDestroy()
    {
        ZombieHealth.OnZombieDied -= HandleZombieDied;
    }

    void FindPlayerCombatReference()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerCombat = player.GetComponent<PlayerCombat>();
        }
    }

    void Update()
    {
        // Try to re-find PlayerCombat if reference is lost on scene reload
        if (playerCombat == null)
        {
            FindPlayerCombatReference();
        }

        // Keep player ultimateAttackTimer in sync if active
        if (ultimateActive)
        {
            activeDurationTimer -= Time.deltaTime;
            if (activeDurationTimer <= 0f)
            {
                ultimateActive = false;
                ultimateCooldownTimer = 5f; // start 5s cooldown
                Debug.Log("[XPManager] Ultimate duration completed. Entering 5s cooldown.");
            }
        }
        else if (ultimateCooldownTimer > 0f)
        {
            ultimateCooldownTimer -= Time.deltaTime;
            if (ultimateCooldownTimer <= 0f)
            {
                ultimateReady = true; // Ready to use again
                Debug.Log("[XPManager] Ultimate cooldown complete. Ready to use again.");
            }
        }
    }

    void HandleZombieDied(ZombieHealth zombie)
    {
        if (ultimateUnlocked) return; // Once unlocked, XP is full and remains unlocked

        currentXP++;
        if (currentXP >= maxXP)
        {
            LevelUp();
        }
    }

    public void LevelUp()
    {
        if (ultimateUnlocked) return;
        
        currentXP = maxXP;
        ultimateReady = true;
        ultimateUnlocked = true;
        
        // Notify UI of Level Up event if UIManager exists
        GameUIManager ui = FindObjectOfType<GameUIManager>();
        if (ui != null)
        {
            ui.TriggerLevelUpPopup();
        }

        Debug.Log("[XPManager] LEVEL UP! Ultimate Attack Ready.");
    }

    public void ActivateUltimate()
    {
        if (ultimateReady && playerCombat != null)
        {
            playerCombat.ActivateUltimateAttack();
            ultimateReady = false;
            ultimateActive = true;
            activeDurationTimer = 20f; // 20s active duration
            Debug.Log("[XPManager] Ultimate activated.");
        }
    }

    public void ResetState()
    {
        currentXP = 0;
        ultimateReady = false;
        ultimateUnlocked = false;
        ultimateActive = false;
        ultimateCooldownTimer = 0f;
        activeDurationTimer = 0f;
        playerCombat = null;
        Debug.Log("[XPManager] ResetState complete.");
    }
}
