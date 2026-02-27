using System.Collections;
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
    public float airGravityMultiplier = 4f;
    public float attackLiftAssistTime = 0.12f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public float groundCheckDistance = 0.12f;
    [Range(0f, 1f)] public float groundNormalMinY = 0.35f;
    public float groundedGraceTime = 0.12f;
    public LayerMask groundLayer;

    [Header("Knockback")]
    public float knockControlLockTime = 0.15f;
    public float knockbackLaunchVelocityScale = 0.32f;
    public float knockbackGravityMultiplier = 1.35f;
    public float minKnockbackVerticalVelocity = 4.5f;

    [Header("Fighter Contact")]
    public float fighterPushBlockTime = 0.08f;
    [Range(0f, 1f)] public float fighterSideContactMinAbsNormalX = 0.45f;

    private Rigidbody rb;
    private CapsuleCollider bodyCollider;

    private float moveInput;
    private int jumpCount;
    private float attackLiftAssistTimer;
    private float groundedGraceTimer;
    private float collisionGroundedTimer;
    private float positiveMoveBlockTimer;
    private float negativeMoveBlockTimer;

    private bool inKnockback;

    public bool IsGrounded { get; private set; }
    public bool IsGroundedForActions => IsGrounded || groundedGraceTimer > 0f || collisionGroundedTimer > 0f;
    public bool IsInKnockback => inKnockback;
    public float VelocityY => rb.linearVelocity.y;
    public float VelocityX => rb.linearVelocity.x;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        ApplyNoFrictionToBodyCollider();

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
        if (inKnockback)
        {
            moveInput = 0f;
            return;
        }

        moveInput = input;
    }

    public void Jump()
    {
        if (inKnockback) return;
        if (jumpCount >= maxJumps) return;

        jumpCount++;

        float effectiveGravity = Mathf.Abs(Physics.gravity.y) * Mathf.Max(airGravityMultiplier, 0.01f);
        float jumpVelocity =
            Mathf.Sqrt(2f * effectiveGravity * jumpHeight);

        Vector3 v = rb.linearVelocity;
        v.y = jumpVelocity;
        rb.linearVelocity = v;
    }

    public void ReleaseJump()
    {
        // Jump height is fixed; button release does not change ascent.
    }

    public void ResetAirRecovery()
    {
        jumpCount = 0;
        attackLiftAssistTimer = 0f;
    }

    public void ApplyAttackImpulse(Vector2 impulse, bool clampUpward = true)
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.x += impulse.x;

        float targetY = velocity.y + impulse.y;
        velocity.y = clampUpward ? Mathf.Max(velocity.y, targetY) : targetY;

        rb.linearVelocity = velocity;

        if (impulse.y > 0f)
        {
            attackLiftAssistTimer = attackLiftAssistTime;
        }
    }

    // =============================
    // KNOCKBACK
    // =============================

    public void ApplyKnockback(Vector3 direction, float force)
    {
        inKnockback = true;
        attackLiftAssistTimer = 0f;

        rb.linearVelocity = Vector3.zero;

        Vector3 launchDir = new Vector3(direction.x, Mathf.Max(direction.y, 0.25f), 0f).normalized;
        float launchSpeed = Mathf.Max(0f, force * knockbackLaunchVelocityScale);
        Vector3 launchVelocity = launchDir * launchSpeed;
        launchVelocity.y = Mathf.Max(launchVelocity.y, minKnockbackVerticalVelocity);
        rb.linearVelocity = launchVelocity;

        StartCoroutine(KnockbackLockRoutine());
    }

    private IEnumerator KnockbackLockRoutine()
    {
        if (knockControlLockTime > 0f)
            yield return new WaitForSeconds(knockControlLockTime);

        while (!IsGroundedForActions)
        {
            yield return null;
        }

        inKnockback = false;
    }

    // =============================
    // UPDATE
    // =============================

    private void Update()
    {
        if (collisionGroundedTimer > 0f)
        {
            collisionGroundedTimer -= Time.deltaTime;
        }

        if (positiveMoveBlockTimer > 0f)
        {
            positiveMoveBlockTimer -= Time.deltaTime;
        }

        if (negativeMoveBlockTimer > 0f)
        {
            negativeMoveBlockTimer -= Time.deltaTime;
        }

        IsGrounded = CheckGrounded() || collisionGroundedTimer > 0f;

        if (IsGrounded)
        {
            jumpCount = 0;
            attackLiftAssistTimer = 0f;
            groundedGraceTimer = groundedGraceTime;
        }
        else if (groundedGraceTimer > 0f)
        {
            groundedGraceTimer -= Time.deltaTime;
        }
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null)
        {
            return CheckGroundedFromBody();
        }

        float castRadius = Mathf.Max(0.01f, groundCheckRadius * 0.9f);
        Vector3 origin = groundCheck.position + Vector3.up * 0.05f;

        if (!Physics.SphereCast(
                origin,
                castRadius,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore))
        {
            return CheckGroundedFromBody();
        }

        if (hit.normal.y >= groundNormalMinY)
        {
            return true;
        }

        return CheckGroundedFromBody();
    }

    private bool CheckGroundedFromBody()
    {
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        float castRadius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x * 0.75f, groundCheckRadius));
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + castRadius + 0.05f, bounds.center.z);

        return Physics.SphereCast(
            origin,
            castRadius,
            Vector3.down,
            out RaycastHit hit,
            Mathf.Max(groundCheckDistance, 0.2f),
            groundLayer,
            QueryTriggerInteraction.Ignore) && hit.normal.y >= groundNormalMinY;
    }

    private void ApplyNoFrictionToBodyCollider()
    {
        if (bodyCollider == null)
        {
            return;
        }

        PhysicsMaterial noFrictionMaterial = new PhysicsMaterial("RuntimeFighterNoFriction")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Average
        };

        bodyCollider.material = noFrictionMaterial;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!IsLayerInMask(collision.gameObject.layer, groundLayer))
        {
            CacheFighterSideBlock(collision);
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y >= groundNormalMinY)
            {
                collisionGroundedTimer = Mathf.Max(collisionGroundedTimer, groundedGraceTime);
                return;
            }
        }
    }

    private void CacheFighterSideBlock(Collision collision)
    {
        CharacterMotor otherMotor = collision.collider.GetComponentInParent<CharacterMotor>();
        if (otherMotor == null || otherMotor == this)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (Mathf.Abs(contact.normal.x) < fighterSideContactMinAbsNormalX ||
                Mathf.Abs(contact.normal.y) > 0.55f)
            {
                continue;
            }

            float otherX = otherMotor.transform.position.x;
            if (otherX > transform.position.x)
            {
                positiveMoveBlockTimer = Mathf.Max(positiveMoveBlockTimer, fighterPushBlockTime);
            }
            else
            {
                negativeMoveBlockTimer = Mathf.Max(negativeMoveBlockTimer, fighterPushBlockTime);
            }

            return;
        }
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void FixedUpdate()
    {
        // 🔥 IMPORTANT
        // Jangan overwrite velocity saat knockback
        if (inKnockback)
        {
            moveInput = 0f;
            return;
        }

        if (attackLiftAssistTimer > 0f)
        {
            attackLiftAssistTimer -= Time.fixedDeltaTime;
        }

        float resolvedMoveInput = moveInput;
        if (resolvedMoveInput > 0f && positiveMoveBlockTimer > 0f)
        {
            resolvedMoveInput = 0f;
        }
        else if (resolvedMoveInput < 0f && negativeMoveBlockTimer > 0f)
        {
            resolvedMoveInput = 0f;
        }

        rb.linearVelocity = new Vector3(
            resolvedMoveInput * moveSpeed,
            rb.linearVelocity.y,
            0f
        );

        float activeGravityMultiplier = inKnockback ? knockbackGravityMultiplier : airGravityMultiplier;

        // Apply the same gravity multiplier during ascent and descent.
        if (!Mathf.Approximately(activeGravityMultiplier, 1f))
        {
            rb.linearVelocity += (activeGravityMultiplier - 1f) * Physics.gravity.y * Time.fixedDeltaTime * Vector3.up;
        }
    }
}
