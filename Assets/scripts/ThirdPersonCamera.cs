using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;

    public float distance = 4f;
    public float height = 1.6f;
    public float mouseSensitivity = 120f;
    public float smoothSpeed = 30f;

    public float minPitch = -15f;
    public float maxPitch = 35f;

    public static float CameraSensitivityMultiplier = 1f;

    private float yaw;
    private float pitch = 10f;

    private InputAction lookAction;

    // Mobile touch tracking
    private int activeLookTouchId = -1;
    private Vector2 lastTouchPosition;
    private bool isMobilePlatform = false;

    // Smoothing for buttery-smooth mobile camera
    private float smoothedDeltaX = 0f;
    private float smoothedDeltaY = 0f;
    private const float TOUCH_SMOOTH_SPEED = 15f; // Higher = more responsive, lower = smoother
    private const float TOUCH_SCALE = 0.12f;      // Touch pixel-to-rotation scale

    void Start()
    {
        // Load saved sensitivity
        CameraSensitivityMultiplier = PlayerPrefs.GetFloat("CameraSensitivityMultiplier", 1.0f);

        // Detect if we're on a mobile platform at runtime
#if UNITY_ANDROID || UNITY_IOS
        isMobilePlatform = true;
#else
        isMobilePlatform = false;
#endif

        // Auto-find player if target not set
        if (target == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (target == null)
        {
            Debug.LogError("[ThirdPersonCamera] No target found! Tag your player as 'Player'.");
            return;
        }

        // Only setup lookAction for desktop — on mobile we use pure touch input
        if (!isMobilePlatform)
        {
            PlayerInput playerInput = target.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                lookAction = playerInput.actions["Look"];
                lookAction.Enable();
            }
        }

        yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float rawDeltaX = 0f;
        float rawDeltaY = 0f;

        bool handledTouch = false;

        // ── MOBILE: Pure touch-based camera control ──
        if (isMobilePlatform)
        {
            // Try new Input System touchscreen first
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    int id = touch.touchId.ReadValue();
                    if (touch.press.wasPressedThisFrame)
                    {
                        Vector2 startPos = touch.startPosition.ReadValue();
                        if (startPos.x > Screen.width * 0.35f)
                        {
                            if (UnityEngine.EventSystems.EventSystem.current != null &&
                                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(id))
                            {
                                continue;
                            }
                            activeLookTouchId = id;
                            lastTouchPosition = touch.position.ReadValue();
                            // Reset smoothing on new touch to prevent carry-over momentum
                            smoothedDeltaX = 0f;
                            smoothedDeltaY = 0f;
                        }
                    }

                    if (touch.press.isPressed && id == activeLookTouchId)
                    {
                        Vector2 currentPos = touch.position.ReadValue();
                        Vector2 delta = currentPos - lastTouchPosition;

                        rawDeltaX = delta.x * TOUCH_SCALE;
                        rawDeltaY = delta.y * TOUCH_SCALE;

                        lastTouchPosition = currentPos;
                        handledTouch = true;
                        break;
                    }

                    if (touch.press.wasReleasedThisFrame && id == activeLookTouchId)
                    {
                        activeLookTouchId = -1;
                    }
                }
            }

            // Fallback to legacy touch API
            if (!handledTouch && Input.touchCount > 0)
            {
                foreach (Touch touch in Input.touches)
                {
                    if (touch.phase == UnityEngine.TouchPhase.Began)
                    {
                        if (touch.position.x > Screen.width * 0.35f)
                        {
                            if (UnityEngine.EventSystems.EventSystem.current != null &&
                                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                            {
                                continue;
                            }
                            activeLookTouchId = touch.fingerId;
                            lastTouchPosition = touch.position;
                            smoothedDeltaX = 0f;
                            smoothedDeltaY = 0f;
                        }
                    }

                    if (touch.fingerId == activeLookTouchId)
                    {
                        if (touch.phase == UnityEngine.TouchPhase.Moved)
                        {
                            Vector2 delta = touch.position - lastTouchPosition;
                            rawDeltaX = delta.x * TOUCH_SCALE;
                            rawDeltaY = delta.y * TOUCH_SCALE;
                            lastTouchPosition = touch.position;
                            handledTouch = true;
                        }
                        else if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
                        {
                            activeLookTouchId = -1;
                        }
                        break;
                    }
                }
            }

            // ── SMOOTH the touch deltas using exponential interpolation ──
            // This creates a buttery-smooth camera feel with slight natural momentum
            float lerpFactor = 1f - Mathf.Exp(-TOUCH_SMOOTH_SPEED * Time.deltaTime);
            smoothedDeltaX = Mathf.Lerp(smoothedDeltaX, rawDeltaX, lerpFactor);
            smoothedDeltaY = Mathf.Lerp(smoothedDeltaY, rawDeltaY, lerpFactor);

            // Apply smoothed values with sensitivity
            float finalX = smoothedDeltaX * CameraSensitivityMultiplier;
            float finalY = smoothedDeltaY * CameraSensitivityMultiplier;

            yaw += finalX;
            pitch -= finalY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
        else
        {
            // ── DESKTOP: Use Input System lookAction (mouse/gamepad right stick) ──
            Vector2 lookInput = Vector2.zero;
            if (lookAction != null)
                lookInput = lookAction.ReadValue<Vector2>();

            float deltaX = lookInput.x * mouseSensitivity * Time.deltaTime * CameraSensitivityMultiplier;
            float deltaY = lookInput.y * mouseSensitivity * Time.deltaTime * CameraSensitivityMultiplier;

            yaw += deltaX;
            pitch -= deltaY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 focusPoint = target.position + Vector3.up * height;
        Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;

        // Smooth camera position for silky follow
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
        transform.LookAt(focusPoint);
    }
}