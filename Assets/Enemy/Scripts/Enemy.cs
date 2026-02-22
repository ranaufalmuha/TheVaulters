using UnityEngine;
using System.Collections;

public class Enemy : MonoBehaviour
{
    private Rigidbody rb;
    private SpriteRenderer sr;

    [SerializeField] private float baseKnockback = 8f;
    private float damageMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeHit(Vector3 attackerPos, float attackForce)
    {
        float dir = Mathf.Sign(transform.position.x - attackerPos.x);

        Vector3 knockDir = new Vector3(dir, 0.6f, 0f).normalized;

        float finalForce = attackForce * damageMultiplier;

        rb.linearVelocity = Vector3.zero;
        rb.AddForce(knockDir * finalForce, ForceMode.Impulse);

        damageMultiplier += 0.1f; // semakin sering kena, semakin jauh terpental

        StartCoroutine(HitFlash());
        HitstopManager.Instance?.DoHitstop(0.06f);
    }

    private IEnumerator HitFlash()
    {
        sr.color = Color.white;
        transform.localScale *= 1.1f;

        yield return new WaitForSeconds(0.1f);

        sr.color = Color.gray;
        transform.localScale /= 1.1f;
    }
}
