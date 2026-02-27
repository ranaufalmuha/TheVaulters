using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
public class CharacterVisual : MonoBehaviour
{
    private static readonly int AttackTypeHash = Animator.StringToHash("AttackType");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private static readonly int HurtTriggerHash = Animator.StringToHash("Hurt");
    private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int VYHash = Animator.StringToHash("VY");

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;

    private CharacterMotor motor;

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
    }

    private void Update()
    {
        UpdateAnimator();
    }

    public int FacingSign
    {
        get
        {
            if (visualRoot == null) return 1;
            return visualRoot.localScale.x >= 0f ? 1 : -1;
        }
    }

    public void HandleFlip(float moveX)
    {
        if (moveX == 0 || visualRoot == null) return;

        Vector3 scale = visualRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveX);
        visualRoot.localScale = scale;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(SpeedHash, Mathf.Abs(motor.VelocityX));
        animator.SetBool(GroundedHash, motor.IsGrounded);
        animator.SetFloat(VYHash, motor.VelocityY);
    }

    public void PlayAttack(int attackIndex)
    {
        if (animator == null) return;

        animator.SetInteger(AttackTypeHash, attackIndex);
        animator.SetTrigger(AttackTriggerHash);
    }

    public void PlayHurt()
    {
        if (animator == null) return;

        animator.ResetTrigger(AttackTriggerHash);
        animator.SetInteger(AttackTypeHash, 0);
        animator.SetTrigger(HurtTriggerHash);
    }

    public void SetBlocking(bool value)
    {
        if (animator == null) return;

        animator.SetBool(IsBlockingHash, value);
    }
}
