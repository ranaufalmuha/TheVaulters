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
    [SerializeField] private float attackActiveTime = 0.18f;    // hitbox ON
    [SerializeField] private float attackEndLag = 0.12f;        // setelah hitbox OFF, masih “commit”
    [SerializeField] private float comboWindow = 0.22f;         // waktu untuk input attack ke-2
    [SerializeField] private float minAttackInterval = 0.06f;   // anti spam gila

    [Header("Damage/Knockback")]
    [SerializeField] private float baseKnockbackIdle = 14f;
    [SerializeField] private float baseKnockbackRun = 16f;
    [SerializeField] private float baseKnockbackAir = 15f;

    [SerializeField] private float horizontalBias = 2.5f; // ✅ lebih ke samping
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
        // block tidak boleh saat hitstun
        if (inHitStun) value = false;

        isBlocking = value;

        // kalau lagi block, reset chain biar ga “buffer spam”
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

        // ✅ supaya ga spam tiap frame / input kebanyakan
        if (Time.time - lastAttackTime < minAttackInterval) return;

        // kalau sedang attack:
        // - kalau combo window terbuka -> boleh input ke-2
        // - kalau tidak -> ignore
        if (isAttacking && !canCombo) return;

        AttackType next = DetermineAttackType();
        lastAttackTime = Time.time;

        StartCoroutine(AttackRoutine(next));
    }

    private AttackType DetermineAttackType()
    {
        bool grounded = motor.IsGrounded;
        bool moving = Mathf.Abs(motor.VelocityX) > 0.15f;

        // kalau masih bisa combo, pilih varian ke-2 sesuai kategori sebelumnya
        if (canCombo)
        {
            if (!grounded) return AttackType.Air2;
            if (moving) return AttackType.Run2;
            return AttackType.Idle2;
        }

        // kalau bukan combo, mulai dari 1
        if (!grounded) return AttackType.Air1;
        if (moving) return AttackType.Run1;
        return AttackType.Idle1;
    }

    private IEnumerator AttackRoutine(AttackType type)
    {
        isAttacking = true;
        canCombo = false;

        // play anim
        visual.PlayAttack((int)type);
        lastAttackType = type;

        // hitbox on
        if (attackHitbox != null) attackHitbox.enabled = true;
        yield return new WaitForSeconds(attackActiveTime);

        // open combo window (biar bisa input attack ke-2)
        canCombo = true;
        comboTimer = comboWindow;

        // hitbox off
        if (attackHitbox != null) attackHitbox.enabled = false;

        // endlag
        yield return new WaitForSeconds(attackEndLag);

        // kalau player tidak input combo sampai window habis, Update() akan reset
        isAttacking = false;
    }

    // Dipanggil oleh AttackHitbox saat mengenai target
    public void ApplyHit(Vector3 attackerPosition, float baseForce, float damage)
    {
        if (health == null) return;

        float finalDamage = damage;
        float finalForce = baseForce;

        if (isBlocking)
        {
            finalDamage *= blockDamageMultiplier;
            finalForce *= blockForceMultiplier;
        }

        health.TakeDamage(finalDamage);

        float multiplier = health.GetKnockbackMultiplier();
        finalForce *= multiplier;

        float dir = Mathf.Sign(transform.position.x - attackerPosition.x);
        Vector3 knockDir = new Vector3(dir * horizontalBias, verticalBias, 0f).normalized;

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

    // helper kalau kamu mau hitbox nanya attack power berdasar type
    public float GetCurrentAttackForce()
    {
        // kategori berdasarkan lastAttackType
        switch (lastAttackType)
        {
            case AttackType.Run1:
            case AttackType.Run2: return baseKnockbackRun;
            case AttackType.Air1:
            case AttackType.Air2: return baseKnockbackAir;
            default: return baseKnockbackIdle;
        }
    }
}
