using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterVisual))]
[RequireComponent(typeof(HealthScaling))]
public class CharacterCombat : MonoBehaviour
{
    private enum AttackType
    {
        None = 0,
        Ground1 = 1,
        Ground2 = 2,
        Air1 = 5,
        Air2 = 6
    }

    [Header("Attack")]
    [SerializeField] private Collider attackHitbox;
    [SerializeField] private AttackTiming groundAttack1Timing = new AttackTiming(0.22f, 0.18f);
    [SerializeField] private AttackTiming groundAttack2Timing = new AttackTiming(0.18f, 0.24f);
    [SerializeField] private AttackTiming airAttack1Timing = new AttackTiming(0.14f, 0.12f);
    [SerializeField] private AttackTiming airAttack2Timing = new AttackTiming(0.12f, 0.12f);
    [SerializeField] private float comboWindow = 0.28f;
    [SerializeField] private float comboCooldown = 0.16f;
    [SerializeField] private Vector2 airAttack1Impulse = new Vector2(3f, 5f);
    [SerializeField] private Vector2 airAttack2Impulse = new Vector2(4.5f, 6f);

    [Header("Damage/Knockback")]
    [SerializeField] private float baseKnockbackGround = 14f;
    [SerializeField] private float baseKnockbackAir = 15f;

    [SerializeField] private float horizontalBias = 2.5f;
    [SerializeField] private float verticalBias = 0.35f;
    [SerializeField] private float ringOutForceAtZeroHP = 42f;
    [SerializeField] private float maxKnockbackForce = 48f;

    [Header("Hit Stun")]
    [SerializeField] private float hitStunDuration = 0.22f;

    [Header("Block")]
    [SerializeField] private float blockDamageMultiplier = 0.30f;
    [SerializeField] private float blockForceMultiplier = 0.50f;

    private CharacterMotor motor;
    private CharacterVisual visual;
    private HealthScaling health;

    private bool isAttacking;
    private bool inHitStun;
    private bool isBlocking;

    private bool canCombo;
    private bool queuedComboAttack;
    private float comboTimer;
    private float cooldownTimer;
    private bool airAttack1Used;
    private bool airAttack2Used;

    private AttackType lastAttackType = AttackType.None;
    private AttackType currentAttackType = AttackType.None;

    private float currentAttackForce;

    public bool IsGroundMovementLocked => isBlocking || (isAttacking && IsGroundAttack(currentAttackType));

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        visual = GetComponent<CharacterVisual>();
        health = GetComponent<HealthScaling>();

