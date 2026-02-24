using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchFlowManager : MonoBehaviour
{
    [Header("Match")]
    [SerializeField] private float matchDurationSeconds = 180f;
    [SerializeField] private float returnToMenuDelaySeconds = 2.5f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Participants")]
    [SerializeField] private CharacterStock playerStock;
    [SerializeField] private CharacterStock enemyStock;

    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    private float remainingTime;
    private bool isMatchEnded;

    private void Awake()
    {
        AutoBindParticipants();
        AutoBindTimerText();

        remainingTime = Mathf.Max(0f, matchDurationSeconds);
        UpdateTimerUI();

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private void OnEnable()
    {
        CharacterStock.OnOutOfStock += HandleOutOfStock;
    }

    private void OnDisable()
    {
        CharacterStock.OnOutOfStock -= HandleOutOfStock;
    }

    private void Update()
    {
        if (isMatchEnded) return;

        remainingTime -= Time.deltaTime;
        if (remainingTime < 0f) remainingTime = 0f;

        UpdateTimerUI();

        if (remainingTime <= 0f)
        {
            ResolveByTimeout();
        }
    }

    private void HandleOutOfStock(CharacterStock depletedStock)
    {
        if (isMatchEnded) return;

        if (depletedStock == playerStock)
        {
            EndMatch("YOU LOSE");
            return;
        }

        if (depletedStock == enemyStock)
        {
            EndMatch("YOU WIN");
            return;
        }

        // Fallback in case stock refs were not assigned as expected.
        ResolveByLivesAndHealth();
    }

    private void ResolveByTimeout()
    {
        if (isMatchEnded) return;
        ResolveByLivesAndHealth();
    }

    private void ResolveByLivesAndHealth()
    {
        int playerLives = playerStock != null ? playerStock.Lives : 0;
        int enemyLives = enemyStock != null ? enemyStock.Lives : 0;

        if (playerLives > enemyLives)
        {
            EndMatch("YOU WIN");
            return;
        }

        if (playerLives < enemyLives)
        {
            EndMatch("YOU LOSE");
            return;
        }

        float playerHP = GetCurrentHealth(playerStock);
        float enemyHP = GetCurrentHealth(enemyStock);

        if (playerHP > enemyHP)
        {
            EndMatch("YOU WIN");
            return;
        }

        if (playerHP < enemyHP)
        {
            EndMatch("YOU LOSE");
            return;
        }

        EndMatch("DRAW");
    }

    private float GetCurrentHealth(CharacterStock stock)
    {
        if (stock == null) return 0f;

        HealthScaling hp = stock.GetComponent<HealthScaling>();
        return hp != null ? hp.CurrentHealth : 0f;
    }

    private void EndMatch(string result)
    {
        if (isMatchEnded) return;
        isMatchEnded = true;

        FreezeCharacter(playerStock);
        FreezeCharacter(enemyStock);

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultText != null)
            resultText.text = result;

        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        yield return new WaitForSecondsRealtime(returnToMenuDelaySeconds);

        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError("Main Menu scene not found in Build Settings: " + mainMenuSceneName);
            yield break;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void FreezeCharacter(CharacterStock stock)
    {
        if (stock == null) return;
        stock.FreezeForMatchEnd();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(remainingTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void AutoBindParticipants()
    {
        if (playerStock == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                playerStock = playerObject.GetComponent<CharacterStock>();
        }

        if (enemyStock == null)
        {
            GameObject enemyObject = GameObject.FindGameObjectWithTag("Enemy");
            if (enemyObject != null)
                enemyStock = enemyObject.GetComponent<CharacterStock>();
        }
    }

    private void AutoBindTimerText()
    {
        if (timerText != null) return;

        GameObject timePanel = GameObject.Find("TimePanel");
        if (timePanel == null) return;

        timerText = timePanel.GetComponentInChildren<TMP_Text>(true);
    }
}
