using UnityEngine;

public class EnemyAttackHitbox : MonoBehaviour
{
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private float force = 8f;

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement3D player = other.GetComponent<PlayerMovement3D>();
        if (player == null) return;

        float dir = Mathf.Sign(
            player.transform.position.x - enemyRoot.position.x
        );

        Vector3 knockDir = new Vector3(dir, 0.6f, 0f).normalized;

        player.ReceiveHit(knockDir, force);
    }
}
