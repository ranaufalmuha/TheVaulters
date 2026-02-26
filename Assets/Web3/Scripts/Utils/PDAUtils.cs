using System;
using System.Collections.Generic;
using System.Text;
using Solana.Unity.Wallet;

namespace Web3.Utils
{
    public static class PDAUtils
    {
        public static PublicKey GetPlayerProfilePDA(PublicKey owner, PublicKey programId)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (programId == null) throw new ArgumentNullException(nameof(programId));

            byte[] seedPrefix = Encoding.UTF8.GetBytes("profile");
            byte[] seedOwner = owner.KeyBytes;

            return FindProgramAddress(new[] { seedPrefix, seedOwner }, programId);
        }

        public static PublicKey GetMatchPDA(ulong matchId, PublicKey programId)
        {
            if (programId == null) throw new ArgumentNullException(nameof(programId));

            byte[] seedPrefix = Encoding.UTF8.GetBytes("match");
            byte[] seedMatchId = BitConverter.GetBytes(matchId);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(seedMatchId);
            }

            return FindProgramAddress(new[] { seedPrefix, seedMatchId }, programId);
        }

        private static PublicKey FindProgramAddress(IEnumerable<byte[]> seeds, PublicKey programId)
        {
            PublicKey pda;
            byte bump;
            bool ok = PublicKey.TryFindProgramAddress(seeds, programId, out pda, out bump);
            if (!ok)
            {
                throw new InvalidOperationException("Failed to derive PDA.");
            }

            return pda;
        }
    }
}
