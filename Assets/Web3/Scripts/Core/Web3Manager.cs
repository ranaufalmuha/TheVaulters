using System;
using System.Threading.Tasks;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Types;

namespace Web3.Core
{
    public static class Web3Manager
    {
        public static IRpcClient Rpc { get; private set; }

        public static void Init()
        {
            if (Rpc != null) return;
            Rpc = ClientFactory.GetClient(Cluster.DevNet);
        }

        public static async Task<string> GetRecentBlockhashAsync(Commitment commitment = Commitment.Confirmed)
        {
            EnsureInitialized();

            RequestResult<Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.LatestBlockHash>> latest =
                await Rpc.GetLatestBlockHashAsync(commitment);

            if (latest != null && latest.WasSuccessful && latest.Result != null && latest.Result.Value != null)
            {
                return latest.Result.Value.Blockhash;
            }

            var legacy = await Rpc.GetRecentBlockHashAsync(commitment);
            if (legacy != null && legacy.WasSuccessful && legacy.Result != null && legacy.Result.Value != null)
            {
                return legacy.Result.Value.Blockhash;
            }

            throw new InvalidOperationException("Failed to fetch recent blockhash from RPC.");
        }

        private static void EnsureInitialized()
        {
            if (Rpc == null)
            {
                throw new InvalidOperationException("Web3Manager is not initialized. Call Web3Manager.Init() first.");
            }
        }
    }
}
