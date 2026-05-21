using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// GameUIManager — Dynamically creates and manages the entire high-fidelity Game UI
/// using the imported "GUI Parts" sprites. Handles the HUD, objective tracker,
/// Mobile Controls, Level Up popups, Victory screens, and Game Over loops.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    public static GameUIManager Instance { get; private set; }

    [Header("Sprites from GUI Parts (Auto-Assigned)")]
    public Sprite hpFrame;
    public Sprite hpLine;
    public Sprite progressFrame;
    public Sprite progressLine;
    public Sprite buttonBackground;
    public Sprite buttonBackgroundActive;
    public Sprite frameBig;
    public Sprite frameMid;
    public Sprite nameBar;

    [Header("Icons (Auto-Assigned)")]
    public Sprite weaponIcon;
    public Sprite skillIcon;
    public Sprite armorIcon;
    public Sprite skullIcon;

    // Static Core Game Loop State Gating
    public static bool IsGamePlaying = false;
    public static bool IsGamePaused = false;

    // UI References
    private Canvas mainCanvas;
    private GameObject hudPanel;
    private Image hpFillImage;
    private Image xpFillImage;
    private Text hpText;
    private Text xpText;
    private Text skullText;
    private Text objectiveText;

    private GameObject levelUpPopup;
    private Text levelUpText;

    private GameObject pausePanel;
    private GameObject gameOverPanel;
    private GameObject victoryPanel;

    // Mobile References
    private GameObject mobileControlsPanel;
    private Button mobileUltimateBtn;

    // Logic State
    private PlayerHealth playerHealth;
    private PlayerCombat playerCombat;
    private bool isLevelUpPopupActive = false;

    private Font GetDefaultFont()
    {
        Font font = Resources.Load<Font>("arial");
        if (font != null) return font;

        font = Resources.Load<Font>("Arial");
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null) return font;

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) return font;

        Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
        foreach (var f in allFonts)
        {
            if (f != null && !string.IsNullOrEmpty(f.name))
                return f;
        }
        return null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Reset static loop states on load/reload - Start game active immediately
        IsGamePlaying = true;
        IsGamePaused = false;
        Time.timeScale = 1f;

        // Force 60 FPS target frame rate on mobile to prevent Unity default 30 FPS lock
        Application.targetFrameRate = 60;

        // Reduce physics tick rate for mobile performance (50 -> 30 Hz)
        Time.fixedDeltaTime = 0.033f; // ~30 Hz physics instead of default 50 Hz

        // Apply saved graphics preset with runtime optimizations
        int savedPreset = PlayerPrefs.GetInt("GraphicsPreset", 0); // Default to Low (0) on mobile
        ApplyGraphicsPreset(savedPreset);
    }

    void Start()
    {
        // Find Player components
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
            playerCombat = player.GetComponent<PlayerCombat>();
        }

        // Build UI Canvas and screens
        CreateCanvas();
        CreateHUD();
        CreatePausePanel();
        CreateLevelUpPopup();
        CreateGameOverPanel();
        CreateVictoryPanel();

        // Initially show the HUD and hide dialog panels
        if (hudPanel != null) hudPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // Subscribe to death and victory events
        PlayerHealth.OnPlayerDeath += OnPlayerDied;
        BossSummonManager.OnBossDefeated += OnBossDefeated;
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDeath -= OnPlayerDied;
        BossSummonManager.OnBossDefeated -= OnBossDefeated;
    }

    void Update()
    {
        if (!IsGamePlaying || IsGamePaused) return;

        // 1. Update Health Bar
        if (playerHealth != null && hpFillImage != null)
        {
            float hpRatio = playerHealth.HealthFraction;
            hpFillImage.rectTransform.anchorMax = new Vector2(hpRatio, 1f);
            hpText.text = $"HP: {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.maxHealth)}";
            
            // Health color indicator: Red to Green
            hpFillImage.color = Color.Lerp(Color.red, Color.green, hpRatio);
        }

        // 2. Update XP Bar
        if (XPManager.Instance != null && xpFillImage != null)
        {
            float xpRatio = Mathf.Clamp01((float)XPManager.Instance.CurrentXP / XPManager.Instance.maxXP);
            xpFillImage.rectTransform.anchorMax = new Vector2(xpRatio, 1f);
            xpText.text = $"XP: {XPManager.Instance.CurrentXP} / {XPManager.Instance.maxXP}";

            // Enable ultimate button on mobile when ready
            if (mobileUltimateBtn != null)
            {
                mobileUltimateBtn.gameObject.SetActive(XPManager.Instance.IsUltimateReady);
            }
        }

        // 3. Update Skull Counter
        if (SkullCounter.Instance != null && skullText != null)
        {
            skullText.text = $"{SkullCounter.Instance.SkullCount} / 7";
        }

        // 4. Update Objective Text
        if (objectiveText != null)
        {
            if (playerCombat != null && !playerCombat.hasWeapon)
            {
                objectiveText.text = "Objective: Search the starting area and pick up the Sword!";
            }
            else if (SkullCounter.Instance != null && SkullCounter.Instance.SkullCount < 7)
            {
                objectiveText.text = $"Objective: Kill zombies and gather Skulls ({SkullCounter.Instance.SkullCount}/7)";
            }
            else
            {
                BossSummonManager boss = FindObjectOfType<BossSummonManager>();
                if (boss != null && boss.IsBossAlive)
                {
                    objectiveText.text = "Objective: DEFEAT THE ZOMBIE BOSS!";
                }
                else
                {
                    objectiveText.text = "Objective: Follow the stone path to the stairs & Summon the Boss!";
                }
            }
        }

        // 5. Ultimate Activation Shortcut via Input System Keyboard check (safe from InvalidOperationException)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.uKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
            {
                OnUltimateClicked();
            }
        }
    }

    // ─── UI Creation ───────────────────────────────────────────────────────────

    void CreateCanvas()
    {
        GameObject canvasGO = new GameObject("GameCanvas", typeof(RectTransform));
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
    }

    void CreateHUD()
    {
        hudPanel = new GameObject("HUDPanel", typeof(RectTransform));
        hudPanel.transform.SetParent(mainCanvas.transform, false);

        RectTransform hudRect = hudPanel.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.sizeDelta = Vector2.zero;

        Font font = GetDefaultFont();

        // 1. Health Bar Frame
        GameObject hpFrameGO = new GameObject("HealthBarFrame", typeof(RectTransform));
        hpFrameGO.transform.SetParent(hudPanel.transform, false);
        Image hpFrameImg = hpFrameGO.AddComponent<Image>();
        hpFrameImg.sprite = hpFrame;
        hpFrameImg.type = Image.Type.Sliced;
        
        RectTransform hpFrameRect = hpFrameGO.GetComponent<RectTransform>();
        hpFrameRect.anchorMin = new Vector2(0f, 1f);
        hpFrameRect.anchorMax = new Vector2(0f, 1f);
        hpFrameRect.pivot = new Vector2(0f, 1f);
        hpFrameRect.sizeDelta = new Vector2(350f, 45f);
        hpFrameRect.anchoredPosition = new Vector2(30f, -30f);

        // Health Bar Fill Area
        GameObject hpFillArea = new GameObject("HealthFillArea", typeof(RectTransform));
        hpFillArea.transform.SetParent(hpFrameGO.transform, false);
        RectTransform hpFillAreaRect = hpFillArea.GetComponent<RectTransform>();
        hpFillAreaRect.anchorMin = new Vector2(0.08f, 0.15f);
        hpFillAreaRect.anchorMax = new Vector2(0.92f, 0.85f);
        hpFillAreaRect.sizeDelta = Vector2.zero;

        // Health Bar Fill
        GameObject hpFillGO = new GameObject("HealthFill", typeof(RectTransform));
        hpFillGO.transform.SetParent(hpFillArea.transform, false);
        hpFillImage = hpFillGO.AddComponent<Image>();
        hpFillImage.sprite = hpLine;
        hpFillImage.type = Image.Type.Filled;
        hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        
        RectTransform hpFillRect = hpFillGO.GetComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = new Vector2(1f, 1f); // controlled in Update
        hpFillRect.sizeDelta = Vector2.zero;
        hpFillRect.pivot = new Vector2(0f, 0.5f);

        // Health Text
        GameObject hpTextGO = new GameObject("HealthText", typeof(RectTransform));
        hpTextGO.transform.SetParent(hpFrameGO.transform, false);
        hpText = hpTextGO.AddComponent<Text>();
        hpText.font = font;
        hpText.fontSize = 18;
        hpText.alignment = TextAnchor.MiddleCenter;
        hpText.color = Color.white;
        Shadow hpShadow = hpTextGO.AddComponent<Shadow>();
        hpShadow.effectColor = Color.black;

        RectTransform hpTextRect = hpTextGO.GetComponent<RectTransform>();
        hpTextRect.anchorMin = Vector2.zero;
        hpTextRect.anchorMax = Vector2.one;
        hpTextRect.sizeDelta = Vector2.zero;


        // 2. XP Bar Frame
        GameObject xpFrameGO = new GameObject("XPBarFrame", typeof(RectTransform));
        xpFrameGO.transform.SetParent(hudPanel.transform, false);
        Image xpFrameImg = xpFrameGO.AddComponent<Image>();
        xpFrameImg.sprite = progressFrame != null ? progressFrame : hpFrame;
        xpFrameImg.type = Image.Type.Sliced;
        
        RectTransform xpFrameRect = xpFrameGO.GetComponent<RectTransform>();
        xpFrameRect.anchorMin = new Vector2(0f, 1f);
        xpFrameRect.anchorMax = new Vector2(0f, 1f);
        xpFrameRect.pivot = new Vector2(0f, 1f);
        xpFrameRect.sizeDelta = new Vector2(300f, 30f);
        xpFrameRect.anchoredPosition = new Vector2(30f, -85f);

        // XP Bar Fill Area
        GameObject xpFillArea = new GameObject("XPFillArea", typeof(RectTransform));
        xpFillArea.transform.SetParent(xpFrameGO.transform, false);
        RectTransform xpFillAreaRect = xpFillArea.GetComponent<RectTransform>();
        xpFillAreaRect.anchorMin = new Vector2(0.08f, 0.15f);
        xpFillAreaRect.anchorMax = new Vector2(0.92f, 0.85f);
        xpFillAreaRect.sizeDelta = Vector2.zero;

        // XP Bar Fill
        GameObject xpFillGO = new GameObject("XPFill", typeof(RectTransform));
        xpFillGO.transform.SetParent(xpFillArea.transform, false);
        xpFillImage = xpFillGO.AddComponent<Image>();
        xpFillImage.sprite = progressLine != null ? progressLine : hpLine;
        xpFillImage.color = new Color(0.2f, 0.8f, 1f, 1f); // cyan-blue XP fill
        
        RectTransform xpFillRect = xpFillGO.GetComponent<RectTransform>();
        xpFillRect.anchorMin = Vector2.zero;
        xpFillRect.anchorMax = new Vector2(0f, 1f);
        xpFillRect.sizeDelta = Vector2.zero;
        xpFillRect.pivot = new Vector2(0f, 0.5f);

        // XP Text
        GameObject xpTextGO = new GameObject("XPText", typeof(RectTransform));
        xpTextGO.transform.SetParent(xpFrameGO.transform, false);
        xpText = xpTextGO.AddComponent<Text>();
        xpText.font = font;
        xpText.fontSize = 14;
        xpText.alignment = TextAnchor.MiddleCenter;
        xpText.color = Color.white;
        Shadow xpShadow = xpTextGO.AddComponent<Shadow>();
        xpShadow.effectColor = Color.black;

        RectTransform xpTextRect = xpTextGO.GetComponent<RectTransform>();
        xpTextRect.anchorMin = Vector2.zero;
        xpTextRect.anchorMax = Vector2.one;
        xpTextRect.sizeDelta = Vector2.zero;


        // 3. Skull Counter (Top Right)
        GameObject skullPanelGO = new GameObject("SkullPanel", typeof(RectTransform));
        skullPanelGO.transform.SetParent(hudPanel.transform, false);
        Image skullPanelImg = skullPanelGO.AddComponent<Image>();
        skullPanelImg.sprite = nameBar;
        skullPanelImg.type = Image.Type.Sliced;
        
        RectTransform skullPanelRect = skullPanelGO.GetComponent<RectTransform>();
        skullPanelRect.anchorMin = new Vector2(1f, 1f);
        skullPanelRect.anchorMax = new Vector2(1f, 1f);
        skullPanelRect.pivot = new Vector2(1f, 1f);
        skullPanelRect.sizeDelta = new Vector2(200f, 50f);
        skullPanelRect.anchoredPosition = new Vector2(-30f, -30f);

        // Skull Icon
        GameObject skullIconGO = new GameObject("SkullIcon", typeof(RectTransform));
        skullIconGO.transform.SetParent(skullPanelGO.transform, false);
        Image skullIconImg = skullIconGO.AddComponent<Image>();
        skullIconImg.sprite = skullIcon;
        
        RectTransform skullIconRect = skullIconGO.GetComponent<RectTransform>();
        skullIconRect.anchorMin = new Vector2(0f, 0.5f);
        skullIconRect.anchorMax = new Vector2(0f, 0.5f);
        skullIconRect.pivot = new Vector2(0f, 0.5f);
        skullIconRect.sizeDelta = new Vector2(40f, 40f);
        skullIconRect.anchoredPosition = new Vector2(10f, 0f);

        // Skull Text
        GameObject skullTextGO = new GameObject("SkullText", typeof(RectTransform));
        skullTextGO.transform.SetParent(skullPanelGO.transform, false);
        skullText = skullTextGO.AddComponent<Text>();
        skullText.font = font;
        skullText.fontSize = 20;
        skullText.alignment = TextAnchor.MiddleLeft;
        skullText.color = Color.yellow;
        Shadow skullShadow = skullTextGO.AddComponent<Shadow>();
        skullShadow.effectColor = Color.black;

        RectTransform skullTextRect = skullTextGO.GetComponent<RectTransform>();
        skullTextRect.anchorMin = new Vector2(0.3f, 0f);
        skullTextRect.anchorMax = new Vector2(1f, 1f);
        skullTextRect.sizeDelta = Vector2.zero;


        // 4. In-Game Pause Button (Top Right, shifted left of Skull Counter)
        GameObject pauseBtnGO = new GameObject("PauseButton", typeof(RectTransform));
        pauseBtnGO.transform.SetParent(hudPanel.transform, false);
        Image pauseBtnImg = pauseBtnGO.AddComponent<Image>();
        pauseBtnImg.sprite = buttonBackground;
        Button pauseBtn = pauseBtnGO.AddComponent<Button>();

        RectTransform pauseBtnRect = pauseBtnGO.GetComponent<RectTransform>();
        pauseBtnRect.anchorMin = new Vector2(1f, 1f);
        pauseBtnRect.anchorMax = new Vector2(1f, 1f);
        pauseBtnRect.pivot = new Vector2(1f, 1f);
        pauseBtnRect.sizeDelta = new Vector2(100f, 45f);
        pauseBtnRect.anchoredPosition = new Vector2(-250f, -32f);

        GameObject pauseBtnTextGO = new GameObject("Text", typeof(RectTransform));
        pauseBtnTextGO.transform.SetParent(pauseBtnGO.transform, false);
        Text pauseBtnTxt = pauseBtnTextGO.AddComponent<Text>();
        pauseBtnTxt.font = font;
        pauseBtnTxt.text = "PAUSE";
        pauseBtnTxt.fontSize = 16;
        pauseBtnTxt.alignment = TextAnchor.MiddleCenter;
        pauseBtnTxt.color = Color.white;
        Shadow pauseBtnShadow = pauseBtnTextGO.AddComponent<Shadow>();
        pauseBtnShadow.effectColor = Color.black;

        RectTransform pauseBtnTextRect = pauseBtnTextGO.GetComponent<RectTransform>();
        pauseBtnTextRect.anchorMin = Vector2.zero;
        pauseBtnTextRect.anchorMax = Vector2.one;
        pauseBtnTextRect.sizeDelta = Vector2.zero;

        pauseBtn.onClick.AddListener(PauseGame);


        // 5. Objective Tracker HUD (Top Center)
        GameObject objGO = new GameObject("ObjectiveHUD", typeof(RectTransform));
        objGO.transform.SetParent(hudPanel.transform, false);
        Image objImg = objGO.AddComponent<Image>();
        objImg.sprite = nameBar;
        objImg.type = Image.Type.Sliced;
        objImg.color = new Color(0f, 0f, 0f, 0.6f);

        RectTransform objRect = objGO.GetComponent<RectTransform>();
        objRect.anchorMin = new Vector2(0.5f, 1f);
        objRect.anchorMax = new Vector2(0.5f, 1f);
        objRect.pivot = new Vector2(0.5f, 1f);
        objRect.sizeDelta = new Vector2(500f, 45f);
        objRect.anchoredPosition = new Vector2(0f, -30f);

        GameObject objTextGO = new GameObject("Text", typeof(RectTransform));
        objTextGO.transform.SetParent(objGO.transform, false);
        objectiveText = objTextGO.AddComponent<Text>();
        objectiveText.font = font;
        objectiveText.fontSize = 18;
        objectiveText.alignment = TextAnchor.MiddleCenter;
        objectiveText.color = new Color(1f, 0.9f, 0.7f, 1f);
        Shadow objShadow = objTextGO.AddComponent<Shadow>();
        objShadow.effectColor = Color.black;

        RectTransform objTextRect = objTextGO.GetComponent<RectTransform>();
        objTextRect.anchorMin = Vector2.zero;
        objTextRect.anchorMax = Vector2.one;
        objTextRect.sizeDelta = Vector2.zero;


        // 6. Create Mobile Controls (Canvas Overlays)
        CreateMobileControls();
    }

    void CreateMobileControls()
    {
        mobileControlsPanel = new GameObject("MobileControlsPanel", typeof(RectTransform));
        mobileControlsPanel.transform.SetParent(hudPanel.transform, false);
        RectTransform mobileRect = mobileControlsPanel.GetComponent<RectTransform>();
        mobileRect.anchorMin = Vector2.zero;
        mobileRect.anchorMax = Vector2.one;
        mobileRect.sizeDelta = Vector2.zero;

        // Attach MobileControls logic component
        MobileControls controls = mobileControlsPanel.AddComponent<MobileControls>();

        // 1. Virtual Joystick Area (Bottom Left)
        GameObject joystickBgGO = new GameObject("JoystickBackground", typeof(RectTransform));
        joystickBgGO.transform.SetParent(mobileControlsPanel.transform, false);
        Image joystickBgImg = joystickBgGO.AddComponent<Image>();
        joystickBgImg.sprite = progressFrame; // Use framed plate or nameBar
        joystickBgImg.type = Image.Type.Sliced;
        joystickBgImg.color = new Color(1f, 1f, 1f, 0.5f);

        RectTransform joystickBgRect = joystickBgGO.GetComponent<RectTransform>();
        joystickBgRect.anchorMin = new Vector2(0f, 0f);
        joystickBgRect.anchorMax = new Vector2(0f, 0f);
        joystickBgRect.pivot = new Vector2(0f, 0f);
        joystickBgRect.sizeDelta = new Vector2(200f, 200f);
        joystickBgRect.anchoredPosition = new Vector2(80f, 80f);

        GameObject joystickHandleGO = new GameObject("JoystickHandle", typeof(RectTransform));
        joystickHandleGO.transform.SetParent(joystickBgGO.transform, false);
        Image joystickHandleImg = joystickHandleGO.AddComponent<Image>();
        joystickHandleImg.sprite = buttonBackground; // round button element
        joystickHandleImg.color = new Color(1f, 1f, 1f, 0.8f);

        RectTransform joystickHandleRect = joystickHandleGO.GetComponent<RectTransform>();
        joystickHandleRect.anchorMin = new Vector2(0.5f, 0.5f);
        joystickHandleRect.anchorMax = new Vector2(0.5f, 0.5f);
        joystickHandleRect.pivot = new Vector2(0.5f, 0.5f);
        joystickHandleRect.sizeDelta = new Vector2(80f, 80f);
        joystickHandleRect.anchoredPosition = Vector2.zero;

        // Assign to controller
        controls.joystickBackground = joystickBgRect;
        controls.joystickHandle = joystickHandleRect;

        // 2. Action Area: Attack Button (Bottom Right)
        GameObject attackBtnGO = new GameObject("AttackButton", typeof(RectTransform));
        attackBtnGO.transform.SetParent(mobileControlsPanel.transform, false);
        Image attackBtnImg = attackBtnGO.AddComponent<Image>();
        attackBtnImg.sprite = buttonBackground;
        Button attackBtn = attackBtnGO.AddComponent<Button>();

        RectTransform attackBtnRect = attackBtnGO.GetComponent<RectTransform>();
        attackBtnRect.anchorMin = new Vector2(1f, 0f);
        attackBtnRect.anchorMax = new Vector2(1f, 0f);
        attackBtnRect.pivot = new Vector2(1f, 0f);
        attackBtnRect.sizeDelta = new Vector2(130f, 130f);
        attackBtnRect.anchoredPosition = new Vector2(-80f, 80f);

        // Sword Icon inside Attack Button
        GameObject weaponIconGO = new GameObject("Icon", typeof(RectTransform));
        weaponIconGO.transform.SetParent(attackBtnGO.transform, false);
        Image weaponIconImg = weaponIconGO.AddComponent<Image>();
        weaponIconImg.sprite = weaponIcon;
        RectTransform weaponIconRect = weaponIconGO.GetComponent<RectTransform>();
        weaponIconRect.anchorMin = new Vector2(0.2f, 0.2f);
        weaponIconRect.anchorMax = new Vector2(0.8f, 0.8f);
        weaponIconRect.sizeDelta = Vector2.zero;

        controls.attackButton = attackBtn;

        // 3. Action Area: Jump Button (Next to Attack)
        GameObject jumpBtnGO = new GameObject("JumpButton", typeof(RectTransform));
        jumpBtnGO.transform.SetParent(mobileControlsPanel.transform, false);
        Image jumpBtnImg = jumpBtnGO.AddComponent<Image>();
        jumpBtnImg.sprite = buttonBackground;
        Button jumpBtn = jumpBtnGO.AddComponent<Button>();

        RectTransform jumpBtnRect = jumpBtnGO.GetComponent<RectTransform>();
        jumpBtnRect.anchorMin = new Vector2(1f, 0f);
        jumpBtnRect.anchorMax = new Vector2(1f, 0f);
        jumpBtnRect.pivot = new Vector2(1f, 0f);
        jumpBtnRect.sizeDelta = new Vector2(90f, 90f);
        jumpBtnRect.anchoredPosition = new Vector2(-230f, 80f);

        GameObject jumpTextGO = new GameObject("Text", typeof(RectTransform));
        jumpTextGO.transform.SetParent(jumpBtnGO.transform, false);
        Text jumpTxt = jumpTextGO.AddComponent<Text>();
        jumpTxt.font = GetDefaultFont();
        jumpTxt.text = "JUMP";
        jumpTxt.fontSize = 18;
        jumpTxt.alignment = TextAnchor.MiddleCenter;
        jumpTxt.color = Color.white;
        RectTransform jumpTextRect = jumpTextGO.GetComponent<RectTransform>();
        jumpTextRect.anchorMin = Vector2.zero;
        jumpTextRect.anchorMax = Vector2.one;
        jumpTextRect.sizeDelta = Vector2.zero;

        controls.jumpButton = jumpBtn;

        // 4. Action Area: Sprint Button (Above Jump)
        GameObject sprintBtnGO = new GameObject("SprintButton", typeof(RectTransform));
        sprintBtnGO.transform.SetParent(mobileControlsPanel.transform, false);
        Image sprintBtnImg = sprintBtnGO.AddComponent<Image>();
        sprintBtnImg.sprite = buttonBackground;
        Button sprintBtn = sprintBtnGO.AddComponent<Button>();

        RectTransform sprintBtnRect = sprintBtnGO.GetComponent<RectTransform>();
        sprintBtnRect.anchorMin = new Vector2(1f, 0f);
        sprintBtnRect.anchorMax = new Vector2(1f, 0f);
        sprintBtnRect.pivot = new Vector2(1f, 0f);
        sprintBtnRect.sizeDelta = new Vector2(90f, 90f);
        sprintBtnRect.anchoredPosition = new Vector2(-230f, 190f);

        GameObject sprintTextGO = new GameObject("Text", typeof(RectTransform));
        sprintTextGO.transform.SetParent(sprintBtnGO.transform, false);
        Text sprintTxt = sprintTextGO.AddComponent<Text>();
        sprintTxt.font = GetDefaultFont();
        sprintTxt.text = "RUN";
        sprintTxt.fontSize = 18;
        sprintTxt.alignment = TextAnchor.MiddleCenter;
        sprintTxt.color = Color.white;
        RectTransform sprintTextRect = sprintTextGO.GetComponent<RectTransform>();
        sprintTextRect.anchorMin = Vector2.zero;
        sprintTextRect.anchorMax = Vector2.one;
        sprintTextRect.sizeDelta = Vector2.zero;

        controls.sprintButton = sprintBtn;

        // 5. Ultimate Skill Button (Above Attack)
        GameObject ultimateBtnGO = new GameObject("UltimateButton", typeof(RectTransform));
        ultimateBtnGO.transform.SetParent(mobileControlsPanel.transform, false);
        Image ultimateBtnImg = ultimateBtnGO.AddComponent<Image>();
        ultimateBtnImg.sprite = buttonBackgroundActive != null ? buttonBackgroundActive : buttonBackground;
        mobileUltimateBtn = ultimateBtnGO.AddComponent<Button>();

        RectTransform ultimateBtnRect = ultimateBtnGO.GetComponent<RectTransform>();
        ultimateBtnRect.anchorMin = new Vector2(1f, 0f);
        ultimateBtnRect.anchorMax = new Vector2(1f, 0f);
        ultimateBtnRect.pivot = new Vector2(1f, 0f);
        ultimateBtnRect.sizeDelta = new Vector2(100f, 100f);
        ultimateBtnRect.anchoredPosition = new Vector2(-95f, 230f);

        GameObject ultimateIconGO = new GameObject("Icon", typeof(RectTransform));
        ultimateIconGO.transform.SetParent(ultimateBtnGO.transform, false);
        Image ultimateIconImg = ultimateIconGO.AddComponent<Image>();
        ultimateIconImg.sprite = skillIcon;
        RectTransform ultimateIconRect = ultimateIconGO.GetComponent<RectTransform>();
        ultimateIconRect.anchorMin = new Vector2(0.15f, 0.15f);
        ultimateIconRect.anchorMax = new Vector2(0.85f, 0.85f);
        ultimateIconRect.sizeDelta = Vector2.zero;

        // Label below ultimate button
        GameObject ultimateTextGO = new GameObject("Text", typeof(RectTransform));
        ultimateTextGO.transform.SetParent(ultimateBtnGO.transform, false);
        Text ultimateTxt = ultimateTextGO.AddComponent<Text>();
        ultimateTxt.font = GetDefaultFont();
        ultimateTxt.text = "ULTIMATE";
        ultimateTxt.fontSize = 14;
        ultimateTxt.alignment = TextAnchor.MiddleCenter;
        ultimateTxt.color = Color.yellow;
        Shadow ultimateTextShadow = ultimateTextGO.AddComponent<Shadow>();
        ultimateTextShadow.effectColor = Color.black;
        ultimateTextShadow.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform ultimateTextRect = ultimateTextGO.GetComponent<RectTransform>();
        ultimateTextRect.anchorMin = new Vector2(0f, 0f);
        ultimateTextRect.anchorMax = new Vector2(1f, 0.2f);
        ultimateTextRect.anchoredPosition = new Vector2(0f, -15f);
        ultimateTextRect.sizeDelta = Vector2.zero;

        mobileUltimateBtn.onClick.AddListener(OnUltimateClicked);
        mobileUltimateBtn.gameObject.SetActive(false); // only show when ready
    }

    void CreatePausePanel()
    {
        pausePanel = new GameObject("PausePanel", typeof(RectTransform));
        pausePanel.transform.SetParent(mainCanvas.transform, false);
        RectTransform panelRect = pausePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelBg = pausePanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.65f); // semi-translucent dark overlay

        // Dialog Frame - height increased to 460 to fit settings
        GameObject dialogGO = new GameObject("PauseFrame", typeof(RectTransform));
        dialogGO.transform.SetParent(pausePanel.transform, false);
        Image dialogImg = dialogGO.AddComponent<Image>();
        if (frameMid != null)
        {
            dialogImg.sprite = frameMid;
            dialogImg.type = Image.Type.Sliced;
        }
        else
        {
            dialogImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        }

        RectTransform dialogRect = dialogGO.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero; // Explicitly center!
        dialogRect.sizeDelta = new Vector2(400f, 460f);

        Font font = GetDefaultFont();

        // Title text
        GameObject titleGO = new GameObject("PauseTitleText", typeof(RectTransform));
        titleGO.transform.SetParent(dialogGO.transform, false);
        Text titleTxt = titleGO.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.text = "GAME PAUSED";
        titleTxt.fontSize = 28;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = Color.white;
        Shadow titleShadow = titleGO.AddComponent<Shadow>();
        titleShadow.effectColor = Color.black;

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(0f, 50f);

        // ────────── 1. SENSITIVITY SECTION ──────────
        GameObject sensLabelGO = new GameObject("SensLabel", typeof(RectTransform));
        sensLabelGO.transform.SetParent(dialogGO.transform, false);
        Text sensLabel = sensLabelGO.AddComponent<Text>();
        sensLabel.font = font;
        sensLabel.text = "CAMERA SENSITIVITY";
        sensLabel.fontSize = 14;
        sensLabel.alignment = TextAnchor.MiddleCenter;
        sensLabel.color = new Color(1f, 0.9f, 0.6f);
        Shadow sensLabelShadow = sensLabelGO.AddComponent<Shadow>();
        sensLabelShadow.effectColor = Color.black;

        RectTransform sensLabelRect = sensLabelGO.GetComponent<RectTransform>();
        sensLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
        sensLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
        sensLabelRect.pivot = new Vector2(0.5f, 0.5f);
        sensLabelRect.anchoredPosition = new Vector2(0f, 130f);
        sensLabelRect.sizeDelta = new Vector2(300f, 20f);

        // Value text
        GameObject sensValueGO = new GameObject("SensValue", typeof(RectTransform));
        sensValueGO.transform.SetParent(dialogGO.transform, false);
        Text sensValueTxt = sensValueGO.AddComponent<Text>();
        sensValueTxt.font = font;
        float currentSens = PlayerPrefs.GetFloat("CameraSensitivityMultiplier", 1.0f);
        sensValueTxt.text = currentSens.ToString("F1");
        sensValueTxt.fontSize = 20;
        sensValueTxt.alignment = TextAnchor.MiddleCenter;
        sensValueTxt.color = Color.white;
        Shadow sensValueShadow = sensValueGO.AddComponent<Shadow>();
        sensValueShadow.effectColor = Color.black;

        RectTransform sensValueRect = sensValueGO.GetComponent<RectTransform>();
        sensValueRect.anchorMin = new Vector2(0.5f, 0.5f);
        sensValueRect.anchorMax = new Vector2(0.5f, 0.5f);
        sensValueRect.pivot = new Vector2(0.5f, 0.5f);
        sensValueRect.anchoredPosition = new Vector2(0f, 95f);
        sensValueRect.sizeDelta = new Vector2(80f, 40f);

        // Decrement button
        GameObject decBtnGO = new GameObject("DecBtn", typeof(RectTransform));
        decBtnGO.transform.SetParent(dialogGO.transform, false);
        Image decImg = decBtnGO.AddComponent<Image>();
        decImg.sprite = buttonBackground;
        Button decBtn = decBtnGO.AddComponent<Button>();

        RectTransform decBtnRect = decBtnGO.GetComponent<RectTransform>();
        decBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        decBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        decBtnRect.pivot = new Vector2(0.5f, 0.5f);
        decBtnRect.anchoredPosition = new Vector2(-70f, 95f);
        decBtnRect.sizeDelta = new Vector2(40f, 35f);

        GameObject decTxtGO = new GameObject("Text", typeof(RectTransform));
        decTxtGO.transform.SetParent(decBtnGO.transform, false);
        Text decTxt = decTxtGO.AddComponent<Text>();
        decTxt.font = font;
        decTxt.text = "<";
        decTxt.fontSize = 18;
        decTxt.alignment = TextAnchor.MiddleCenter;
        decTxt.color = Color.white;

        RectTransform decTxtRect = decTxtGO.GetComponent<RectTransform>();
        decTxtRect.anchorMin = Vector2.zero;
        decTxtRect.anchorMax = Vector2.one;
        decTxtRect.sizeDelta = Vector2.zero;

        decBtn.onClick.AddListener(() => {
            float sens = ThirdPersonCamera.CameraSensitivityMultiplier;
            sens = Mathf.Max(0.2f, sens - 0.1f);
            ThirdPersonCamera.CameraSensitivityMultiplier = sens;
            PlayerPrefs.SetFloat("CameraSensitivityMultiplier", sens);
            PlayerPrefs.Save();
            sensValueTxt.text = sens.ToString("F1");
        });

        // Increment button
        GameObject incBtnGO = new GameObject("IncBtn", typeof(RectTransform));
        incBtnGO.transform.SetParent(dialogGO.transform, false);
        Image incImg = incBtnGO.AddComponent<Image>();
        incImg.sprite = buttonBackground;
        Button incBtn = incBtnGO.AddComponent<Button>();

        RectTransform incBtnRect = incBtnGO.GetComponent<RectTransform>();
        incBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        incBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        incBtnRect.pivot = new Vector2(0.5f, 0.5f);
        incBtnRect.anchoredPosition = new Vector2(70f, 95f);
        incBtnRect.sizeDelta = new Vector2(40f, 35f);

        GameObject incTxtGO = new GameObject("Text", typeof(RectTransform));
        incTxtGO.transform.SetParent(incBtnGO.transform, false);
        Text incTxt = incTxtGO.AddComponent<Text>();
        incTxt.font = font;
        incTxt.text = ">";
        incTxt.fontSize = 18;
        incTxt.alignment = TextAnchor.MiddleCenter;
        incTxt.color = Color.white;

        RectTransform incTxtRect = incTxtGO.GetComponent<RectTransform>();
        incTxtRect.anchorMin = Vector2.zero;
        incTxtRect.anchorMax = Vector2.one;
        incTxtRect.sizeDelta = Vector2.zero;

        incBtn.onClick.AddListener(() => {
            float sens = ThirdPersonCamera.CameraSensitivityMultiplier;
            sens = Mathf.Min(3.0f, sens + 0.1f);
            ThirdPersonCamera.CameraSensitivityMultiplier = sens;
            PlayerPrefs.SetFloat("CameraSensitivityMultiplier", sens);
            PlayerPrefs.Save();
            sensValueTxt.text = sens.ToString("F1");
        });


        // ────────── 2. GRAPHICS QUALITY SECTION ──────────
        GameObject gfxLabelGO = new GameObject("GfxLabel", typeof(RectTransform));
        gfxLabelGO.transform.SetParent(dialogGO.transform, false);
        Text gfxLabel = gfxLabelGO.AddComponent<Text>();
        gfxLabel.font = font;
        gfxLabel.text = "GRAPHICS QUALITY PRESET";
        gfxLabel.fontSize = 14;
        gfxLabel.alignment = TextAnchor.MiddleCenter;
        gfxLabel.color = new Color(1f, 0.9f, 0.6f);
        Shadow gfxLabelShadow = gfxLabelGO.AddComponent<Shadow>();
        gfxLabelShadow.effectColor = Color.black;

        RectTransform gfxLabelRect = gfxLabelGO.GetComponent<RectTransform>();
        gfxLabelRect.anchorMin = new Vector2(0.5f, 0.5f);
        gfxLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
        gfxLabelRect.pivot = new Vector2(0.5f, 0.5f);
        gfxLabelRect.anchoredPosition = new Vector2(0f, 40f);
        gfxLabelRect.sizeDelta = new Vector2(300f, 20f);

        // Low preset button
        GameObject lowBtnGO = new GameObject("LowPresetBtn", typeof(RectTransform));
        lowBtnGO.transform.SetParent(dialogGO.transform, false);
        Image lowBtnImg = lowBtnGO.AddComponent<Image>();
        lowBtnImg.sprite = buttonBackground;
        Button lowBtn = lowBtnGO.AddComponent<Button>();

        RectTransform lowBtnRect = lowBtnGO.GetComponent<RectTransform>();
        lowBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        lowBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        lowBtnRect.pivot = new Vector2(0.5f, 0.5f);
        lowBtnRect.anchoredPosition = new Vector2(-80f, 5f);
        lowBtnRect.sizeDelta = new Vector2(140f, 35f);

        GameObject lowBtnTxtGO = new GameObject("Text", typeof(RectTransform));
        lowBtnTxtGO.transform.SetParent(lowBtnGO.transform, false);
        Text lowBtnTxt = lowBtnTxtGO.AddComponent<Text>();
        lowBtnTxt.font = font;
        lowBtnTxt.text = "LOW (MAX FPS)";
        lowBtnTxt.fontSize = 13;
        lowBtnTxt.alignment = TextAnchor.MiddleCenter;
        lowBtnTxt.color = Color.white;

        RectTransform lowBtnTxtRect = lowBtnTxtGO.GetComponent<RectTransform>();
        lowBtnTxtRect.anchorMin = Vector2.zero;
        lowBtnTxtRect.anchorMax = Vector2.one;
        lowBtnTxtRect.sizeDelta = Vector2.zero;

        // High preset button
        GameObject highBtnGO = new GameObject("HighPresetBtn", typeof(RectTransform));
        highBtnGO.transform.SetParent(dialogGO.transform, false);
        Image highBtnImg = highBtnGO.AddComponent<Image>();
        highBtnImg.sprite = buttonBackground;
        Button highBtn = highBtnGO.AddComponent<Button>();

        RectTransform highBtnRect = highBtnGO.GetComponent<RectTransform>();
        highBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        highBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        highBtnRect.pivot = new Vector2(0.5f, 0.5f);
        highBtnRect.anchoredPosition = new Vector2(80f, 5f);
        highBtnRect.sizeDelta = new Vector2(140f, 35f);

        GameObject highBtnTxtGO = new GameObject("Text", typeof(RectTransform));
        highBtnTxtGO.transform.SetParent(highBtnGO.transform, false);
        Text highBtnTxt = highBtnTxtGO.AddComponent<Text>();
        highBtnTxt.font = font;
        highBtnTxt.text = "HIGH (MAX QUALITY)";
        highBtnTxt.fontSize = 13;
        highBtnTxt.alignment = TextAnchor.MiddleCenter;
        highBtnTxt.color = Color.white;

        RectTransform highBtnTxtRect = highBtnTxtGO.GetComponent<RectTransform>();
        highBtnTxtRect.anchorMin = Vector2.zero;
        highBtnTxtRect.anchorMax = Vector2.one;
        highBtnTxtRect.sizeDelta = Vector2.zero;

        // Setup highlights
        int currentPreset = PlayerPrefs.GetInt("GraphicsPreset", 1);
        if (currentPreset == 0)
        {
            lowBtnImg.color = new Color(0.3f, 1f, 0.3f, 1f);
            highBtnImg.color = Color.white;
        }
        else
        {
            highBtnImg.color = new Color(0.3f, 1f, 0.3f, 1f);
            lowBtnImg.color = Color.white;
        }

        lowBtn.onClick.AddListener(() => {
            ApplyGraphicsPreset(0);
            PlayerPrefs.SetInt("GraphicsPreset", 0);
            PlayerPrefs.Save();
            lowBtnImg.color = new Color(0.3f, 1f, 0.3f, 1f);
            highBtnImg.color = Color.white;
        });

        highBtn.onClick.AddListener(() => {
            ApplyGraphicsPreset(1);
            PlayerPrefs.SetInt("GraphicsPreset", 1);
            PlayerPrefs.Save();
            highBtnImg.color = new Color(0.3f, 1f, 0.3f, 1f);
            lowBtnImg.color = Color.white;
        });


        // ────────── 3. STANDARD NAVIGATION BUTTONS ──────────
        // Resume Button
        GameObject resumeBtnGO = new GameObject("ResumeButton", typeof(RectTransform));
        resumeBtnGO.transform.SetParent(dialogGO.transform, false);
        Image resumeBtnImg = resumeBtnGO.AddComponent<Image>();
        resumeBtnImg.sprite = buttonBackground;
        Button resumeBtn = resumeBtnGO.AddComponent<Button>();

        RectTransform resumeBtnRect = resumeBtnGO.GetComponent<RectTransform>();
        resumeBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        resumeBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        resumeBtnRect.pivot = new Vector2(0.5f, 0.5f);
        resumeBtnRect.anchoredPosition = new Vector2(0f, -60f);
        resumeBtnRect.sizeDelta = new Vector2(180f, 40f);

        GameObject resumeBtnTextGO = new GameObject("Text", typeof(RectTransform));
        resumeBtnTextGO.transform.SetParent(resumeBtnGO.transform, false);
        Text resumeBtnTxt = resumeBtnTextGO.AddComponent<Text>();
        resumeBtnTxt.font = font;
        resumeBtnTxt.text = "RESUME";
        resumeBtnTxt.fontSize = 16;
        resumeBtnTxt.alignment = TextAnchor.MiddleCenter;
        resumeBtnTxt.color = Color.white;

        RectTransform resumeBtnTextRect = resumeBtnTextGO.GetComponent<RectTransform>();
        resumeBtnTextRect.anchorMin = Vector2.zero;
        resumeBtnTextRect.anchorMax = Vector2.one;
        resumeBtnTextRect.sizeDelta = Vector2.zero;

        resumeBtn.onClick.AddListener(ResumeGame);

        // Restart Button
        GameObject restartBtnGO = new GameObject("RestartButton", typeof(RectTransform));
        restartBtnGO.transform.SetParent(dialogGO.transform, false);
        Image restartBtnImg = restartBtnGO.AddComponent<Image>();
        restartBtnImg.sprite = buttonBackground;
        Button restartBtn = restartBtnGO.AddComponent<Button>();

        RectTransform restartBtnRect = restartBtnGO.GetComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        restartBtnRect.pivot = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchoredPosition = new Vector2(0f, -115f);
        restartBtnRect.sizeDelta = new Vector2(180f, 40f);

        GameObject restartBtnTextGO = new GameObject("Text", typeof(RectTransform));
        restartBtnTextGO.transform.SetParent(restartBtnGO.transform, false);
        Text restartBtnTxt = restartBtnTextGO.AddComponent<Text>();
        restartBtnTxt.font = font;
        restartBtnTxt.text = "RESTART";
        restartBtnTxt.fontSize = 16;
        restartBtnTxt.alignment = TextAnchor.MiddleCenter;
        restartBtnTxt.color = Color.white;

        RectTransform restartBtnTextRect = restartBtnTextGO.GetComponent<RectTransform>();
        restartBtnTextRect.anchorMin = Vector2.zero;
        restartBtnTextRect.anchorMax = Vector2.one;
        restartBtnTextRect.sizeDelta = Vector2.zero;

        restartBtn.onClick.AddListener(() => {
            IsGamePaused = false;
            Time.timeScale = 1f;
            RestartGame();
        });

        // Exit Button
        GameObject exitBtnGO = new GameObject("ExitButton", typeof(RectTransform));
        exitBtnGO.transform.SetParent(dialogGO.transform, false);
        Image exitBtnImg = exitBtnGO.AddComponent<Image>();
        exitBtnImg.sprite = buttonBackground;
        Button exitBtn = exitBtnGO.AddComponent<Button>();

        RectTransform exitBtnRect = exitBtnGO.GetComponent<RectTransform>();
        exitBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        exitBtnRect.pivot = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchoredPosition = new Vector2(0f, -170f);
        exitBtnRect.sizeDelta = new Vector2(180f, 40f);

        GameObject exitBtnTextGO = new GameObject("Text", typeof(RectTransform));
        exitBtnTextGO.transform.SetParent(exitBtnGO.transform, false);
        Text exitBtnTxt = exitBtnTextGO.AddComponent<Text>();
        exitBtnTxt.font = font;
        exitBtnTxt.text = "EXIT";
        exitBtnTxt.fontSize = 16;
        exitBtnTxt.alignment = TextAnchor.MiddleCenter;
        exitBtnTxt.color = Color.white;

        RectTransform exitBtnTextRect = exitBtnTextGO.GetComponent<RectTransform>();
        exitBtnTextRect.anchorMin = Vector2.zero;
        exitBtnTextRect.anchorMax = Vector2.one;
        exitBtnTextRect.sizeDelta = Vector2.zero;

        exitBtn.onClick.AddListener(ExitGame);
    }

    void CreateLevelUpPopup()
    {
        levelUpPopup = new GameObject("LevelUpPopup", typeof(RectTransform));
        levelUpPopup.transform.SetParent(mainCanvas.transform, false);
        RectTransform popRect = levelUpPopup.GetComponent<RectTransform>();
        popRect.anchorMin = new Vector2(0.5f, 0.7f);
        popRect.anchorMax = new Vector2(0.5f, 0.7f);
        popRect.pivot = new Vector2(0.5f, 0.5f);
        popRect.anchoredPosition = Vector2.zero;
        popRect.sizeDelta = new Vector2(600f, 120f);

        Image popImg = levelUpPopup.AddComponent<Image>();
        popImg.sprite = frameMid;
        popImg.type = Image.Type.Sliced;
        popImg.color = new Color(1f, 0.9f, 0.5f, 0.95f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(levelUpPopup.transform, false);
        levelUpText = textGO.AddComponent<Text>();
        levelUpText.font = GetDefaultFont();
        levelUpText.text = "LEVEL UP!\nULTIMATE READY (U)";
        levelUpText.fontSize = 28;
        levelUpText.alignment = TextAnchor.MiddleCenter;
        levelUpText.color = new Color(0.9f, 0.1f, 0f, 1f); // Vibrant gold-red
        
        Shadow textShadow = textGO.AddComponent<Shadow>();
        textShadow.effectColor = Color.yellow;
        textShadow.effectDistance = new Vector2(2f, -2f);

        Outline textOutline = textGO.AddComponent<Outline>();
        textOutline.effectColor = new Color(0.1f, 0.05f, 0f, 0.9f);
        textOutline.effectDistance = new Vector2(2f, -2f);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        levelUpPopup.SetActive(false);
    }

    void CreateGameOverPanel()
    {
        gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform));
        gameOverPanel.transform.SetParent(mainCanvas.transform, false);
        RectTransform panelRect = gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelBg = gameOverPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.75f);

        // Dialog Box
        GameObject dialogGO = new GameObject("DialogBox", typeof(RectTransform));
        dialogGO.transform.SetParent(gameOverPanel.transform, false);
        Image dialogImg = dialogGO.AddComponent<Image>();
        if (frameBig != null)
        {
            dialogImg.sprite = frameBig;
            dialogImg.type = Image.Type.Sliced;
        }
        else
        {
            dialogImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        }

        RectTransform dialogRect = dialogGO.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero; // Center!
        dialogRect.sizeDelta = new Vector2(450f, 320f);

        Font font = GetDefaultFont();

        // Title
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(dialogGO.transform, false);
        Text titleTxt = titleGO.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.text = "YOU DIED";
        titleTxt.fontSize = 36;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = Color.red;
        Shadow titleShadow = titleGO.AddComponent<Shadow>();
        titleShadow.effectColor = Color.black;

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.7f);
        titleRect.anchorMax = new Vector2(1f, 0.95f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;

        // 1. Restart Button
        GameObject restartBtnGO = new GameObject("RestartButton", typeof(RectTransform));
        restartBtnGO.transform.SetParent(dialogGO.transform, false);
        Image restartBtnImg = restartBtnGO.AddComponent<Image>();
        restartBtnImg.sprite = buttonBackground;
        Button btn = restartBtnGO.AddComponent<Button>();

        RectTransform btnRect = restartBtnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0f, -10f);
        btnRect.sizeDelta = new Vector2(200f, 50f);

        GameObject btnTextGO = new GameObject("Text", typeof(RectTransform));
        btnTextGO.transform.SetParent(restartBtnGO.transform, false);
        Text btnTxt = btnTextGO.AddComponent<Text>();
        btnTxt.font = font;
        btnTxt.text = "RESTART";
        btnTxt.fontSize = 20;
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color = Color.white;

        RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(RestartGame);

        // 2. Exit Button
        GameObject exitBtnGO = new GameObject("ExitButton", typeof(RectTransform));
        exitBtnGO.transform.SetParent(dialogGO.transform, false);
        Image exitBtnImg = exitBtnGO.AddComponent<Image>();
        exitBtnImg.sprite = buttonBackground;
        Button exitBtn = exitBtnGO.AddComponent<Button>();

        RectTransform exitBtnRect = exitBtnGO.GetComponent<RectTransform>();
        exitBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        exitBtnRect.pivot = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchoredPosition = new Vector2(0f, -70f);
        exitBtnRect.sizeDelta = new Vector2(200f, 50f);

        GameObject exitBtnTextGO = new GameObject("Text", typeof(RectTransform));
        exitBtnTextGO.transform.SetParent(exitBtnGO.transform, false);
        Text exitBtnTxt = exitBtnTextGO.AddComponent<Text>();
        exitBtnTxt.font = font;
        exitBtnTxt.text = "EXIT";
        exitBtnTxt.fontSize = 18;
        exitBtnTxt.alignment = TextAnchor.MiddleCenter;
        exitBtnTxt.color = Color.white;

        RectTransform exitBtnTextRect = exitBtnTextGO.GetComponent<RectTransform>();
        exitBtnTextRect.anchorMin = Vector2.zero;
        exitBtnTextRect.anchorMax = Vector2.one;
        exitBtnTextRect.sizeDelta = Vector2.zero;

        exitBtn.onClick.AddListener(ExitGame);

        gameOverPanel.SetActive(false);
    }

    void CreateVictoryPanel()
    {
        victoryPanel = new GameObject("VictoryPanel", typeof(RectTransform));
        victoryPanel.transform.SetParent(mainCanvas.transform, false);
        RectTransform panelRect = victoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        Image panelBg = victoryPanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0.2f, 0f, 0.7f);

        // Dialog Box
        GameObject dialogGO = new GameObject("DialogBox", typeof(RectTransform));
        dialogGO.transform.SetParent(victoryPanel.transform, false);
        Image dialogImg = dialogGO.AddComponent<Image>();
        if (frameBig != null)
        {
            dialogImg.sprite = frameBig;
            dialogImg.type = Image.Type.Sliced;
        }
        else
        {
            dialogImg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        }

        RectTransform dialogRect = dialogGO.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero; // Center!
        dialogRect.sizeDelta = new Vector2(500f, 350f);

        Font font = GetDefaultFont();

        // Title
        GameObject titleGO = new GameObject("TitleText", typeof(RectTransform));
        titleGO.transform.SetParent(dialogGO.transform, false);
        Text titleTxt = titleGO.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.text = "VICTORY!\nYOU SAVED THE ISLAND";
        titleTxt.fontSize = 32;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = Color.yellow;
        Shadow titleShadow = titleGO.AddComponent<Shadow>();
        titleShadow.effectColor = Color.black;

        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.65f);
        titleRect.anchorMax = new Vector2(1f, 0.95f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = Vector2.zero;

        // 1. Play Again Button
        GameObject restartBtnGO = new GameObject("PlayAgainButton", typeof(RectTransform));
        restartBtnGO.transform.SetParent(dialogGO.transform, false);
        Image restartBtnImg = restartBtnGO.AddComponent<Image>();
        restartBtnImg.sprite = buttonBackground;
        Button btn = restartBtnGO.AddComponent<Button>();

        RectTransform btnRect = restartBtnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0f, -10f);
        btnRect.sizeDelta = new Vector2(220f, 60f);

        GameObject btnTextGO = new GameObject("Text", typeof(RectTransform));
        btnTextGO.transform.SetParent(restartBtnGO.transform, false);
        Text btnTxt = btnTextGO.AddComponent<Text>();
        btnTxt.font = font;
        btnTxt.text = "PLAY AGAIN";
        btnTxt.fontSize = 20;
        btnTxt.alignment = TextAnchor.MiddleCenter;
        btnTxt.color = Color.white;

        RectTransform btnTextRect = btnTextGO.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(RestartGame);

        // 2. Exit Button
        GameObject exitBtnGO = new GameObject("ExitButton", typeof(RectTransform));
        exitBtnGO.transform.SetParent(dialogGO.transform, false);
        Image exitBtnImg = exitBtnGO.AddComponent<Image>();
        exitBtnImg.sprite = buttonBackground;
        Button exitBtn = exitBtnGO.AddComponent<Button>();

        RectTransform exitBtnRect = exitBtnGO.GetComponent<RectTransform>();
        exitBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        exitBtnRect.pivot = new Vector2(0.5f, 0.5f);
        exitBtnRect.anchoredPosition = new Vector2(0f, -80f);
        exitBtnRect.sizeDelta = new Vector2(220f, 60f);

        GameObject exitBtnTextGO = new GameObject("Text", typeof(RectTransform));
        exitBtnTextGO.transform.SetParent(exitBtnGO.transform, false);
        Text exitBtnTxt = exitBtnTextGO.AddComponent<Text>();
        exitBtnTxt.font = font;
        exitBtnTxt.text = "EXIT";
        exitBtnTxt.fontSize = 20;
        exitBtnTxt.alignment = TextAnchor.MiddleCenter;
        exitBtnTxt.color = Color.white;

        RectTransform exitBtnTextRect = exitBtnTextGO.GetComponent<RectTransform>();
        exitBtnTextRect.anchorMin = Vector2.zero;
        exitBtnTextRect.anchorMax = Vector2.one;
        exitBtnTextRect.sizeDelta = Vector2.zero;

        exitBtn.onClick.AddListener(ExitGame);

        victoryPanel.SetActive(false);
    }

    // ─── Interaction Handlers ───────────────────────────────────────────────────

    public void PauseGame()
    {
        IsGamePaused = true;
        Time.timeScale = 0f;

        if (hudPanel != null) hudPanel.SetActive(false);
        if (pausePanel != null) {
            pausePanel.SetActive(true);
            // Ensure pausePanel is drawn on top of all other elements in Canvas
            pausePanel.transform.SetAsLastSibling();
        }
    }

    public void ResumeGame()
    {
        IsGamePaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null) pausePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);
    }

    public void TriggerLevelUpPopup()
    {
        if (isLevelUpPopupActive) return;
        StartCoroutine(LevelUpSequence());
    }

    private IEnumerator LevelUpSequence()
    {
        isLevelUpPopupActive = true;
        if (levelUpPopup != null) levelUpPopup.SetActive(true);
        yield return new WaitForSeconds(2.0f);
        if (levelUpPopup != null) levelUpPopup.SetActive(false);
        isLevelUpPopupActive = false;
    }

    private void OnUltimateClicked()
    {
        if (XPManager.Instance != null && XPManager.Instance.IsUltimateReady)
        {
            XPManager.Instance.ActivateUltimate();
            Debug.Log("[GameUIManager] Ultimate Attack button clicked!");
        }
    }

    private void OnPlayerDied()
    {
        StartCoroutine(ShowGameOverDelayed());
    }

    private IEnumerator ShowGameOverDelayed()
    {
        // Wait 2.5 seconds at normal timescale for death animation to complete
        yield return new WaitForSeconds(2.5f);

        IsGamePlaying = false;
        Time.timeScale = 0f;
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }
    }

    private void OnBossDefeated()
    {
        StartCoroutine(ShowVictoryDelayed());
    }

    private IEnumerator ShowVictoryDelayed()
    {
        // Wait 3.0 seconds to witness the boss's defeat animation
        yield return new WaitForSeconds(3.0f);

        IsGamePlaying = false;
        Time.timeScale = 0f;
        if (hudPanel != null) hudPanel.SetActive(false);
        if (victoryPanel != null) {
            victoryPanel.SetActive(true);
            victoryPanel.transform.SetAsLastSibling();
        }
    }

    private void RestartGame()
    {
        Debug.Log("[GameUIManager] Restarting scene...");
        IsGamePlaying = true;
        IsGamePaused = false;
        Time.timeScale = 1f; // Ensure time scale is restored on scene reload

        if (XPManager.Instance != null)
        {
            XPManager.Instance.ResetState();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private void ExitGame()
    {
        Debug.Log("[GameUIManager] Exiting game...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Applies aggressive runtime graphics optimizations for LOW preset,
    /// or restores full quality for HIGH preset.
    /// This goes beyond QualitySettings levels by directly adjusting
    /// shadow distance, LOD bias, render scale, and shader settings at runtime.
    /// </summary>
    private void ApplyGraphicsPreset(int preset)
    {
        if (preset == 0)
        {
            // ── LOW PRESET: Maximum FPS ──
            QualitySettings.SetQualityLevel(0, true);

            // Disable all real-time shadows
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowDistance = 0f;

            // Reduce pixel light count (use vertex lighting)
            QualitySettings.pixelLightCount = 0;

            // Lower LOD aggressively — use lower detail meshes sooner
            QualitySettings.lodBias = 0.3f;
            QualitySettings.maximumLODLevel = 1;

            // Disable soft vegetation
            QualitySettings.softVegetation = false;

            // Lower skin weights for skeletal animation (2 bones instead of 4)
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // Reduce texture quality (use half-res textures)
            QualitySettings.globalTextureMipmapLimit = 1;

            // Disable anisotropic filtering
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // Reduce particle raycast budget
            QualitySettings.particleRaycastBudget = 16;

            // Lower resolution render scale factor
            QualitySettings.resolutionScalingFixedDPIFactor = 0.7f;

            Debug.Log("[GameUIManager] Applied LOW graphics preset — shadows OFF, reduced resolution, lower LOD.");
        }
        else
        {
            // ── HIGH PRESET: Maximum Quality ──
            QualitySettings.SetQualityLevel(Mathf.Max(0, QualitySettings.names.Length - 1), true);

            // Enable shadows
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 40f;
            QualitySettings.shadowResolution = ShadowResolution.Medium;

            // Restore pixel lights
            QualitySettings.pixelLightCount = 2;

            // Normal LOD
            QualitySettings.lodBias = 1.0f;
            QualitySettings.maximumLODLevel = 0;

            // Enable soft vegetation
            QualitySettings.softVegetation = true;

            // Full skin weights
            QualitySettings.skinWeights = SkinWeights.FourBones;

            // Full texture quality
            QualitySettings.globalTextureMipmapLimit = 0;

            // Enable anisotropic filtering
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // Normal particle budget
            QualitySettings.particleRaycastBudget = 256;

            // Full resolution
            QualitySettings.resolutionScalingFixedDPIFactor = 1.0f;

            Debug.Log("[GameUIManager] Applied HIGH graphics preset — shadows ON, full resolution, normal LOD.");
        }
    }
}
