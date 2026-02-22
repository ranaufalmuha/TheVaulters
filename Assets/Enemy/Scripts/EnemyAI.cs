using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyFighter))]
public class EnemyAI : MonoBehaviour
{
    [Header("Gravity Feel")]
    [SerializeField] private float fallMultiplier = 4f;
    [SerializeField] private float lowJumpMultiplier = 3f;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private Collider attackHitbox;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float stopDistance = 1.5f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private float attackForce = 8f;

    [Header("AI Behaviour")]
    [SerializeField] private float thinkInterval = 0.2f;
    [SerializeField] private float restDuration = 1.0f;

    private Rigidbody rb;
    private EnemyFighter fighter;

    private float thinkTimer;
    private float attackTimer;
    private float restTimer;

    private bool isAttacking;
    private bool isResting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fighter = GetComponent<EnemyFighter>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (visualRoot == null)
            visualRoot = GetComponentInChildren<SpriteRenderer>()?.transform;
    }

    private void Update()
    {
        if (player == null) return;

        UpdateAnimator();

        thinkTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;

        if (isResting)
        {
            restTimer -= Time.deltaTime;
            if (restTimer <= 0f)
                isResting = false;
            return;
        }

        if (thinkTimer <= 0f)
        {
            Think();
            thinkTimer = thinkInterval;
        }
    }

    private void FixedUpdate()
    {
        if (player == null || isAttacking || isResting)
        {
            ApplyGravityFeel();
            return;
        }

        float distance = player.position.x - transform.position.x;

        if (Mathf.Abs(distance) > stopDistance)
        {
            float dir = Mathf.Sign(distance);

            HandleFlip(dir);

            rb.linearVelocity = new Vector3(
                dir * moveSpeed,
                rb.linearVelocity.y,
                0f
            );
        }
        else
        {
            rb.linearVelocity = new Vector3(
                0f,
                rb.linearVelocity.y,
                0f
            );
        }

        ApplyGravityFeel();
    }

    private void ApplyGravityFeel()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private void Think()
    {
        if (player == null || isAttacking || isResting) return;

        float horizontalDist = Mathf.Abs(player.position.x - transform.position.x);

        if (horizontalDist <= attackRange && attackTimer <= 0f)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        attackTimer = attackCooldown;

        // Trigger animation
        animator.SetInteger("AttackType", 1); // pakai attack idle 1 dulu
        animator.SetTrigger("Attack");

        attackHitbox.enabled = true;

        yield return new WaitForSeconds(attackDuration);

        attackHitbox.enabled = false;
        isAttacking = false;

        isResting = true;
        restTimer = restDuration;
    }

    private void HandleFlip(float direction)
    {
        if (visualRoot == null) return;

        if (direction != 0)
        {
            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction);
            visualRoot.localScale = scale;
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool("Grounded", fighter.IsGrounded);
        animator.SetFloat("VY", rb.linearVelocity.y);
    }
}
