using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TMPro;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Solana.Unity.Programs;
using Web3.Core;
using Web3.Programs;
using Web3.Utils;
using Web3.Wallet;

public class MainMenuController : MonoBehaviour
{
    private const ulong SessionFundingLamports = 10000000; // 0.01 SOL
    private const ulong MinMainWalletFeeBufferLamports = 20000;
    private const ulong MinOwnerLamports = 5000000; // 0.005 SOL safety for init/login/delegate/fund txs
    private const ulong XpPerLevel = 100;
    private const string MagicBlockRollupPublicKey = "MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57";

    [SerializeField] private string playSceneName = "SampleScene";
    [SerializeField] private TMP_Text walletAddressText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text mmrText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text solBalanceText;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private float toastDurationSeconds = 2.5f;
    [SerializeField] private GameObject createUsernamePanel;
    [SerializeField] private TMP_InputField usernameInput;

    private PublicKey _profilePda;
    private bool _isConnectedAndLoggedIn;
    private Coroutine _toastRoutine;

    private void Start()
    {
        Web3Manager.Init();
        EnsureXpSliderExists();
        EnsureToastTextExists();
        SetCreateUsernamePanelVisible(false);
        ShowToast("Welcome. Connect wallet to continue.");
        _ = RestoreSessionStateAsync();
    }

    public void ConnectWallet()
    {
        _ = ConnectWalletAndPrepareProfileAsync();
    }

    public void PlayGame()
    {
        if (!_isConnectedAndLoggedIn)
        {
            Debug.LogWarning("Connect wallet and complete login first.");
            ShowToast("Connect wallet and login first.", true);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(playSceneName))
        {
            Debug.LogError("Play scene not found in Build Settings: " + playSceneName);
            ShowToast("Play scene is missing in Build Settings.", true);
            return;
        }

        Time.timeScale = 1f;
        ShowToast("Loading match...");
        SceneManager.LoadScene(playSceneName);
    }

    public void CreateUsername()
    {
        _ = CreateUsernameAsync();
    }

    private async Task ConnectWalletAndPrepareProfileAsync()
    {
        try
        {
            ShowToast("Connecting wallet...");
            if (WalletManager.MainAccount == null)
            {
                await WalletManager.ConnectWallet();
            }
            if (WalletManager.SessionAccount == null)
            {
                WalletManager.GenerateSession();
            }

            Debug.Log("Wallet public key: " + WalletManager.MainAccount.PublicKey);
            Debug.Log("Session public key: " + WalletManager.SessionAccount.PublicKey);

            _profilePda = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
            await EnsureOwnerHasFundingAsync(WalletManager.MainAccount.PublicKey);

            await UpdateSolBalanceAsync();
            bool profileExists = await TryLoadProfileAndUpdateUiAsync();

            if (!profileExists)
            {
                SetCreateUsernamePanelVisible(true);
                _isConnectedAndLoggedIn = false;
                ShowToast("Wallet connected. Create your username first.");
                return;
            }

            SetCreateUsernamePanelVisible(false);
            string loginSig = await LoginOnChain();
            Debug.Log("Login tx signature: " + loginSig);
            _isConnectedAndLoggedIn = true;
            ShowToast("Login successful.");
        }
        catch (Exception ex)
        {
            Debug.LogError("Connect wallet flow failed: " + ex.Message);
            _isConnectedAndLoggedIn = false;
            ShowToast("Connect wallet failed: " + ex.Message, true);
        }
    }

    private async Task RestoreSessionStateAsync()
    {
        if (WalletManager.MainAccount == null || WalletManager.SessionAccount == null)
        {
            _isConnectedAndLoggedIn = false;
            return;
        }

        _profilePda = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
        await UpdateSolBalanceAsync();
        bool profileExists = await TryLoadProfileAndUpdateUiAsync();
        SetCreateUsernamePanelVisible(!profileExists);
        _isConnectedAndLoggedIn = profileExists;
        if (profileExists)
        {
            ShowToast("Session restored.");
        }
    }

