using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump")]
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float jumpHeight = 2.5f;

    [Header("Feel")]
    [SerializeField] private float fallMultiplier = 4f;
    [SerializeField] private float lowJumpMultiplier = 3f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private Transform visualToFlip;
    [SerializeField] private Animator animator;

    [Header("Attack")]
    [SerializeField] private Collider attackHitbox;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private float knockbackForce = 8f;

    private Rigidbody rb;
    private PlayerInputActions inputActions;

    private float moveInput;
    private bool isGrounded;
    private bool jumpHeld;
    private int jumpCount;

    private bool isAttacking;
    private bool canCombo;
    private AttackType currentAttack = AttackType.None;

    private enum AttackType
    {
        None = 0,
        Idle1 = 1,
        Idle2 = 2,
        Run1 = 3,
        Run2 = 4,
        Air1 = 5,
        Air2 = 6
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezePositionZ |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Jump.canceled += OnJumpReleased;
        inputActions.Player.Attack.performed += OnAttack;
    }

    private void OnDisable()
    {
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Jump.canceled -= OnJumpReleased;
        inputActions.Player.Attack.performed -= OnAttack;
        inputActions.Disable();
    }

    private void Update()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        moveInput = input.x;

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (isGrounded) jumpCount = 0;

        // Flip
        if (visualToFlip != null && moveInput != 0)
        {
            Vector3 scale = visualToFlip.localScale;
            scale.x = Mathf.Abs(scale.x) * (moveInput > 0 ? 1f : -1f);
            visualToFlip.localScale = scale;
        }

        // Animator
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveInput));
            animator.SetBool("Grounded", isGrounded);
            animator.SetFloat("VY", rb.linearVelocity.y);
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector3(
            moveInput * moveSpeed,
            rb.linearVelocity.y,
            0f
        );

        // Gravity feel
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (jumpCount >= maxJumps) return;

        jumpCount++;
        jumpHeld = true;

        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);

        Vector3 v = rb.linearVelocity;
        v.y = jumpVelocity;
        rb.linearVelocity = v;
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        jumpHeld = false;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (isAttacking && !canCombo) return;

        DetermineAttackType();
        StartCoroutine(AttackRoutine());
    }

    private void DetermineAttackType()
    {
        bool moving = Mathf.Abs(moveInput) > 0.1f;

        if (!isGrounded)
        {
            currentAttack = (currentAttack == AttackType.Air1 && canCombo)
                ? AttackType.Air2
                : AttackType.Air1;
        }
        else if (moving)
        {
            currentAttack = (currentAttack == AttackType.Run1 && canCombo)
                ? AttackType.Run2
                : AttackType.Run1;
        }
        else
        {
            currentAttack = (currentAttack == AttackType.Idle1 && canCombo)
                ? AttackType.Idle2
                : AttackType.Idle1;
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canCombo = false;

        animator.SetBool("IsAttacking", true);
        animator.SetInteger("AttackType", (int)currentAttack);

        ApplySelfForce();

        attackHitbox.enabled = true;

        yield return new WaitForSeconds(attackDuration * 0.5f);

        canCombo = true;

        yield return new WaitForSeconds(attackDuration * 0.5f);

        attackHitbox.enabled = false;

        isAttacking = false;
        canCombo = false;
        currentAttack = AttackType.None;

        animator.SetBool("IsAttacking", false);
    }

    private void ApplySelfForce()
    {
        float dir = Mathf.Sign(visualToFlip.localScale.x);

        if (currentAttack == AttackType.Run1 || currentAttack == AttackType.Run2)
        {
            rb.AddForce(new Vector3(dir * 5f, 0f, 0f), ForceMode.Impulse);
        }
        else if (currentAttack == AttackType.Air1 || currentAttack == AttackType.Air2)
        {
            rb.AddForce(new Vector3(dir * 4f, 2f, 0f), ForceMode.Impulse);
        }
    }
}
