using UnityEngine;
using UnityEngine.UI;

public class HealthScaling : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    [Header("Knockback Scaling (by current HP)")]
    [Tooltip("Multiplier when HP >= 70%")]
    [SerializeField] private float multGreen = 1.0f;

    [Tooltip("Multiplier when HP = 50-70%")]
    [SerializeField] private float multYellow = 1.35f;

    [Tooltip("Multiplier when HP = 20-50%")]
    [SerializeField] private float multDarkRed = 1.9f;

    [Tooltip("Multiplier when HP = 20% (start of danger zone)")]
    [SerializeField] private float multAt20 = 2.6f;

    [Tooltip("Multiplier when HP = 0% (VERY large)")]
    [SerializeField] private float multAt0 = 12.0f;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float HealthPercent01 => maxHealth <= 0 ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

    private void Awake()
    {
        currentHealth = maxHealth;
        AutoBindUI();
        UpdateUI();
    }

    private void AutoBindUI()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>();
        if (fillImage == null && slider != null && slider.fillRect != null)
            fillImage = slider.fillRect.GetComponent<Image>();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float dmg)
    {
        if (dmg <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - dmg);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slider != null)
        {
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
        }

        if (fillImage == null) return;

        float p = HealthPercent01;

        // >70% hijau, >50% kuning, >20% merah tua, sisanya merah terang
        if (p > 0.70f)
            fillImage.color = Color.green;
        else if (p > 0.50f)
            fillImage.color = Color.yellow;
        else if (p > 0.20f)
            fillImage.color = new Color(0.55f, 0.05f, 0.05f); // merah tua
        else
            fillImage.color = new Color(1.0f, 0.05f, 0.05f);  // merah darah terang
    }

    /// <summary>
    /// Knockback multiplier berdasarkan sisa HP (semakin kecil HP, semakin besar knockback).
    /// 20% jadi “zona bahaya”; dari 20% ke 0% naik tajam sampai multAt0.
    /// </summary>
    public float GetKnockbackMultiplier()
    {
        float p = HealthPercent01;

        if (p > 0.70f) return multGreen;
        if (p > 0.50f) return multYellow;
        if (p > 0.20f) return multDarkRed;

        // Danger zone: p ∈ [0..0.2]
        // t = 0 saat 20%, t = 1 saat 0%
        float t = Mathf.InverseLerp(0.20f, 0.0f, p);

        // Naik tajam (bisa kamu tune)
        // SmoothStep biar gak patah
        t = Mathf.SmoothStep(0f, 1f, t);

        return Mathf.Lerp(multAt20, multAt0, t);
    }
}
