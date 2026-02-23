using UnityEngine;

[RequireComponent(typeof(CharacterMotor))]
public class CharacterVisual : MonoBehaviour
{
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

        animator.SetFloat("Speed", Mathf.Abs(motor.VelocityX));
        animator.SetBool("Grounded", motor.IsGrounded);
        animator.SetFloat("VY", motor.VelocityY);
    }

    public void PlayAttack(int attackIndex)
    {
        animator.SetInteger("AttackType", attackIndex);
        animator.SetTrigger("Attack");
    }

    public void PlayHurt()
    {
        if (animator == null) return;

        animator.SetTrigger("Hurt");
    }

    public void SetBlocking(bool value)
    {
        animator.SetBool("IsBlocking", value);
    }
}
