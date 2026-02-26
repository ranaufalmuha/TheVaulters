using System.Collections;
using System.Collections.Generic;
using System;
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
    private const long WinMmrDelta = 20;
    private const long LoseMmrDelta = -10;
    private const long DrawMmrDelta = 5;
    private const ulong WinXp = 30;
    private const ulong LoseXp = 12;
    private const ulong DrawXp = 18;
    private const ulong XpPerLevel = 100;

    private enum MatchOutcome
    {
        Win,
        Lose,
        Draw
    }

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
    private bool commitSubmitted;
    private Task pendingCommitTask;
    private MatchOutcome outcome;

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
        pendingCommitTask = Task.CompletedTask;
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
        outcome = ParseOutcome(result);

        FreezeCharacter(playerStock);
        FreezeCharacter(enemyStock);

        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (resultText != null)
            resultText.text = result;

        if (createMatchSucceeded && !commitSubmitted)
        {
            commitSubmitted = true;
            pendingCommitTask = CommitMatchSafeAsync();
        }

        StartCoroutine(ReturnToMainMenuRoutine());
    }

    private IEnumerator ReturnToMainMenuRoutine()
    {
        yield return new WaitForSecondsRealtime(returnToMenuDelaySeconds);

        float commitWaitTimeoutSeconds = 8f;
        while (pendingCommitTask != null && !pendingCommitTask.IsCompleted && commitWaitTimeoutSeconds > 0f)
        {
            commitWaitTimeoutSeconds -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (pendingCommitTask != null && !pendingCommitTask.IsCompleted)
        {
            Debug.LogWarning("CommitMatch is still pending. Returning to menu.");
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
        currentMatchId = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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

    public async Task<string> CommitMatch()
    {
        if (currentMatchId == 0UL)
        {
            currentMatchId = (ulong)System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        if (currentMatchPda == null)
        {
            currentMatchPda = PDAUtils.GetMatchPDA(currentMatchId, MatchProgram.ProgramId);
        }

        PublicKey playerProfile = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
        // Anchor optional account sentinel: pass current program id to represent None.
        PublicKey optionalPlayerB = MatchProgram.ProgramId;
        bool playerAWin = outcome == MatchOutcome.Win;

        TransactionInstruction commitIx = MatchProgram.CommitMatch(
            currentMatchPda,
            playerProfile,
            optionalPlayerB,
            WalletManager.SessionAccount.PublicKey,
            playerAWin);

        string signature = await SendSessionTransaction(commitIx, WalletManager.SessionAccount);
        Debug.Log("CommitMatch tx signature: " + signature);
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

    private async Task CommitMatchSafeAsync()
    {
        try
        {
            string sig = await CommitMatch();
            string progressionSig = await ApplyProgressionAsync();
            if (!string.IsNullOrEmpty(progressionSig))
            {
                Debug.Log("Progression tx signature: " + progressionSig);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("CommitMatch failed: " + ex.Message);
        }
    }

    private async Task<string> ApplyProgressionAsync()
    {
        PublicKey profile = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
        PublicKey signer = WalletManager.SessionAccount.PublicKey;
        PlayerSnapshot snapshot = await GetPlayerSnapshotAsync(profile);

        long mmrDelta = GetMmrDelta(outcome);
        ulong xpGain = GetXpGain(outcome);

        // Apply MMR first in its own tx so XP instruction mismatch cannot block it.
        string mmrSig = await SendSessionTransaction(
            PlayerProgram.UpdateMmr(profile, signer, mmrDelta),
            WalletManager.SessionAccount);

        string xpSig;
        try
        {
            xpSig = await SendSessionTransaction(
                PlayerProgram.GainXp(profile, signer, xpGain),
                WalletManager.SessionAccount);
        }
        catch (Exception)
        {
            xpSig = await SendSessionTransaction(
                PlayerProgram.AddXp(profile, signer, xpGain),
                WalletManager.SessionAccount);
        }

        ulong levelBefore = snapshot.Xp / XpPerLevel;
        ulong levelAfter = (snapshot.Xp + xpGain) / XpPerLevel;
        ulong gainedLevels = levelAfter > levelBefore ? levelAfter - levelBefore : 0UL;
        if (gainedLevels > 0)
        {
            try
            {
                await SendSessionTransaction(PlayerProgram.GainLevel(profile, signer, gainedLevels), WalletManager.SessionAccount);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("GainLevel instruction failed: " + ex.Message);
            }
        }

        return !string.IsNullOrEmpty(xpSig) ? xpSig : mmrSig;
    }

    private static long GetMmrDelta(MatchOutcome matchOutcome)
    {
        if (matchOutcome == MatchOutcome.Win) return WinMmrDelta;
        if (matchOutcome == MatchOutcome.Lose) return LoseMmrDelta;
        return DrawMmrDelta;
    }

    private static ulong GetXpGain(MatchOutcome matchOutcome)
    {
        if (matchOutcome == MatchOutcome.Win) return WinXp;
        if (matchOutcome == MatchOutcome.Lose) return LoseXp;
        return DrawXp;
    }

    private static MatchOutcome ParseOutcome(string result)
    {
        if (string.Equals(result, "YOU WIN", StringComparison.OrdinalIgnoreCase))
        {
            return MatchOutcome.Win;
        }

        if (string.Equals(result, "YOU LOSE", StringComparison.OrdinalIgnoreCase))
        {
            return MatchOutcome.Lose;
        }

        return MatchOutcome.Draw;
    }

    private static async Task<PlayerSnapshot> GetPlayerSnapshotAsync(PublicKey profile)
    {
        var accountInfo = await Web3Manager.Rpc.GetAccountInfoAsync(profile.Key, Commitment.Confirmed);
        if (accountInfo == null || !accountInfo.WasSuccessful || accountInfo.Result == null || accountInfo.Result.Value == null)
        {
            throw new InvalidOperationException("Failed to fetch player profile account.");
        }

        List<string> data = accountInfo.Result.Value.Data;
        if (data == null || data.Count == 0 || string.IsNullOrEmpty(data[0]))
        {
            throw new InvalidOperationException("Player profile account has empty data.");
        }

        byte[] raw = Convert.FromBase64String(data[0]);
        int offset = 8;  // discriminator
        offset += 32;    // owner

        uint usernameLen = ReadU32(raw, ref offset);
        offset += (int)usernameLen;

        ReadU64(raw, ref offset);
        ReadU64(raw, ref offset);
        ulong xp = ReadU64(raw, ref offset);

        return new PlayerSnapshot
        {
            Xp = xp
        };
    }

    private static async Task<string> SendSessionTransaction(TransactionInstruction instruction, Account signer)
    {
        return await SendSessionTransaction(new List<TransactionInstruction> { instruction }, signer);
    }

    private static async Task<string> SendSessionTransaction(List<TransactionInstruction> instructions, Account signer)
    {
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var tx = new Transaction
            {
                FeePayer = signer.PublicKey,
                RecentBlockHash = await Web3Manager.GetRecentBlockhashAsync(),
                Instructions = new List<TransactionInstruction>(),
                Signatures = new List<SignaturePubKeyPair>()
            };

            for (int i = 0; i < instructions.Count; i++)
            {
                tx.Add(instructions[i]);
            }
            tx.Sign(signer);

            var txResult = await Web3Manager.Rpc.SendTransactionAsync(
                tx.Serialize(),
                false,
                Commitment.Confirmed);

            if (txResult != null && txResult.WasSuccessful && !string.IsNullOrEmpty(txResult.Result))
            {
                bool confirmed = await Web3Manager.Rpc.ConfirmTransaction(txResult.Result, Commitment.Confirmed);
                if (confirmed)
                {
                    return txResult.Result;
                }

                throw new InvalidOperationException("Transaction rejected on-chain. Signature: " + txResult.Result);
            }

            string reason = txResult != null ? txResult.Reason : "null response";
            bool canRetry = reason != null && reason.IndexOf("Blockhash not found", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!canRetry || attempt == maxAttempts - 1)
            {
                throw new InvalidOperationException("Transaction failed: " + reason);
            }
        }

        throw new InvalidOperationException("Transaction failed after retries.");
    }

    private static uint ReadU32(byte[] raw, ref int offset)
    {
        byte[] tmp = new byte[4];
        Buffer.BlockCopy(raw, offset, tmp, 0, 4);
        if (!BitConverter.IsLittleEndian) Array.Reverse(tmp);
        offset += 4;
        return BitConverter.ToUInt32(tmp, 0);
    }

    private static ulong ReadU64(byte[] raw, ref int offset)
    {
        byte[] tmp = new byte[8];
        Buffer.BlockCopy(raw, offset, tmp, 0, 8);
        if (!BitConverter.IsLittleEndian) Array.Reverse(tmp);
        offset += 8;
        return BitConverter.ToUInt64(tmp, 0);
    }

    private struct PlayerSnapshot
    {
        public ulong Xp;
    }
}
