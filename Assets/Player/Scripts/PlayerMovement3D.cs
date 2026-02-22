using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Jump")]
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float jumpHeight = 2.5f;

    [Header("Feel - Fighting Game")]
    [SerializeField] private float fallMultiplier = 4f;       // gravity extra saat turun
    [SerializeField] private float lowJumpMultiplier = 3f;    // gravity extra kalau lepas tombol cepat

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Visual")]
    [SerializeField] private Transform visualToFlip;
    [SerializeField] private Animator animator; // drag animator dari Visual
    [SerializeField] private float apexThreshold = 0.2f; // untuk tuning

    private int jumpCount;
    private bool jumpHeld;
    private Rigidbody rb;
    private PlayerInputActions inputActions;
    private float moveInput;
    private bool isGrounded;

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
        inputActions.Player.Jump.canceled += OnJumpReleased; // ← detect lepas tombol
    }

    private void OnDisable()
    {
        inputActions.Player.Jump.performed -= OnJump;
        inputActions.Player.Jump.canceled -= OnJumpReleased;
        inputActions.Disable();
    }

    private void Update()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        moveInput = input.x;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded) jumpCount = 0;

        // Flip visual
        if (visualToFlip != null && moveInput != 0)
        {
            Vector3 scale = visualToFlip.localScale;
            scale.x = Mathf.Abs(scale.x) * (moveInput > 0 ? 1f : -1f);
            visualToFlip.localScale = scale;
        }

        // Animator params
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(moveInput));
            animator.SetBool("Grounded", isGrounded);
            animator.SetFloat("VY", rb.linearVelocity.y);
        }
    }

    private void FixedUpdate()
    {
        // Horizontal movement
        rb.linearVelocity = new Vector3(
            moveInput * moveSpeed,
            rb.linearVelocity.y,
            0f
        );

        // ▼ KUNCI ANTI-FLOATY: tambah gravity extra
        if (rb.linearVelocity.y < 0f)
        {
            // Sedang turun → gravity jauh lebih kencang
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0f && !jumpHeld)
        {
            // Naik tapi tombol sudah dilepas → langsung kasih gravity lebih
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (jumpCount >= maxJumps) return;

        jumpCount++;
        jumpHeld = true;

        // Hitung velocity dari jumpHeight secara fisik benar
        // v = sqrt(2 * |gravity| * height)
        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);

        Vector3 v = rb.linearVelocity;
        v.y = jumpVelocity;
        rb.linearVelocity = v;
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        jumpHeld = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
