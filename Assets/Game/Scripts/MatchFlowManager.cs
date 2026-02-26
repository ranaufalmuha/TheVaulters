using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Solana.Unity.Rpc.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using Web3.Core;
using Web3.Programs;
using Web3.Utils;
using Web3.Wallet;

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
    private ulong currentMatchId;
    private PublicKey currentMatchPda;
    private bool createMatchSucceeded;
    private bool finalizeSubmitted;
    private Task pendingFinalizeTask;

    private void Awake()
    {
        AutoBindParticipants();
        AutoBindTimerText();

        remainingTime = Mathf.Max(0f, matchDurationSeconds);
        UpdateTimerUI();

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    private void Start()
    {
        Web3Manager.Init();
        pendingFinalizeTask = Task.CompletedTask;
        _ = BeginMatchChainFlowAsync();
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

        if (createMatchSucceeded && !finalizeSubmitted)
        {
            finalizeSubmitted = true;
            pendingFinalizeTask = FinalizeMatchSafeAsync();
        }

        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        yield return new WaitForSecondsRealtime(returnToMenuDelaySeconds);

        float finalizeWaitTimeoutSeconds = 8f;
        while (pendingFinalizeTask != null && !pendingFinalizeTask.IsCompleted && finalizeWaitTimeoutSeconds > 0f)
        {
            finalizeWaitTimeoutSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (pendingFinalizeTask != null && !pendingFinalizeTask.IsCompleted)
        {
            Debug.LogWarning("FinalizeMatch is still pending. Returning to menu.");
        }

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

    public async Task<string> CreateMatch()
    {
        currentMatchId = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        currentMatchPda = PDAUtils.GetMatchPDA(currentMatchId, MatchProgram.ProgramId);
        PublicKey playerProfile = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
        // Anchor optional account sentinel: pass current program id to represent None.
        PublicKey optionalPlayerB = MatchProgram.ProgramId;

        TransactionInstruction ix = MatchProgram.CreateMatch(
            currentMatchPda,
            WalletManager.SessionAccount.PublicKey,
            playerProfile,
            optionalPlayerB,
            currentMatchId,
            true);

        string signature = await SendSessionTransaction(ix, WalletManager.SessionAccount);
        Debug.Log("CreateMatch tx signature: " + signature);
        return signature;
    }

    public async Task<string> FinalizeMatch()
    {
        if (currentMatchId == 0UL)
        {
            currentMatchId = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        if (currentMatchPda == null)
        {
            currentMatchPda = PDAUtils.GetMatchPDA(currentMatchId, MatchProgram.ProgramId);
        }

        PublicKey playerProfile = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
        // Anchor optional account sentinel: pass current program id to represent None.
        PublicKey optionalPlayerB = MatchProgram.ProgramId;
        bool playerAWin = enemyStock != null && enemyStock.IsOutOfStock;
        TransactionInstruction ix = MatchProgram.FinalizeMatch(
            currentMatchPda,
            playerProfile,
            optionalPlayerB,
            WalletManager.SessionAccount.PublicKey,
            playerAWin);

        string signature = await SendSessionTransaction(ix, WalletManager.SessionAccount);
        Debug.Log("FinalizeMatch tx signature: " + signature);
        return signature;
    }

    private async Task BeginMatchChainFlowAsync()
    {
        if (WalletManager.MainAccount == null || WalletManager.SessionAccount == null)
        {
            Debug.LogWarning("Wallet/session missing in Match scene. Match tx flow is disabled.");
            createMatchSucceeded = false;
            return;
        }

        try
        {
            string sig = await CreateMatch();
            createMatchSucceeded = !string.IsNullOrEmpty(sig);
        }
        catch (System.Exception ex)
        {
            createMatchSucceeded = false;
            Debug.LogError("CreateMatch failed: " + ex.Message);
        }
    }

    private async Task FinalizeMatchSafeAsync()
    {
        try
        {
            string sig = await FinalizeMatch();
            if (string.IsNullOrEmpty(sig))
            {
                Debug.LogWarning("FinalizeMatch returned empty signature.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("FinalizeMatch failed: " + ex.Message);
        }
    }

    private static async Task<string> SendSessionTransaction(TransactionInstruction instruction, Account signer)
    {
        var tx = new Transaction
        {
            FeePayer = signer.PublicKey,
            RecentBlockHash = await Web3Manager.GetRecentBlockhashAsync(),
            Instructions = new List<TransactionInstruction>(),
            Signatures = new List<SignaturePubKeyPair>()
        };

        AddInstruction(tx, instruction);
        tx.Sign(signer);

        var txResult = await Web3Manager.Rpc.SendTransactionAsync(
            tx.Serialize(),
            false,
            Commitment.Confirmed);

        if (txResult == null || !txResult.WasSuccessful || string.IsNullOrEmpty(txResult.Result))
        {
            string reason = txResult != null ? txResult.Reason : "null response";
            Debug.LogError("Transaction failed: " + reason);
            return string.Empty;
        }

        return txResult.Result;
    }

    private static async Task<string> SendMainWalletTransaction(TransactionInstruction instruction)
    {
        var tx = new Transaction
        {
            FeePayer = WalletManager.MainAccount.PublicKey,
            RecentBlockHash = await Web3Manager.GetRecentBlockhashAsync(),
            Instructions = new List<TransactionInstruction>(),
            Signatures = new List<SignaturePubKeyPair>()
        };

        tx.Add(instruction);
        Transaction signed = await WalletManager.SignTransactionWithMainWallet(tx);

        var txResult = await Web3Manager.Rpc.SendTransactionAsync(
            signed.Serialize(),
            false,
            Commitment.Confirmed);

        if (txResult == null || !txResult.WasSuccessful || string.IsNullOrEmpty(txResult.Result))
        {
            string reason = txResult != null ? txResult.Reason : "null response";
            Debug.LogError("Main wallet transaction failed: " + reason);
            return string.Empty;
        }

        return txResult.Result;
    }

    private static void AddInstruction(Transaction tx, TransactionInstruction instruction)
    {
        tx.Add(instruction);
    }
}
