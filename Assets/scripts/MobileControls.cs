using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MobileControls — Tracks on-screen virtual joystick and button inputs for mobile.
/// Supports both touch on mobile and mouse clicks/drags in the Editor for easy testing.
/// </summary>
public class MobileControls : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public static MobileControls Instance { get; private set; }

    [Header("Joystick Settings")]
    public RectTransform joystickBackground;
    public RectTransform joystickHandle;
    public float joystickRange = 75f;

    [Header("Buttons")]
    public Button attackButton;
    public Button jumpButton;
    public Button sprintButton;

    // Public API properties read by PlayerMovement and PlayerCombat
    public Vector2 MoveInput { get; private set; } = Vector2.zero;
    public bool JumpInput { get; private set; } = false;
    public bool SprintInput { get; private set; } = false;
    public bool AttackInput { get; private set; } = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (attackButton != null)
        {
            attackButton.onClick.AddListener(() => StartCoroutine(TriggerAttackPulse()));
        }
        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(() => StartCoroutine(TriggerJumpPulse()));
        }
        if (sprintButton != null)
        {
            sprintButton.onClick.AddListener(ToggleSprint);
        }
    }

    void LateUpdate()
    {
        // Clear frame-triggered inputs after update cycle
        JumpInput = false;
        AttackInput = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (joystickBackground == null) return;
        
        // If pointer is within joystick background, start dragging
        if (RectTransformUtility.RectangleContainsScreenPoint(joystickBackground, eventData.position, eventData.pressEventCamera))
        {
            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystickBackground == null || joystickHandle == null) return;

        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBackground, eventData.position, eventData.pressEventCamera, out localPos))
        {
            localPos = Vector2.ClampMagnitude(localPos, joystickRange);
            joystickHandle.anchoredPosition = localPos;
            MoveInput = localPos / joystickRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
        MoveInput = Vector2.zero;
    }

    private void ToggleSprint()
    {
        SprintInput = !SprintInput;
        Image img = sprintButton.GetComponent<Image>();
        if (img != null)
        {
            // Toggle highlight color
            img.color = SprintInput ? new Color(0.5f, 1f, 0.5f, 1f) : Color.white;
        }
    }

    private System.Collections.IEnumerator TriggerAttackPulse()
    {
        AttackInput = true;
        yield return null;
        AttackInput = false;
    }

    private System.Collections.IEnumerator TriggerJumpPulse()
    {
        JumpInput = true;
        yield return null;
        JumpInput = false;
    }
}
