using System.Collections.Generic;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Wallet;
using Web3.Utils;

namespace Web3.Programs
{
    public static class PlayerProgram
    {
        private const string PlaceholderProgramId = "REPLACE_WITH_PLAYER_PROGRAM_ID";
        public const string ProgramIdString = "A4WGoXEELbB7iAaaa2oNQHCtuirB4cfvDeAYsA7v8yTa";
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
                    AccountMeta.ReadOnly(owner, true)
                },
                Data = AnchorUtils.BuildSetSessionKeyData(sessionKey.KeyBytes, expiry)
            };
        }

        public static TransactionInstruction Delegate(
            PublicKey profile,
            PublicKey owner,
            PublicKey rollup)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(owner, true)
                },
                Data = AnchorUtils.BuildDelegateData(rollup.KeyBytes)
            };
        }

        public static TransactionInstruction Undelegate(
            PublicKey profile,
            PublicKey owner)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(owner, true)
                },
                Data = AnchorUtils.BuildUndelegateData()
            };
        }

        public static TransactionInstruction UpdateMmr(
            PublicKey profile,
            PublicKey signer,
            long delta)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(signer, true)
                },
                Data = AnchorUtils.BuildUpdateMmrData(delta)
            };
        }

        public static TransactionInstruction GainXp(
            PublicKey profile,
            PublicKey signer,
            ulong amount)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(signer, true)
                },
                Data = AnchorUtils.BuildGainXpData(amount)
            };
        }

        public static TransactionInstruction AddXp(
            PublicKey profile,
            PublicKey signer,
            ulong amount)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(signer, true)
                },
                Data = AnchorUtils.BuildAddXpData(amount)
            };
        }

        public static TransactionInstruction GainLevel(
            PublicKey profile,
            PublicKey signer,
            ulong amount)
        {
            return new TransactionInstruction
            {
                ProgramId = ProgramId.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profile, false),
                    AccountMeta.ReadOnly(signer, true)
                },
                Data = AnchorUtils.BuildGainLevelData(amount)
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
