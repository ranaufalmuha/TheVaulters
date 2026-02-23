using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CharacterVisual))]
public class PlayerController : MonoBehaviour
{
    private CharacterMotor motor;
    private CharacterCombat combat;
    private CharacterVisual visual;
    private PlayerInputActions input;

    [Header("Block")]
    [SerializeField] private float blockMoveMultiplier = 0.85f; // 1 = normal speed saat block

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CharacterCombat>();
        visual = GetComponent<CharacterVisual>();
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        input.Enable();

        input.Player.Jump.performed += _ => motor.Jump();
        input.Player.Jump.canceled += _ => motor.ReleaseJump();

        input.Player.Attack.performed += _ => combat.TryAttack();
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void Update()
    {
        Vector2 move = input.Player.Move.ReadValue<Vector2>();

        // ✅ Defend hanya saat grounded
        bool defendPressed = input.Player.Defend.IsPressed();
        bool isBlocking = defendPressed && motor.IsGrounded;

        // ✅ Defend TIDAK freeze movement
        float moveX = move.x * (isBlocking ? blockMoveMultiplier : 1f);

        motor.SetMoveInput(moveX);
        visual.HandleFlip(moveX);

        combat.SetBlocking(isBlocking);
        visual.SetBlocking(isBlocking);
    }
}
