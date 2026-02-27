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

    [Header("Stage Awareness")]
    [SerializeField] private float edgeProbeForwardDistance = 0.9f;
    [SerializeField] private float edgeProbeHeight = 0.35f;
    [SerializeField] private float edgeProbeDownDistance = 2f;
    [SerializeField] private float offstageVerticalTolerance = 1.4f;
    [SerializeField] private float ledgeRetreatMove = 0.65f;

    private CharacterMotor motor;
    private CharacterCombat combat;
    private CharacterVisual visual;
    private CharacterMotor playerMotor;
    private CharacterStock playerStock;

    private float decisionTimer;
    private float attackCooldown;

    private bool isDodging;

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CharacterCombat>();
        visual = GetComponent<CharacterVisual>();
        CachePlayerRefs();
    }

    private void Update()
    {
        CachePlayerRefs();
        if (player == null) return;

        if (motor.IsInKnockback || combat.IsInHitStun)
        {
            motor.SetMoveInput(0f);
            return;
        }

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
        if (dir == 0f) dir = 1f;

        visual.HandleFlip(dir);

        // JATUH? COBA RECOVER
        if (!motor.IsGrounded && motor.VelocityY < -2f)
        {
            if (Random.value > 0.5f)
                motor.Jump();
        }

        if (ShouldAvoidOffstageChase(dir))
        {
            MoveSafely(-dir * ledgeRetreatMove);
            return;
        }

        // Terlalu dekat → mundur
        if (absDist < retreatDistance)
        {
            MoveSafely(-dir);
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
        MoveSafely(dir);
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

        MoveSafely(dir);

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

    private void CachePlayerRefs()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player != null)
        {
            if (playerMotor == null)
            {
                playerMotor = player.GetComponent<CharacterMotor>();
            }

            if (playerStock == null)
            {
                playerStock = player.GetComponent<CharacterStock>();
            }
        }
    }

    private bool ShouldAvoidOffstageChase(float dirToPlayer)
    {
        if (!motor.IsGroundedForActions)
        {
            return false;
        }

        if (playerStock != null && playerStock.IsDead)
        {
            return true;
        }

        bool playerOffstage = playerMotor != null && !playerMotor.IsGroundedForActions &&
            player.position.y < transform.position.y - offstageVerticalTolerance;

        if (playerOffstage)
        {
            return true;
        }

        return !HasGroundAhead(dirToPlayer);
    }

    private void MoveSafely(float desiredMove)
    {
        if (Mathf.Approximately(desiredMove, 0f))
        {
            motor.SetMoveInput(0f);
            return;
        }

        if (motor.IsGroundedForActions && !HasGroundAhead(Mathf.Sign(desiredMove)))
        {
            motor.SetMoveInput(0f);
            return;
        }

        motor.SetMoveInput(desiredMove);
    }

    private bool HasGroundAhead(float moveDir)
    {
        if (Mathf.Approximately(moveDir, 0f))
        {
            return true;
        }

        Transform probeOriginTransform = motor.groundCheck != null ? motor.groundCheck : transform;
        Vector3 origin = probeOriginTransform.position;
        origin += Vector3.right * Mathf.Sign(moveDir) * edgeProbeForwardDistance;
        origin += Vector3.up * edgeProbeHeight;

        return Physics.Raycast(
            origin,
            Vector3.down,
            edgeProbeDownDistance,
            motor.groundLayer,
            QueryTriggerInteraction.Ignore);
    }
}
