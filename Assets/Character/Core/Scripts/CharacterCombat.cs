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
        Idle1 = 1,
        Idle2 = 2,
        Run1 = 3,
        Run2 = 4,
        Air1 = 5,
        Air2 = 6
    }

    [Header("Attack")]
    [SerializeField] private Collider attackHitbox;
    [SerializeField] private float attackActiveTime = 0.18f;
    [SerializeField] private float attackEndLag = 0.12f;
    [SerializeField] private float comboWindow = 0.22f;
    [SerializeField] private float minAttackInterval = 0.06f;

    [Header("Damage/Knockback")]
    [SerializeField] private float baseKnockbackIdle = 14f;
    [SerializeField] private float baseKnockbackRun = 16f;
    [SerializeField] private float baseKnockbackAir = 15f;

    [SerializeField] private float horizontalBias = 2.5f;
    [SerializeField] private float verticalBias = 0.35f;

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
    private float comboTimer;
    private float lastAttackTime;

    private AttackType lastAttackType = AttackType.None;

    // 🔥 TAMBAHAN PENTING
    private float currentAttackForce;

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
        if (canCombo)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f)
            {
                canCombo = false;
                lastAttackType = AttackType.None;
            }
        }
    }

    public void SetBlocking(bool value)
    {
        if (inHitStun) value = false;

        isBlocking = value;

        if (isBlocking)
        {
            canCombo = false;
            lastAttackType = AttackType.None;
        }
    }

    public void TryAttack()
    {
        if (inHitStun) return;
        if (isBlocking) return;
        if (Time.time - lastAttackTime < minAttackInterval) return;
        if (isAttacking && !canCombo) return;

        AttackType next = DetermineAttackType();
        lastAttackTime = Time.time;

        StartCoroutine(AttackRoutine(next));
    }

    private AttackType DetermineAttackType()
    {
        bool grounded = motor.IsGrounded;
        bool moving = Mathf.Abs(motor.VelocityX) > 0.15f;

        if (canCombo)
        {
            if (!grounded) return AttackType.Air2;
            if (moving) return AttackType.Run2;
            return AttackType.Idle2;
        }

        if (!grounded) return AttackType.Air1;
        if (moving) return AttackType.Run1;
        return AttackType.Idle1;
    }

    private IEnumerator AttackRoutine(AttackType type)
    {
        isAttacking = true;
        canCombo = false;

        visual.PlayAttack((int)type);
        lastAttackType = type;

        // 🔥 SET FORCE DI SINI (bukan nanti saat hit)
        switch (type)
        {
            case AttackType.Run1:
            case AttackType.Run2:
                currentAttackForce = baseKnockbackRun;
                break;

            case AttackType.Air1:
            case AttackType.Air2:
                currentAttackForce = baseKnockbackAir;
                break;

            default:
                currentAttackForce = baseKnockbackIdle;
                break;
        }

        if (attackHitbox != null) attackHitbox.enabled = true;
        yield return new WaitForSeconds(attackActiveTime);

        canCombo = true;
        comboTimer = comboWindow;

        if (attackHitbox != null) attackHitbox.enabled = false;

        yield return new WaitForSeconds(attackEndLag);

        isAttacking = false;
    }

    // 🔥 FIX: gunakan currentAttackForce, bukan lastAttackType lagi
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

        float dir = Mathf.Sign(transform.position.x - attackerPosition.x);
        Vector3 knockDir = new Vector3(dir * horizontalBias, verticalBias, 0f);

        motor.ResetAirRecovery();
        motor.ApplyKnockback(knockDir, finalForce);

        visual.PlayHurt();
        StartCoroutine(HitStunRoutine());
    }

    private IEnumerator HitStunRoutine()
    {
        inHitStun = true;
        yield return new WaitForSeconds(hitStunDuration);
        inHitStun = false;
    }
}
