using System.Collections.Generic;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Web3.Utils;

namespace Web3.Programs
{
    public static class MatchProgram
    {
        private const string PlaceholderProgramId = "REPLACE_WITH_MATCH_PROGRAM_ID";
        public const string ProgramIdString = "HjSbgWXcGaWnaj4x3RVvK38edxkWsomMrHdb1f77yx3a";
        public static PublicKey ProgramId => GetProgramId();

        public static TransactionInstruction CreateMatch(
            PublicKey matchAccount,
            PublicKey payer,
            PublicKey playerAProfile,
            PublicKey playerBProfileOrNullProgramId,
            ulong matchId,
            bool isBot)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(matchAccount, false),
                    AccountMeta.Writable(payer, true),
                    AccountMeta.ReadOnly(playerAProfile, false),
                    AccountMeta.ReadOnly(playerBProfileOrNullProgramId, false),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false)
                },
                Data = AnchorUtils.BuildCreateMatchData(matchId, isBot)
            };
        }

        public static TransactionInstruction CommitMatch(
            PublicKey matchAccount,
            PublicKey playerProfile,
            PublicKey playerBProfileOrNullProgramId,
            PublicKey signer,
            bool playerAWin)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(matchAccount, false),
                    AccountMeta.Writable(playerProfile, false),
                    AccountMeta.ReadOnly(playerBProfileOrNullProgramId, false),
                    AccountMeta.ReadOnly(signer, true)
                },
                Data = AnchorUtils.BuildCommitMatchData(playerAWin)
            };
        }

        private static PublicKey GetProgramId()
        {
            if (ProgramIdString == PlaceholderProgramId)
            {
                throw new System.InvalidOperationException("Set MatchProgram.ProgramIdString to your deployed program id.");
            }

            return new PublicKey(ProgramIdString);
        }
    }
}
