using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public Transform player;
    public Animator animator;
    public PlayerHealth playerHealth;

    public float detectionRadius = 20f;
    public float attackRange = 2f;

    public float walkSpeed = 0.8f;

    public float attackCooldown = 1.5f;
    public float attackDuration = 1f;

    private NavMeshAgent agent;

    private float cooldownTimer;
    private float attackTimer;

    private bool isAttacking;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");

            if (p != null)
            {
                player = p.transform;
                playerHealth = p.GetComponent<PlayerHealth>();
            }
        }

        animator.applyRootMotion = false;

        agent.speed = walkSpeed;
        agent.updateRotation = false;
        agent.stoppingDistance = attackRange;
    }

    void Update()
    {
        if (player == null) return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            StopZombie();
            return;
        }

        cooldownTimer -= Time.deltaTime;

        float distance = Vector3.Distance(transform.position, player.position);

        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;

            FacePlayer();

            if (attackTimer <= 0f)
            {
                isAttacking = false;
            }

            return;
        }

        if (distance <= attackRange && cooldownTimer <= 0f)
        {
            Attack();
        }
        else if (distance <= detectionRadius)
        {
            WalkToPlayer();
        }
        else
        {
            StopZombie();
        }

        RotateToVelocity();
    }

    void WalkToPlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(player.position);

        animator.SetBool("isWalking", true);
    }

    void Attack()
    {
        isAttacking = true;

        cooldownTimer = attackCooldown;
        attackTimer = attackDuration;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        animator.SetBool("isWalking", false);

        FacePlayer();

        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");

        if (playerHealth != null && !playerHealth.IsDead)
        {
            playerHealth.TakeDamage(10);
        }
    }

    void StopZombie()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        animator.SetBool("isWalking", false);
    }

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                8f * Time.deltaTime
            );
        }
    }

    void RotateToVelocity()
    {
        Vector3 dir = agent.velocity;
        dir.y = 0;

        if (dir.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                6f * Time.deltaTime
            );
        }
    }
}