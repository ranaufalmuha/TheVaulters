using System;
using System.Threading.Tasks;
using Solana.Unity.Rpc.Models;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

namespace Web3.Wallet
{
    public static class WalletManager
    {
        public static Account MainAccount { get; private set; }
        public static Account SessionAccount { get; private set; }
        public static WalletBase AdapterWallet { get; private set; }

        public static async Task<Account> ConnectWallet()
        {
            if (AdapterWallet == null)
            {
                var options = new SolanaWalletAdapterOptions
                {
                    solanaMobileWalletAdapterOptions = new SolanaMobileWalletAdapterOptions(),
                    solanaWalletAdapterWebGLOptions = new SolanaWalletAdapterWebGLOptions
                    {
                        walletAdapterUIPrefab = Resources.Load<GameObject>("SolanaUnitySDK/WalletAdapterUI"),
                        walletAdapterButtonPrefab = Resources.Load<GameObject>("SolanaUnitySDK/WalletAdapterButton")
                    },
                    phantomWalletOptions = new PhantomWalletOptions()
                };

                AdapterWallet = new SolanaWalletAdapter(options, RpcCluster.DevNet);
            }

            MainAccount = await AdapterWallet.Login();
            if (MainAccount == null)
            {
                throw new InvalidOperationException("Wallet adapter connection failed or was cancelled.");
            }

            return MainAccount;
        }

        public static async Task<Transaction> SignTransactionWithMainWallet(Transaction tx)
        {
            if (AdapterWallet == null || MainAccount == null)
            {
                throw new InvalidOperationException("Wallet not connected. Call ConnectWallet first.");
            }

            return await AdapterWallet.SignTransaction(tx);
        }

        public static void GenerateSession()
        {
            SessionAccount = new Account();
        }
    }
}