        if (attackHitbox != null)
            attackHitbox.enabled = false;
    }

    private void Update()
    {
        if (motor.IsGrounded)
        {
            ResetAirAttackUsage();
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (canCombo)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                ResetComboState(startCooldown: !isAttacking);
            }
        }
    }

    public void SetBlocking(bool value)
    {
        if (inHitStun) value = false;

        isBlocking = value;

        if (isBlocking)
        {
            ResetComboState(startCooldown: false);
        }
    }

    public void TryAttack()
    {
        if (inHitStun) return;
        if (isBlocking) return;
        if (cooldownTimer > 0f) return;

        if (isAttacking)
        {
            if (CanQueueCombo())
            {
                queuedComboAttack = true;
            }
            return;
        }

        AttackType next = DetermineAttackType();
        if (next == AttackType.None) return;
        StartCoroutine(AttackRoutine(next));
    }

    private AttackType DetermineAttackType()
    {
        bool grounded = motor.IsGrounded;
        bool continueCombo = canCombo && comboTimer > 0f;

        if (!grounded)
        {
            if (continueCombo && lastAttackType == AttackType.Air1 && !airAttack2Used)
                return AttackType.Air2;

            if (!airAttack1Used)
                return AttackType.Air1;

            return AttackType.None;
        }

        return continueCombo && lastAttackType == AttackType.Ground1 ? AttackType.Ground2 : AttackType.Ground1;
    }

    private IEnumerator AttackRoutine(AttackType type)
    {
        isAttacking = true;
        bool finisher = IsFinisher(type);
        AttackTiming timing = GetAttackTiming(type);

        queuedComboAttack = false;
        canCombo = false;
        comboTimer = 0f;

        visual.PlayAttack((int)type);
        lastAttackType = type;
        currentAttackType = type;

        if (type == AttackType.Air1)
        {
            airAttack1Used = true;
        }
        else if (type == AttackType.Air2)
        {
            airAttack2Used = true;
        }

        if (type == AttackType.Air1 || type == AttackType.Air2)
        {
            Vector2 baseImpulse = type == AttackType.Air1 ? airAttack1Impulse : airAttack2Impulse;
            Vector2 impulse = new Vector2(baseImpulse.x * visual.FacingSign, baseImpulse.y);
            motor.ApplyAttackImpulse(impulse);
        }

        switch (type)
        {
            case AttackType.Air1:
            case AttackType.Air2:
                currentAttackForce = baseKnockbackAir;
                break;

            default:
                currentAttackForce = baseKnockbackGround;
                break;
        }

        if (attackHitbox != null) attackHitbox.enabled = true;
        yield return new WaitForSeconds(timing.ActiveTime);

        if (attackHitbox != null) attackHitbox.enabled = false;

        if (!finisher)
        {
            canCombo = true;
            comboTimer = comboWindow;
        }

        yield return new WaitForSeconds(timing.EndLag);

        isAttacking = false;

        if (!finisher && queuedComboAttack && canCombo && comboTimer > 0f)
        {
            AttackType next = DetermineAttackType();
            if (next != AttackType.None)
            {
                StartCoroutine(AttackRoutine(next));
                yield break;
            }
        }

        if (finisher)
        {
            ResetComboState(startCooldown: true);
        }
        else if (!isAttacking)
        {
            currentAttackType = AttackType.None;
        }
    }

    public float GetCurrentAttackForce()
    {
        return currentAttackForce;
    }

    public void ApplyHit(Vector3 attackerPosition, float baseForce, float damage)
    {
        if (health == null) return;

        if (isBlocking)
        {
            damage *= blockDamageMultiplier;
            baseForce *= blockForceMultiplier;
        }

        health.TakeDamage(damage);

        float multiplier = health.GetKnockbackMultiplier();
        float finalForce = baseForce * multiplier;
        if (health.CurrentHealth <= 0f)
            finalForce = Mathf.Max(finalForce, ringOutForceAtZeroHP);
        finalForce = Mathf.Min(finalForce, maxKnockbackForce);

        float dir = transform.position.x >= attackerPosition.x ? 1f : -1f;
        Vector3 knockDir = new Vector3(dir * horizontalBias, verticalBias, 0f);

        motor.ResetAirRecovery();
        motor.ApplyKnockback(knockDir, finalForce);

        ResetComboState(startCooldown: false);
        ResetAirAttackUsage();
        currentAttackType = AttackType.None;
        visual.PlayHurt();
        StartCoroutine(HitStunRoutine());
    }

    private IEnumerator HitStunRoutine()
    {
        inHitStun = true;
        yield return new WaitForSeconds(hitStunDuration);
        inHitStun = false;
    }

    private bool CanQueueCombo()
    {
        return !queuedComboAttack && !IsFinisher(lastAttackType);
    }

    private static bool IsFinisher(AttackType type)
    {
        return type == AttackType.Ground2 || type == AttackType.Air2;
    }

    private static bool IsGroundAttack(AttackType type)
    {
        return type == AttackType.Ground1 || type == AttackType.Ground2;
    }

    private AttackTiming GetAttackTiming(AttackType type)
    {
        switch (type)
        {
            case AttackType.Ground1:
                return groundAttack1Timing;
            case AttackType.Ground2:
                return groundAttack2Timing;
            case AttackType.Air1:
                return airAttack1Timing;
            case AttackType.Air2:
                return airAttack2Timing;
            default:
                return groundAttack1Timing;
        }
    }

    private void ResetComboState(bool startCooldown)
    {
        canCombo = false;
        queuedComboAttack = false;
        comboTimer = 0f;
        lastAttackType = AttackType.None;
        if (!isAttacking)
        {
            currentAttackType = AttackType.None;
        }

        if (startCooldown)
        {
            cooldownTimer = comboCooldown;
        }
    }

    private void ResetAirAttackUsage()
    {
        airAttack1Used = false;
        airAttack2Used = false;
    }

    [System.Serializable]
    private struct AttackTiming
    {
        public float ActiveTime;
        public float EndLag;

        public AttackTiming(float activeTime, float endLag)
        {
            ActiveTime = activeTime;
            EndLag = endLag;
        }
    }
}
