using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMovement3D player; // drag Player root
    [SerializeField] private LayerMask enemyLayer;

    [Header("Hit Settings")]
    [SerializeField] private float hitCooldownPerTarget = 0.05f;

    private float lastHitTime;
    private EnemyFighter lastEnemyHit;

    private void Reset()
    {
        // Auto-detect parent player if possible
        player = GetComponentInParent<PlayerMovement3D>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // optional: membantu kalau collider overlap pas hitbox baru aktif
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        // Layer filter (optional, but good)
        if ((enemyLayer.value & (1 << other.gameObject.layer)) == 0) return;

        if (Time.time - lastHitTime < hitCooldownPerTarget) return;

        var enemy = other.GetComponentInParent<EnemyFighter>();
        if (enemy == null) return;

        // prevent multi-hit spam on same activation window
        if (enemy == lastEnemyHit && Time.time - lastHitTime < 0.15f) return;

        lastEnemyHit = enemy;
        lastHitTime = Time.time;

        // Use player API (knockback + attack type)
        player.NotifyHit(enemy, transform.position);
    }
}
