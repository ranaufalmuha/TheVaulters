using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterCombat))]
[RequireComponent(typeof(CharacterVisual))]
public class EnemyAI : MonoBehaviour
{
    public enum AggressionLevel
    {
        Easy,
        Normal,
        Hard
    }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Difficulty")]
    [SerializeField] private AggressionLevel aggression = AggressionLevel.Normal;

    [Header("Distance")]
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float retreatDistance = 0.8f;

    private CharacterMotor motor;
    private CharacterCombat combat;
    private CharacterVisual visual;

    private float decisionTimer;
    private float attackCooldown;

    private bool isDodging;

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CharacterCombat>();
        visual = GetComponent<CharacterVisual>();
    }

    private void Update()
    {
        if (player == null) return;

        decisionTimer -= Time.deltaTime;
        attackCooldown -= Time.deltaTime;

        if (decisionTimer > 0) return;

        decisionTimer = GetThinkDelay();

        MakeDecision();
    }

    private void MakeDecision()
    {
        float distance = player.position.x - transform.position.x;
        float absDist = Mathf.Abs(distance);
        float dir = Mathf.Sign(distance);

        visual.HandleFlip(dir);

        // JATUH? COBA RECOVER
        if (!motor.IsGrounded && motor.VelocityY < -2f)
        {
            if (Random.value > 0.5f)
                motor.Jump();
        }

        // Terlalu dekat → mundur
        if (absDist < retreatDistance)
        {
            motor.SetMoveInput(-dir);
            return;
        }

        // Dalam jarak attack
        if (absDist <= attackDistance)
        {
            motor.SetMoveInput(0);

            if (attackCooldown <= 0f)
            {
                if (ShouldAttack())
                {
                    combat.TryAttack();
                    attackCooldown = GetAttackCooldown();
                }
                else
                {
                    TryDodge(dir);
                }
            }

            return;
        }

        // Jauh → kejar
        motor.SetMoveInput(dir);
    }

    private void TryDodge(float dir)
    {
        if (isDodging) return;

        if (Random.value > 0.5f)
        {
            StartCoroutine(DodgeRoutine(-dir));
        }
    }

    private IEnumerator DodgeRoutine(float dir)
    {
        isDodging = true;

        motor.SetMoveInput(dir);

        yield return new WaitForSeconds(0.3f);

        motor.SetMoveInput(0);

        isDodging = false;
    }

    private bool ShouldAttack()
    {
        switch (aggression)
        {
            case AggressionLevel.Easy:
                return Random.value > 0.6f;

            case AggressionLevel.Normal:
                return Random.value > 0.3f;

            case AggressionLevel.Hard:
                return true;

            default:
                return false;
        }
    }

    private float GetThinkDelay()
    {
        switch (aggression)
        {
            case AggressionLevel.Easy:
                return Random.Range(0.5f, 0.8f);

            case AggressionLevel.Normal:
                return Random.Range(0.3f, 0.5f);

            case AggressionLevel.Hard:
                return Random.Range(0.1f, 0.2f);

            default:
                return 0.4f;
        }
    }

    private float GetAttackCooldown()
    {
        switch (aggression)
        {
            case AggressionLevel.Easy:
                return 1.2f;

            case AggressionLevel.Normal:
                return 0.8f;

            case AggressionLevel.Hard:
                return 0.5f;

            default:
                return 1f;
        }
    }
}
