using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(CharacterMotor))]
[RequireComponent(typeof(CharacterCombat))]
public class CharacterStock : MonoBehaviour
{
    [Header("Stock Settings")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float respawnDelay = 7f;
    [SerializeField] private float deathBottomY = -45f;
    [SerializeField] private float deathTopY = 78f;
    [SerializeField] private float deathLeftX = -52f;
    [SerializeField] private float deathRightX = 82f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;

    [Header("UI")]
    [SerializeField] private TMP_Text healthLeftText;
    [SerializeField] private GameObject respawnPanel;
    [SerializeField] private TMP_Text respawnTimeText;

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
        UpdateHealthLeftUI();

        if (respawnPanel != null)
            respawnPanel.SetActive(false);
    }

    private void Update()
    {
        Vector3 p = transform.position;
        bool outOfArena =
            p.y < deathBottomY ||
            p.y > deathTopY ||
            p.x < deathLeftX ||
            p.x > deathRightX;

        if (!isDead && outOfArena)
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
        UpdateHealthLeftUI();

        if (currentLives <= 0)
        {
            Debug.Log(name + " OUT OF STOCK! GAME OVER.");
            if (respawnPanel != null) respawnPanel.SetActive(false);
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
        if (respawnPanel != null)
            respawnPanel.SetActive(true);

        float timer = respawnDelay;

        while (timer > 0)
        {
            int secondsLeft = Mathf.CeilToInt(timer);
            if (respawnTimeText != null)
                respawnTimeText.text = secondsLeft.ToString();

            timer -= Time.deltaTime;
            yield return null;
        }

        if (respawnTimeText != null)
            respawnTimeText.text = "0";

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

        if (respawnPanel != null)
            respawnPanel.SetActive(false);
    }

    private void UpdateHealthLeftUI()
    {
        if (healthLeftText == null) return;
        healthLeftText.text = Mathf.Max(0, currentLives).ToString();
    }
}