    private async Task CreateUsernameAsync()
    {
        try
        {
            if (WalletManager.MainAccount == null)
            {
                Debug.LogError("Connect wallet first.");
                ShowToast("Connect wallet first.", true);
                return;
            }

            string username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                Debug.LogError("Username cannot be empty.");
                ShowToast("Username cannot be empty.", true);
                return;
            }

            if (username.Length > 50)
            {
                Debug.LogError("Username max length is 50.");
                ShowToast("Username max length is 50.", true);
                return;
            }

            if (_profilePda == null)
            {
                _profilePda = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
            }

            await EnsureOwnerHasFundingAsync(WalletManager.MainAccount.PublicKey);

            var ix = BuildInitializePlayerInstruction(_profilePda, WalletManager.MainAccount.PublicKey, username);
            string signature = await SendMainWalletInstructionAsync(ix);
            Debug.Log("Initialize player tx signature: " + signature);
            ShowToast("Creating profile on-chain...");

            bool profileExists = await WaitForProfileAsync();
            if (!profileExists)
            {
                Debug.LogError("Profile still missing after initialize.");
                ShowToast("Profile not found yet. Please try again.", true);
                return;
            }

            SetCreateUsernamePanelVisible(false);

            string loginSig = await LoginOnChain();
            Debug.Log("Login tx signature: " + loginSig);
            _isConnectedAndLoggedIn = true;
            ShowToast("Username created and login complete.");
        }
        catch (Exception ex)
        {
            // Common case after slow RPC propagation: account already initialized on-chain.
            bool profileExists = await WaitForProfileAsync();
            if (profileExists)
            {
                SetCreateUsernamePanelVisible(false);
                string loginSig = await LoginOnChain();
                Debug.Log("Login tx signature: " + loginSig);
                _isConnectedAndLoggedIn = true;
                ShowToast("Profile already exists. Logged in.");
                return;
            }

            Debug.LogError("Create username failed: " + ex.Message);
            _isConnectedAndLoggedIn = false;
            ShowToast("Create username failed: " + ex.Message, true);
        }
    }

    private static async Task<string> LoginOnChain()
    {
        PublicKey owner = WalletManager.MainAccount.PublicKey;
        PublicKey profile = PDAUtils.GetPlayerProfilePDA(owner, PlayerProgram.ProgramId);
        PublicKey session = WalletManager.SessionAccount.PublicKey;
        long expiry = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400;

        var instructions = new List<TransactionInstruction>();

        TransactionInstruction setSessionIx = PlayerProgram.SetSessionKey(
            profile,
            owner,
            session,
            expiry);
        instructions.Add(setSessionIx);

        ulong sessionLamports = await GetBalanceLamportsAsync(session);
        if (sessionLamports < SessionFundingLamports)
        {
            ulong topUpLamports = SessionFundingLamports - sessionLamports;
            instructions.Add(SystemProgram.Transfer(owner, session, topUpLamports));
        }

        TransactionInstruction delegateIx = PlayerProgram.Delegate(
            profile,
            owner,
            new PublicKey(MagicBlockRollupPublicKey));
        instructions.Add(delegateIx);

        string bundledSig = await SendMainWalletInstructionsAsync(instructions);
        Debug.Log("Login bundle tx signature: " + bundledSig + " (set_session_key + funding(if needed) + delegate_profile)");
        return bundledSig;
    }

    private static async Task EnsureSessionHasFundingAsync(PublicKey owner, PublicKey session)
    {
        ulong sessionLamports = await GetBalanceLamportsAsync(session);

        if (sessionLamports >= SessionFundingLamports)
        {
            return;
        }

        ulong topUpLamports = SessionFundingLamports - sessionLamports;
        ulong ownerLamports = await GetBalanceLamportsAsync(owner);

        ulong needed = topUpLamports + MinMainWalletFeeBufferLamports;
        if (ownerLamports < needed)
        {
            throw new InvalidOperationException(
                "Main wallet SOL is insufficient for session funding. " +
                "Owner: " + ownerLamports + " lamports, needed: " + needed + " lamports.");
        }

        TransactionInstruction fundSessionIx = SystemProgram.Transfer(owner, session, topUpLamports);
        string fundSig = await SendMainWalletInstructionAsync(fundSessionIx);
        Debug.Log("FundSession tx signature: " + fundSig + " (" + topUpLamports + " lamports)");
    }

    private static async Task EnsureOwnerHasFundingAsync(PublicKey owner)
    {
        ulong ownerLamports = await GetBalanceLamportsAsync(owner);

        if (ownerLamports >= MinOwnerLamports)
        {
            return;
        }

        ulong needed = MinOwnerLamports - ownerLamports;
        Debug.LogWarning("Owner balance is low (" + ownerLamports + " lamports). Requesting devnet airdrop...");
        var airdropRes = await Web3Manager.Rpc.RequestAirdropAsync(owner.Key, needed, Commitment.Confirmed);
        if (airdropRes == null || !airdropRes.WasSuccessful || string.IsNullOrEmpty(airdropRes.Result))
        {
            string reason = airdropRes != null ? airdropRes.Reason : "null response";
            throw new InvalidOperationException(
                "Low wallet SOL and airdrop failed. Add SOL manually. Reason: " + reason);
        }

        Debug.Log("Airdrop tx signature: " + airdropRes.Result + " (" + needed + " lamports)");
    }

    private static async Task<ulong> GetBalanceLamportsAsync(PublicKey address)
    {
        var res = await Web3Manager.Rpc.GetBalanceAsync(address.Key, Commitment.Confirmed);
        if (res == null || !res.WasSuccessful || res.Result == null)
        {
            return 0UL;
        }

        return (ulong)res.Result.Value;
    }

    private static async Task<string> SendMainWalletInstructionAsync(TransactionInstruction instruction)
    {
        return await SendMainWalletInstructionsAsync(new List<TransactionInstruction> { instruction });
    }

    private static async Task<string> SendMainWalletInstructionsAsync(List<TransactionInstruction> instructions)
    {
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var tx = new Transaction
            {
                FeePayer = WalletManager.MainAccount.PublicKey,
                RecentBlockHash = await Web3Manager.GetRecentBlockhashAsync(),
                Instructions = new List<TransactionInstruction>(),
                Signatures = new List<SignaturePubKeyPair>()
            };

            for (int i = 0; i < instructions.Count; i++)
            {
                tx.Add(instructions[i]);
            }

            Transaction signed = await WalletManager.SignTransactionWithMainWallet(tx);
            if (signed == null)
            {
                throw new InvalidOperationException("Transaction signing cancelled");
            }

            var txResult = await Web3Manager.Rpc.SendTransactionAsync(
                signed.Serialize(),
                false,
                Commitment.Confirmed);

            if (txResult != null && txResult.WasSuccessful && !string.IsNullOrEmpty(txResult.Result))
            {
                return txResult.Result;
            }

            string reason = txResult != null ? txResult.Reason : "null response";
            bool canRetry = reason != null && reason.IndexOf("Blockhash not found", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!canRetry || attempt == maxAttempts - 1)
            {
                throw new InvalidOperationException("Main wallet transaction failed: " + reason);
            }
        }

        throw new InvalidOperationException("Main wallet transaction failed after retries.");
    }

    private async Task<bool> TryLoadProfileAndUpdateUiAsync()
    {
        var res = await Web3Manager.Rpc.GetAccountInfoAsync(_profilePda.Key, Commitment.Confirmed);
        if (res == null || !res.WasSuccessful || res.Result == null || res.Result.Value == null)
        {
            return false;
        }

        List<string> data = res.Result.Value.Data;
        if (data == null || data.Count == 0 || string.IsNullOrEmpty(data[0]))
        {
            return false;
        }

        byte[] raw = Convert.FromBase64String(data[0]);
        PlayerProfileView profile = DecodePlayerProfile(raw);

        if (walletAddressText != null) walletAddressText.text = FormatWalletAddress(WalletManager.MainAccount.PublicKey.Key);
        if (usernameText != null) usernameText.text = profile.Username;
        if (mmrText != null) mmrText.text = profile.Mmr.ToString();
        UpdateXpUi(profile.Xp);
        if (levelText != null) levelText.text = profile.Level.ToString();

        return true;
    }

    private async Task<bool> WaitForProfileAsync()
    {
        const int maxAttempts = 12;
        const int delayMs = 500;

        for (int i = 0; i < maxAttempts; i++)
        {
            bool found = await TryLoadProfileAndUpdateUiAsync();
            if (found) return true;
            await Task.Delay(delayMs);
        }

        return false;
    }

    private async Task UpdateSolBalanceAsync()
    {
        var balance = await Web3Manager.Rpc.GetBalanceAsync(WalletManager.MainAccount.PublicKey.Key, Commitment.Confirmed);
        if (balance == null || !balance.WasSuccessful || balance.Result == null)
        {
            return;
        }

        double sol = balance.Result.Value / 1000000000.0;
        if (solBalanceText != null) solBalanceText.text = sol.ToString("0.####");
    }

    private static TransactionInstruction BuildInitializePlayerInstruction(PublicKey profilePda, PublicKey user, string username)
    {
        byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
        byte[] usernameLen = BitConverter.GetBytes((uint)usernameBytes.Length);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(usernameLen);
        }

        byte[] discriminator = BuildAnchorDiscriminator("initialize_player");
        byte[] data = new byte[discriminator.Length + usernameLen.Length + usernameBytes.Length];
        Buffer.BlockCopy(discriminator, 0, data, 0, discriminator.Length);
        Buffer.BlockCopy(usernameLen, 0, data, discriminator.Length, usernameLen.Length);
        Buffer.BlockCopy(usernameBytes, 0, data, discriminator.Length + usernameLen.Length, usernameBytes.Length);

        return new TransactionInstruction
        {
            ProgramId = PlayerProgram.ProgramId.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(profilePda, false),
                AccountMeta.Writable(user, true),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
            },
            Data = data
        };
    }

    private static byte[] BuildAnchorDiscriminator(string methodName)
    {
        string preimage = "global:" + methodName;
        byte[] input = Encoding.UTF8.GetBytes(preimage);
        byte[] hash;
        using (SHA256 sha = SHA256.Create())
        {
            hash = sha.ComputeHash(input);
        }

        byte[] discriminator = new byte[8];
        Buffer.BlockCopy(hash, 0, discriminator, 0, discriminator.Length);
        return discriminator;
    }

    private static PlayerProfileView DecodePlayerProfile(byte[] raw)
    {
        int offset = 8; // anchor discriminator
        offset += 32;   // owner

        uint usernameLen = ReadU32(raw, ref offset);
        string username = Encoding.UTF8.GetString(raw, offset, (int)usernameLen);
        offset += (int)usernameLen;

        ulong mmr = ReadU64(raw, ref offset);
        ulong level = ReadU64(raw, ref offset);
        ulong xp = ReadU64(raw, ref offset);

        return new PlayerProfileView
        {
            Username = username,
            Mmr = mmr,
            Level = level,
            Xp = xp
        };
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

    private void SetCreateUsernamePanelVisible(bool visible)
    {
        if (createUsernamePanel != null)
            createUsernamePanel.SetActive(visible);
    }

    private void UpdateXpUi(ulong totalXp)
    {
        ulong levelXp = totalXp % XpPerLevel;
        if (xpText != null)
        {
            xpText.text = levelXp + "/" + XpPerLevel + " XP";
        }

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = (float)XpPerLevel;
            xpSlider.SetValueWithoutNotify((float)levelXp);
        }
    }

    private void EnsureXpSliderExists()
    {
        if (xpSlider != null || xpText == null)
        {
            return;
        }

        GameObject sliderGo = new GameObject("XP Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        sliderGo.transform.SetParent(xpText.transform, false);

        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0f);
        sliderRect.anchorMax = new Vector2(0.95f, 0f);
        sliderRect.pivot = new Vector2(0.5f, 0f);
        sliderRect.anchoredPosition = new Vector2(0f, 4f);
        sliderRect.sizeDelta = new Vector2(0f, 12f);

        Image backgroundImage = sliderGo.GetComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.2f);
        backgroundImage.raycastTarget = false;

        GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image fillImage = fillGo.GetComponent<Image>();
        fillImage.color = new Color(0.196f, 0.741f, 0.376f, 0.95f);
        fillImage.raycastTarget = false;

        Slider slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = (float)XpPerLevel;
        slider.SetValueWithoutNotify(0f);

        xpSlider = slider;
    }

    private void EnsureToastTextExists()
    {
        if (toastText != null)
        {
            toastText.gameObject.SetActive(false);
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        GameObject toastGo = new GameObject("ToastMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        toastGo.transform.SetParent(canvas.transform, false);

        RectTransform rect = toastGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 40f);
        rect.sizeDelta = new Vector2(780f, 64f);

        TextMeshProUGUI tmp = toastGo.GetComponent<TextMeshProUGUI>();
        tmp.font = xpText != null ? xpText.font : null;
        tmp.fontSize = 30;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        toastText = tmp;
        toastGo.SetActive(false);
    }

    private void ShowToast(string message, bool isError = false)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        if (toastText == null)
        {
            Debug.Log((isError ? "[Toast Error] " : "[Toast] ") + message);
            return;
        }

        toastText.text = message;
        toastText.color = isError ? new Color(1f, 0.52f, 0.52f, 1f) : Color.white;
        toastText.gameObject.SetActive(true);

        if (_toastRoutine != null)
        {
            StopCoroutine(_toastRoutine);
        }

        _toastRoutine = StartCoroutine(HideToastRoutine());
    }

    private IEnumerator HideToastRoutine()
    {
        yield return new WaitForSeconds(toastDurationSeconds);
        if (toastText != null)
        {
            toastText.gameObject.SetActive(false);
        }

        _toastRoutine = null;
    }

    private static string FormatWalletAddress(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress) || walletAddress.Length <= 9)
        {
            return walletAddress;
        }

        return walletAddress.Substring(0, 5) + "..." + walletAddress.Substring(walletAddress.Length - 4, 4);
    }

    private struct PlayerProfileView
    {
        public string Username;
        public ulong Mmr;
        public ulong Level;
        public ulong Xp;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
