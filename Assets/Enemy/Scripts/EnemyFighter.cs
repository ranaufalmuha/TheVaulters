using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class EnemyFighter : MonoBehaviour
{
    [Header("Knockback Scaling")]
    [SerializeField] private float knockbackGrowth = 0.12f;
    [SerializeField] private float maxKnockbackMultiplier = 2.5f;

    [Header("Weight / Traction")]
    [SerializeField] private float groundTraction = 60f;
    [SerializeField] private float maxPushSpeed = 1.0f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Death")]
    [SerializeField] private float deathYLevel = -15f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody rb;

    private float damageMultiplier = 1f;
    private bool isDead;
    private bool inKnockback;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (!isDead && transform.position.y < deathYLevel)
            Die();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        UpdateGrounded();
        ApplyGroundTraction();
    }

    private void UpdateGrounded()
    {
        IsGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );
    }

    private void ApplyGroundTraction()
    {
        if (inKnockback) return;
        if (!IsGrounded) return;

        // kalau lagi dikontrol AI movement, jangan traction
        if (Mathf.Abs(rb.linearVelocity.x) > 0.1f) return;

        float vx = rb.linearVelocity.x;

        vx = Mathf.MoveTowards(
            vx,
            0f,
            groundTraction * Time.fixedDeltaTime
        );

        rb.linearVelocity = new Vector3(
            vx,
            rb.linearVelocity.y,
            0f
        );
    }

    public void TakeHit(Vector3 knockDir, float force)
    {
        if (isDead) return;

        float finalForce = force * damageMultiplier;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(knockDir.normalized * finalForce, ForceMode.Impulse);

        damageMultiplier = Mathf.Clamp(
            damageMultiplier + knockbackGrowth,
            1f,
            maxKnockbackMultiplier
        );

        inKnockback = true;
        CancelInvoke(nameof(EndKnockback));
        Invoke(nameof(EndKnockback), 0.25f);

        StartCoroutine(HitFeedback());
    }

    private void EndKnockback()
    {
        inKnockback = false;
    }

    private IEnumerator HitFeedback()
    {
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;
        Vector3 originalScale = transform.localScale;

        spriteRenderer.color = Color.white;
        transform.localScale = originalScale * 1.08f;

        yield return new WaitForSeconds(0.08f);

        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
    }

    private void Die()
    {
        isDead = true;
        Destroy(gameObject, 0.4f);
    }
}
