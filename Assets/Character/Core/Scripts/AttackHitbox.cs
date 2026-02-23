using UnityEngine;
using System.Collections.Generic;

public class AttackHitbox : MonoBehaviour
{
    [SerializeField] private CharacterCombat owner;
    [SerializeField] private float damage = 10f;
    private readonly HashSet<CharacterCombat> hitTargets = new HashSet<CharacterCombat>();
    private Collider hitboxCollider;
    private bool wasHitboxEnabled;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<CharacterCombat>();

        hitboxCollider = GetComponent<Collider>();
        wasHitboxEnabled = hitboxCollider != null && hitboxCollider.enabled;
    }

    private void Update()
    {
        if (hitboxCollider == null) return;

        bool isEnabledNow = hitboxCollider.enabled;
        if (isEnabledNow && !wasHitboxEnabled)
            hitTargets.Clear();

        wasHitboxEnabled = isEnabledNow;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;

        CharacterCombat target = other.GetComponentInParent<CharacterCombat>();
        if (target == null) return;
        if (target == owner) return;
        if (hitTargets.Contains(target)) return;

        float force = owner.GetCurrentAttackForce();

        Debug.Log("Force sent: " + force);

        hitTargets.Add(target);
        target.ApplyHit(owner.transform.position, force, damage);
    }
}
