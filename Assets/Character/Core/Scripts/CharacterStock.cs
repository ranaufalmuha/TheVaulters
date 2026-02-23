using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterCombat))]
public class CharacterStock : MonoBehaviour
{
    [Header("Stock Settings")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float respawnDelay = 7f;
    [SerializeField] private float deathYLimit = -15f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;

    private CharacterMotor motor;
    private CharacterCombat combat;
    private Rigidbody rb;
    private Collider[] colliders;

    private int currentLives;
    private bool isDead;

    public int Lives => currentLives;
    public bool IsDead => isDead;

    private void Awake()
    {
        motor = GetComponent<CharacterMotor>();
        combat = GetComponent<CharacterCombat>();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        currentLives = maxLives;
    }

    private void Update()
    {
        if (!isDead && transform.position.y < deathYLimit)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        currentLives--;
        isDead = true;

        Debug.Log(name + " KO! Lives left: " + currentLives);

        if (currentLives <= 0)
        {
            Debug.Log(name + " OUT OF STOCK! GAME OVER.");
            return;
        }

        // Disable control
        motor.enabled = false;
        combat.enabled = false;

        // Stop physics
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Disable colliders
        foreach (var col in colliders)
            col.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        float timer = respawnDelay;

        while (timer > 0)
        {
            Debug.Log(name + " Respawn in: " + Mathf.Ceil(timer));
            timer -= 1f;
            yield return new WaitForSeconds(1f);
        }

        Respawn();
    }

    private void Respawn()
    {
        var hp = GetComponent<HealthScaling>();
        if (hp != null) hp.ResetHealth();

        transform.position = spawnPoint.position;

        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;

        foreach (var col in colliders)
            col.enabled = true;

        motor.enabled = true;
        combat.enabled = true;

        isDead = false;
    }
}
