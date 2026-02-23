using UnityEngine;
using UnityEngine.UI;

public class HealthScaling : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;

    private float currentHealth;

    public float HealthPercent => currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (slider != null)
        {
            slider.maxValue = maxHealth;
            slider.value = currentHealth;
        }

        UpdateColor();
    }

    public void TakeDamage(float dmg)
    {
        Debug.Log("Damage received: " + dmg);
        currentHealth = Mathf.Max(0f, currentHealth - dmg);

        if (slider != null)
            slider.value = currentHealth;


        UpdateColor();
    }

    private void UpdateColor()
    {
        if (fillImage == null) return;

        float percent = HealthPercent;

        if (percent > 0.5f)
            fillImage.color = Color.green;
        else if (percent > 0.2f)
            fillImage.color = Color.yellow;
        else
            fillImage.color = Color.red;
    }

    // Knockback multiplier berdasarkan sisa HP
    public float GetKnockbackMultiplier()
    {
        float percent = HealthPercent;

        return 1f + (1f - percent) * 4f;
    }
}
