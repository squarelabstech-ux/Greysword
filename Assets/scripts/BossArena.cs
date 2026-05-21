using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// BossArena — Manages showing the "Summon Boss" button when player reaches the stairs,
/// checks for 7 skulls, and summons the boss.
/// </summary>
public class BossArena : MonoBehaviour
{
    private bool playerInArena = false;
    private GameObject canvasGO;
    private GameObject summonButtonGO;
    private GameObject popupTextGO;
    private Text popupText;

    private BossSummonManager bossManager;
    private Transform playerTransform;

    // Target stair area point and detection radius
    private readonly Vector3 stairPoint = new Vector3(355.372f, 9.174f, 594.795f);
    private const float detectionRadius = 8f;

    void Start()
    {
        // Find player transform
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        bossManager = FindObjectOfType<BossSummonManager>();

        CreateUI();
    }

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

    void OnDestroy()
    {
        if (canvasGO != null)
        {
            Destroy(canvasGO);
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            return;
        }

        if (bossManager == null)
        {
            bossManager = FindObjectOfType<BossSummonManager>();
        }

        // Distance check instead of physical trigger collider for 100% reliability
        float dist = Vector3.Distance(playerTransform.position, stairPoint);
        bool inRange = dist <= detectionRadius;

        if (inRange && !playerInArena)
        {
            playerInArena = true;
            ShowPopup("Boss Summon Place Found");
            if (bossManager != null && !bossManager.IsBossAlive)
            {
                summonButtonGO.SetActive(true);
            }
        }
        else if (!inRange && playerInArena)
        {
            playerInArena = false;
            summonButtonGO.SetActive(false);
            if (bossManager != null && bossManager.IsBossAlive)
            {
                bossManager.ResetBoss();
            }
        }
    }

    void CreateUI()
    {
        // Create Canvas with RectTransform
        canvasGO = new GameObject("BossArenaCanvas", typeof(RectTransform));
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Get sprites and icons from GameUIManager to maintain unified style
        Sprite btnSprite = null;
        Sprite frameSprite = null;
        Sprite skullIcon = null;
        if (GameUIManager.Instance != null)
        {
            btnSprite = GameUIManager.Instance.buttonBackground;
            frameSprite = GameUIManager.Instance.frameMid;
            skullIcon = GameUIManager.Instance.skullIcon;
        }

        // Create Summon Boss Button
        summonButtonGO = new GameObject("SummonBossButton", typeof(RectTransform));
        summonButtonGO.transform.SetParent(canvasGO.transform, false);
        Image btnImage = summonButtonGO.AddComponent<Image>();
        if (btnSprite != null)
        {
            btnImage.sprite = btnSprite;
            btnImage.type = Image.Type.Sliced;
            btnImage.color = Color.white;
        }
        else
        {
            btnImage.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        }

        Button btn = summonButtonGO.AddComponent<Button>();
        
        RectTransform btnRect = summonButtonGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.15f);
        btnRect.anchorMax = new Vector2(0.5f, 0.15f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = new Vector2(0f, 50f);
        btnRect.sizeDelta = new Vector2(300f, 70f);

        // Content container inside button to align skull icon and text
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(summonButtonGO.transform, false);
        RectTransform contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.sizeDelta = Vector2.zero;

        // Skull icon inside button
        if (skullIcon != null)
        {
            GameObject iconGO = new GameObject("SkullIcon", typeof(RectTransform));
            iconGO.transform.SetParent(contentGO.transform, false);
            Image iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = skullIcon;

            RectTransform iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.1f, 0.5f);
            iconRect.anchorMax = new Vector2(0.1f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(32f, 32f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(contentGO.transform, false);
        Text text = textGO.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.text = "SUMMON BOSS (7 Skulls)";
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        Shadow txtShadow = textGO.AddComponent<Shadow>();
        txtShadow.effectColor = Color.black;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.2f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.sizeDelta = Vector2.zero;

        btn.onClick.AddListener(OnSummonClicked);

        // Create Popup Frame & Text
        popupTextGO = new GameObject("BossPopupFrame", typeof(RectTransform));
        popupTextGO.transform.SetParent(canvasGO.transform, false);
        
        Image popupBg = popupTextGO.AddComponent<Image>();
        if (frameSprite != null)
        {
            popupBg.sprite = frameSprite;
            popupBg.type = Image.Type.Sliced;
            popupBg.color = new Color(1f, 0.9f, 0.5f, 0.95f); // Gold tint
        }
        else
        {
            popupBg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
        }

        RectTransform popupRect = popupTextGO.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.7f);
        popupRect.anchorMax = new Vector2(0.5f, 0.7f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = Vector2.zero;
        popupRect.sizeDelta = new Vector2(500f, 80f);

        GameObject popupLabelGO = new GameObject("Text", typeof(RectTransform));
        popupLabelGO.transform.SetParent(popupTextGO.transform, false);
        popupText = popupLabelGO.AddComponent<Text>();
        popupText.font = GetDefaultFont();
        popupText.text = "";
        popupText.fontSize = 22;
        popupText.alignment = TextAnchor.MiddleCenter;
        popupText.color = new Color(0.6f, 0.1f, 0f, 1f); // Deep red text
        Shadow popupShadow = popupLabelGO.AddComponent<Shadow>();
        popupShadow.effectColor = Color.yellow;
        
        RectTransform popupLabelRect = popupLabelGO.GetComponent<RectTransform>();
        popupLabelRect.anchorMin = Vector2.zero;
        popupLabelRect.anchorMax = Vector2.one;
        popupLabelRect.sizeDelta = Vector2.zero;

        summonButtonGO.SetActive(false);
        popupTextGO.SetActive(false);
    }

    void OnSummonClicked()
    {
        if (bossManager == null || bossManager.IsBossAlive) return;

        int skullsNeeded = 7;
        if (SkullCounter.Instance != null)
        {
            if (SkullCounter.Instance.SkullCount >= skullsNeeded)
            {
                SkullCounter.Instance.DeductSkulls(skullsNeeded);
                summonButtonGO.SetActive(false);
                bossManager.SummonBoss();
            }
            else
            {
                ShowPopup($"Need {skullsNeeded} skulls to summon boss");
            }
        }
        else
        {
            // If SkullCounter is missing in testing, let it summon anyway to prevent blocking
            summonButtonGO.SetActive(false);
            bossManager.SummonBoss();
        }
    }

    void ShowPopup(string msg)
    {
        if (popupText == null) return;
        popupText.text = msg;
        popupTextGO.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HidePopup());
    }

    IEnumerator HidePopup()
    {
        yield return new WaitForSeconds(3f);
        popupTextGO.SetActive(false);
    }
}
