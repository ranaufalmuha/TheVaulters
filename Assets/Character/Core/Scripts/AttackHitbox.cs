using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private CharacterCombat owner;
    [SerializeField] private float damage = 10f;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<CharacterCombat>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;

        CharacterCombat target = other.GetComponentInParent<CharacterCombat>();
        if (target == null) return;
        if (target == owner) return;

        float force = owner.GetCurrentAttackForce();

        Debug.Log("Force sent: " + force);

        target.ApplyHit(owner.transform.position, force, damage);
    }
}
