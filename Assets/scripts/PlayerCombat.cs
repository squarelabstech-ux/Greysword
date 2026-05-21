using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerCombat — Handles melee attacks against zombies.
///
/// SETUP:
/// - Attach to Player root GameObject.
/// - Set attackDamage, attackRange, attackCooldown in Inspector.
/// - Requires PlayerInput with an "Attack" binding (e.g., Left Mouse or mobile button).
/// - Assign attackPoint (empty child Transform at player's fist/weapon location).
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon System")]
    [Tooltip("If false, the player is unarmed and cannot attack. WeaponPickup sets this to true.")]
    public bool hasWeapon = false;

    [Header("Combat Settings")]
    [Tooltip("Damage dealt per swing")]
    public float attackDamage = 20f;

    [Tooltip("Radius around attackPoint that hits zombies")]
    public float attackRange = 1.5f;

    [Tooltip("Seconds between attacks")]
    public float attackCooldown = 0.8f;

    [Tooltip("Transform placed at the attack origin (e.g., player's hand). If null, uses this transform.")]
    public Transform attackPoint;

    [Tooltip("Layer mask for zombie/enemy objects")]
    public LayerMask enemyLayers;

    [Header("Animator")]
    public Animator animator;
    [HideInInspector] public bool isAttacking = false;

    [Header("Ultimate Attack System")]
    [HideInInspector] public float ultimateAttackTimer = 0f;

    // ─── Private State ─────────────────────────────────────────────────────────

    private InputAction attackAction;
    private float       cooldownTimer = 0f;
    private bool        isDead        = false;
    private PlayerBehaviorTracker behaviorTracker;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        behaviorTracker = FindObjectOfType<PlayerBehaviorTracker>();

        // Auto-assign animator if not set in Inspector
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        var playerInput = GetComponent<PlayerInput>();
        try
        {
            attackAction = playerInput.actions["Attack"];
            attackAction.Enable();
        }
        catch
        {
            Debug.LogWarning("[PlayerCombat] No 'Attack' action found in Input Actions. Add it via the Input Actions asset.");
        }

        if (attackPoint == null)
            attackPoint = transform;

        PlayerHealth.OnPlayerDeath += () => isDead = true;
    }

    void Update()
    {
        if (isDead) return;
        if (!GameUIManager.IsGamePlaying || GameUIManager.IsGamePaused) return;
        
        // Cannot attack if the player hasn't picked up the sword yet
        if (!hasWeapon) return;

        if (ultimateAttackTimer > 0f)
            ultimateAttackTimer -= Time.deltaTime;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // Check attack input: Right mouse button or Attack Action (for mobile/UI compatibility)
        bool leftClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        
        bool wantsAttack = false;
        
        // If the Input System "Attack" action fired, only count it if it WASN'T the left mouse button
        // This ensures left click is strictly reserved for clicking UI buttons
        if (attackAction != null && attackAction.WasPressedThisFrame())
        {
            if (!leftClick) wantsAttack = true;
        }
        
        // Right click always attacks
        if (rightClick) wantsAttack = true;

        // Mobile Controls Attack Button support
        if (MobileControls.Instance != null && MobileControls.Instance.isActiveAndEnabled)
        {
            if (MobileControls.Instance.AttackInput) wantsAttack = true;
        }

        if (wantsAttack && cooldownTimer <= 0f)
        {
            PerformAttack();
            cooldownTimer = attackCooldown;
        }
    }

    // ─── Attack Logic ──────────────────────────────────────────────────────────

    void PerformAttack()
    {
        isAttacking = true;
        Invoke(nameof(ResetAttack), attackCooldown);

        Debug.Log($"[PlayerCombat] PerformAttack! hasWeapon={hasWeapon}, animator={(animator != null ? animator.name : "NULL")}");

        if (animator != null)
        {
            if (ultimateAttackTimer > 0f)
                animator.SetTrigger("UltimateAttack");
            else
                animator.SetTrigger("Attack");
        }
        else
            Debug.LogError("[PlayerCombat] Animator is NULL! Cannot play attack animation.");

        // Remove layer mask to ensure we hit the zombie regardless of layer settings
        // Also elevate the sphere by 1 unit to hit the torso, not just the feet
        Vector3 spherePos = attackPoint != null ? attackPoint.position : transform.position;
        Collider[] hitColliders = Physics.OverlapSphere(spherePos + Vector3.up * 1f, attackRange);
        bool hitSomething = false;

        foreach (Collider col in hitColliders)
        {
            ZombieHealth zh = col.GetComponentInParent<ZombieHealth>();
            if (zh != null && !zh.IsDead)
            {
                float damageToDeal = attackDamage;
                if (ultimateAttackTimer > 0f)
                {
                    damageToDeal = attackDamage * 3f;
                }

                zh.TakeDamage(damageToDeal);
                hitSomething = true;
                Debug.Log($"[PlayerCombat] Hit {col.gameObject.name} for {damageToDeal} damage.");
            }
        }

        // Track hit/miss for behavior analysis
        if (behaviorTracker != null)
            behaviorTracker.OnPlayerAttack(hitSomething);
    }

    void ResetAttack()
    {
        isAttacking = false;
    }

    // ─── Ultimate Ability ──────────────────────────────────────────────────────

    public void ActivateUltimateAttack()
    {
        ultimateAttackTimer = 20f;
        Debug.Log("[PlayerCombat] Ultimate Attack Activated! Changed animation and increased damage for 20 seconds.");

        // Force an immediate attack so the player doesn't have to click again to see the new animation
        if (cooldownTimer <= 0f)
        {
            PerformAttack();
            cooldownTimer = attackCooldown;
        }
    }

    // ─── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
