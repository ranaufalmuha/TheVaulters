using System.Collections.Generic;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Web3.Utils;

namespace Web3.Programs
{
    public static class PlayerProgram
    {
        private const string PlaceholderProgramId = "REPLACE_WITH_PLAYER_PROGRAM_ID";
        public const string ProgramIdString = "8hbqXv8S51R8bjvPHHKkTN6cSVDfUC2cR5tDgqtzqftC";
        public static PublicKey ProgramId => GetProgramId();

        public static TransactionInstruction SetSessionKey(
            PublicKey profile,
            PublicKey owner,
            PublicKey sessionKey,
            long expiry)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.Writable(owner, true)
                },
                Data = AnchorUtils.BuildSetSessionKeyData(sessionKey.KeyBytes, expiry)
            };
        }

        private static PublicKey GetProgramId()
        {
            if (ProgramIdString == PlaceholderProgramId)
            {
                throw new System.InvalidOperationException("Set PlayerProgram.ProgramIdString to your deployed program id.");
            }

            return new PublicKey(ProgramIdString);
        }
    }
}
