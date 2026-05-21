using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ZombieAI — Simple, bulletproof zombie behavior.
/// Chase player when in range, attack when close, wander when player is far.
/// Will NEVER get stuck.
/// </summary>
public class ZombieAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 50f;
    public float chaseRange     = 80f;
    public float attackRange    = 2.2f;

    [Header("Movement")]
    public float wanderSpeed = 1.5f;
    public float chaseSpeed  = 4.0f;
    public float wanderRadius = 10f;
    public float wanderWaitTime = 3f;

    [Header("Attack")]
    public float attackDamage     = 10f;
    public float attackCooldown   = 2f;

    [Header("References")]
    public Transform playerTransform;
    public PlayerHealth playerHealth;

    // Public so other scripts can access
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public Animator animator;
    [HideInInspector] public float hitStunTimer = 0f;

    // Runtime multipliers for difficulty system
    [HideInInspector] public float runtimeSpeedMultiplier  = 1f;
    [HideInInspector] public float runtimeDamageMultiplier = 1f;
    [HideInInspector] public float runtimeHealthMultiplier = 1f;

    // Private
    private float attackTimer = 0f;
    private float wanderTimer = 0f;
    private bool  isDead = false;
    private bool  playerIsDead = false;
    private bool  isAttacking = false; // Lock to let attack animation finish
    private float repathTimer = 0f;

    // Animator parameter hashes (matching EnemyAI.controller)
    private static readonly int HASH_IsWalking   = Animator.StringToHash("IsWalking");
    private static readonly int HASH_IsChasing   = Animator.StringToHash("IsChasing");
    private static readonly int HASH_IsAttacking = Animator.StringToHash("IsAttacking");
    private static readonly int HASH_IsDead      = Animator.StringToHash("IsDead");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // CRITICAL: Disable root motion — it fights NavMeshAgent and causes stuck
        if (animator != null)
            animator.applyRootMotion = false;
    }

    void Start()
    {
        // Auto-find player
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerHealth = p.GetComponent<PlayerHealth>();
            }
        }

        // Setup agent
        if (agent != null)
        {
            agent.speed = wanderSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = attackRange * 0.6f;
            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.autoBraking = false;
        }

        // Ensure on NavMesh
        if (agent != null && !agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 100f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[ZombieAI] {name} warped to NavMesh at {hit.position}");
            }
        }

        PlayerHealth.OnPlayerDeath += () => playerIsDead = true;
    }

    void Update()
    {
        if (isDead) return;
        if (!GameUIManager.IsGamePlaying || GameUIManager.IsGamePaused)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            return;
        }

        // Reduce timers
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        // Hit stun — freeze briefly when hit
        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.deltaTime;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (hitStunTimer <= 0f && agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
            return;
        }

        // If currently attacking, wait for animation to finish
        if (isAttacking)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            FacePlayer();
            return;
        }

        // If player is dead, just idle
        if (playerTransform == null || playerIsDead)
        {
            DoIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        // BEHAVIOR: Simple distance-based decisions
        if (dist <= attackRange)
        {
            DoAttack();
        }
        else if (dist <= detectionRange)
        {
            DoChase();
        }
        else
        {
            DoWander();
        }
    }

    // ─── Behaviors ──────────────────────────────────────────────────────────────

    void DoChase()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.speed = chaseSpeed * runtimeSpeedMultiplier;
        
        repathTimer -= Time.deltaTime;
        if (repathTimer <= 0f)
        {
            repathTimer = Random.Range(0.15f, 0.3f); // Stagger updates across frames to eliminate lag spikes
            agent.SetDestination(playerTransform.position);
        }

        SetAnimState(chase: true);
    }

    void DoAttack()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // Stop moving
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        FacePlayer();

        // Attack on cooldown
        if (attackTimer <= 0f)
        {
            bool isBoss = gameObject.name.IndexOf("Monster10", System.StringComparison.OrdinalIgnoreCase) >= 0;
            attackTimer = isBoss ? 5f : attackCooldown;
            isAttacking = true;

            SetAnimState(attack: true);

            // Deal damage after a short delay
            Invoke(nameof(DealDamage), 0.4f);
            // End attack lock after animation duration
            Invoke(nameof(EndAttack), 1.0f);
        }
        else
        {
            // Waiting for cooldown — just idle and face player
            SetAnimState();
        }
    }

    void EndAttack()
    {
        isAttacking = false;
        SetAnimState();
    }

    void DealDamage()
    {
        if (isDead || playerIsDead) return;
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= attackRange * 1.5f && playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage * runtimeDamageMultiplier);
        }
    }

    void DoIdle()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        SetAnimState();
    }

    void DoWander()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        // If already moving to a wander point, let it finish
        if (!agent.pathPending && agent.remainingDistance > 0.5f)
        {
            agent.isStopped = false;
            SetAnimState(walk: true);
            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            wanderTimer = wanderWaitTime;

            // Pick a random point nearby
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, wanderRadius, NavMesh.AllAreas))
            {
                agent.speed = wanderSpeed;
                agent.isStopped = false;
                agent.SetDestination(hit.position);
                SetAnimState(walk: true);
            }
        }
        else
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            SetAnimState();
        }
    }

    // ─── Animation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets exactly ONE animation bool to true, all others to false.
    /// This prevents animation conflicts and ensures clean transitions.
    /// </summary>
    void SetAnimState(bool walk = false, bool chase = false, bool attack = false, bool dead = false)
    {
        if (animator == null) return;

        animator.SetBool(HASH_IsWalking, walk);
        animator.SetBool(HASH_IsChasing, chase);
        animator.SetBool(HASH_IsAttacking, attack);
        animator.SetBool(HASH_IsDead, dead);
    }

    void FacePlayer()
    {
        if (playerTransform == null) return;
        Vector3 dir = (playerTransform.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    public void OnZombieDied()
    {
        isDead = true;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
        SetAnimState(dead: true);

        // Fix AnyState "Can Transition To Self" jitter bug
        Invoke(nameof(ResetDeadBool), 0.1f);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        enabled = false;
    }

    void ResetDeadBool()
    {
        if (animator != null) animator.SetBool(HASH_IsDead, false);
    }

    // ─── Gizmos ─────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
