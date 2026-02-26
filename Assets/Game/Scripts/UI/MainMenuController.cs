using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
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
    private const ulong SessionFundingLamports = 200000; // 0.0002 SOL
    private const ulong MinMainWalletFeeBufferLamports = 20000;

    [SerializeField] private string playSceneName = "SampleScene";
    [SerializeField] private TMP_Text walletAddressText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text solBalanceText;
    [SerializeField] private GameObject createUsernamePanel;
    [SerializeField] private TMP_InputField usernameInput;

    private PublicKey _profilePda;
    private bool _isConnectedAndLoggedIn;

    private void Start()
    {
        Web3Manager.Init();
        SetCreateUsernamePanelVisible(false);
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
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(playSceneName))
        {
            Debug.LogError("Play scene not found in Build Settings: " + playSceneName);
            return;
        }

        Time.timeScale = 1f;
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

            await UpdateSolBalanceAsync();
            bool profileExists = await TryLoadProfileAndUpdateUiAsync();

            if (!profileExists)
            {
                SetCreateUsernamePanelVisible(true);
                _isConnectedAndLoggedIn = false;
                return;
            }

            SetCreateUsernamePanelVisible(false);
            string loginSig = await LoginOnChain();
            Debug.Log("Login tx signature: " + loginSig);
            _isConnectedAndLoggedIn = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("Connect wallet flow failed: " + ex.Message);
            _isConnectedAndLoggedIn = false;
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
    }

    private async Task CreateUsernameAsync()
    {
        try
        {
            if (WalletManager.MainAccount == null)
            {
                Debug.LogError("Connect wallet first.");
                return;
            }

            string username = usernameInput != null ? usernameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(username))
            {
                Debug.LogError("Username cannot be empty.");
                return;
            }

            if (username.Length > 50)
            {
                Debug.LogError("Username max length is 50.");
                return;
            }

            if (_profilePda == null)
            {
                _profilePda = PDAUtils.GetPlayerProfilePDA(WalletManager.MainAccount.PublicKey, PlayerProgram.ProgramId);
            }

            var ix = BuildInitializePlayerInstruction(_profilePda, WalletManager.MainAccount.PublicKey, username);
            string signature = await SendMainWalletInstructionAsync(ix);
            Debug.Log("Initialize player tx signature: " + signature);

            bool profileExists = await WaitForProfileAsync();
            if (!profileExists)
            {
                Debug.LogError("Profile still missing after initialize.");
                return;
            }

            SetCreateUsernamePanelVisible(false);

            string loginSig = await LoginOnChain();
            Debug.Log("Login tx signature: " + loginSig);
            _isConnectedAndLoggedIn = true;
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
                return;
            }

            Debug.LogError("Create username failed: " + ex.Message);
            _isConnectedAndLoggedIn = false;
        }
    }

    private static async Task<string> LoginOnChain()
    {
        PublicKey owner = WalletManager.MainAccount.PublicKey;
        PublicKey profile = PDAUtils.GetPlayerProfilePDA(owner, PlayerProgram.ProgramId);
        PublicKey session = WalletManager.SessionAccount.PublicKey;
        long expiry = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 86400;

        TransactionInstruction setSessionIx = PlayerProgram.SetSessionKey(
            profile,
            owner,
            session,
            expiry);

        string setSessionSig = await SendMainWalletInstructionAsync(setSessionIx);
        Debug.Log("SetSessionKey tx signature: " + setSessionSig);

        await EnsureSessionHasFundingAsync(owner, session);
        return setSessionSig;
    }

    private static async Task EnsureSessionHasFundingAsync(PublicKey owner, PublicKey session)
    {
        var sessionBalanceRes = await Web3Manager.Rpc.GetBalanceAsync(session.Key, Commitment.Confirmed);
        ulong sessionLamports = sessionBalanceRes != null && sessionBalanceRes.WasSuccessful && sessionBalanceRes.Result != null
            ? (ulong)sessionBalanceRes.Result.Value
            : 0UL;

        if (sessionLamports >= SessionFundingLamports)
        {
            return;
        }

        ulong topUpLamports = SessionFundingLamports - sessionLamports;
        var ownerBalanceRes = await Web3Manager.Rpc.GetBalanceAsync(owner.Key, Commitment.Confirmed);
        ulong ownerLamports = ownerBalanceRes != null && ownerBalanceRes.WasSuccessful && ownerBalanceRes.Result != null
            ? (ulong)ownerBalanceRes.Result.Value
            : 0UL;

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

    private static async Task<string> SendMainWalletInstructionAsync(TransactionInstruction instruction)
    {
        return await SendMainWalletInstructionsAsync(new List<TransactionInstruction> { instruction });
    }

    private static async Task<string> SendMainWalletInstructionsAsync(List<TransactionInstruction> instructions)
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

        var txResult = await Web3Manager.Rpc.SendTransactionAsync(
            signed.Serialize(),
            false,
            Commitment.Confirmed);

        if (txResult == null || !txResult.WasSuccessful || string.IsNullOrEmpty(txResult.Result))
        {
            string reason = txResult != null ? txResult.Reason : "null response";
            throw new InvalidOperationException("Main wallet transaction failed: " + reason);
        }

        return txResult.Result;
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

        if (walletAddressText != null) walletAddressText.text = WalletManager.MainAccount.PublicKey.Key;
        if (usernameText != null) usernameText.text = profile.Username;
        if (xpText != null) xpText.text = profile.Xp.ToString();
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
