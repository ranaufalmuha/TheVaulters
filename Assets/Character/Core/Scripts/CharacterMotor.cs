using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CharacterMotor : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 8f;

    [Header("Jump")]
    public int maxJumps = 2;
    public float jumpHeight = 2.5f;

    [Header("Feel")]
    public float fallMultiplier = 4f;
    public float lowJumpMultiplier = 3f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Header("Knockback")]
    public float knockControlLockTime = 0.15f;

    private Rigidbody rb;

    private float moveInput;
    private int jumpCount;
    private bool jumpHeld;

    private bool inKnockback;
    private float knockTimer;

    public bool IsGrounded { get; private set; }
    public float VelocityY => rb.linearVelocity.y;
    public float VelocityX => rb.linearVelocity.x;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints =
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    // =============================
    // MOVEMENT
    // =============================

    public void SetMoveInput(float input)
    {
        moveInput = input;
    }

    public void Jump()
    {
        if (jumpCount >= maxJumps) return;

        jumpCount++;
        jumpHeld = true;

        float jumpVelocity =
            Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);

        Vector3 v = rb.linearVelocity;
        v.y = jumpVelocity;
        rb.linearVelocity = v;
    }

    public void ReleaseJump()
    {
        jumpHeld = false;
    }

    public void ResetAirRecovery()
    {
        jumpCount = 0;
        jumpHeld = false;
    }

    // =============================
    // KNOCKBACK
    // =============================

    public void ApplyKnockback(Vector3 direction, float force)
    {
        inKnockback = true;
        knockTimer = knockControlLockTime;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction * force, ForceMode.Impulse);
    }

    // =============================
    // UPDATE
    // =============================

    private void Update()
    {
        IsGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (IsGrounded)
            jumpCount = 0;

        if (inKnockback)
        {
            knockTimer -= Time.deltaTime;
            if (knockTimer <= 0f)
                inKnockback = false;
        }
    }

    private void FixedUpdate()
    {
        // 🔥 IMPORTANT
        // Jangan overwrite velocity saat knockback
        if (!inKnockback)
        {
            rb.linearVelocity = new Vector3(
                moveInput * moveSpeed,
                rb.linearVelocity.y,
                0f
            );
        }

        // Gravity feel
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += (fallMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.linearVelocity +=
                (lowJumpMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
    }
}
