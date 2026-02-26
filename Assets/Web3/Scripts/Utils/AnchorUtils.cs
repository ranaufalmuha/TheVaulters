using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Web3.Utils
{
    public static class AnchorUtils
    {
        public static byte[] BuildSetSessionKeyData(byte[] sessionKey, long expiry)
        {
            if (sessionKey == null || sessionKey.Length == 0)
            {
                throw new ArgumentException("Session key bytes are required.", nameof(sessionKey));
            }

            byte[] expiryBytes = BitConverter.GetBytes(expiry);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(expiryBytes);
            }

            return BuildAnchorInstructionData("set_session_key", sessionKey, expiryBytes);
        }

        public static byte[] BuildCreateMatchData(ulong matchId, bool isBot)
        {
            byte[] matchIdBytes = BitConverter.GetBytes(matchId);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(matchIdBytes);
            }

            byte[] isBotBytes = { isBot ? (byte)1 : (byte)0 };

            return BuildAnchorInstructionData("create_match", matchIdBytes, isBotBytes);
        }

        public static byte[] BuildFinalizeMatchData(bool playerAWin)
        {
            byte[] playerAWinBytes = { playerAWin ? (byte)1 : (byte)0 };
            return BuildAnchorInstructionData("finalize_match", playerAWinBytes);
        }

        public static byte[] BuildDelegateData(byte[] rollup)
        {
            if (rollup == null || rollup.Length == 0)
            {
                throw new ArgumentException("Rollup pubkey bytes are required.", nameof(rollup));
            }

            return BuildAnchorInstructionData("delegate_profile", rollup);
        }

        public static byte[] BuildUndelegateData()
        {
            return BuildAnchorInstructionData("undelegate_profile");
        }

        public static byte[] BuildCommitMatchData(bool playerAWin)
        {
            byte[] playerAWinBytes = { playerAWin ? (byte)1 : (byte)0 };
            return BuildAnchorInstructionData("commit_match", playerAWinBytes);
        }

        public static byte[] BuildUpdateMmrData(long delta)
        {
            byte[] deltaBytes = BitConverter.GetBytes(delta);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(deltaBytes);
            }

            return BuildAnchorInstructionData("update_mmr", deltaBytes);
        }

        public static byte[] BuildGainXpData(ulong amount)
        {
            byte[] amountBytes = BitConverter.GetBytes(amount);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(amountBytes);
            }

            return BuildAnchorInstructionData("gain_xp", amountBytes);
        }

        public static byte[] BuildAddXpData(ulong amount)
        {
            byte[] amountBytes = BitConverter.GetBytes(amount);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(amountBytes);
            }

            return BuildAnchorInstructionData("add_xp", amountBytes);
        }

        public static byte[] BuildGainLevelData(ulong amount)
        {
            byte[] amountBytes = BitConverter.GetBytes(amount);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(amountBytes);
            }

            return BuildAnchorInstructionData("gain_level", amountBytes);
        }

        private static byte[] BuildAnchorInstructionData(string instructionName, params byte[][] payloadChunks)
        {
            byte[] discriminator = BuildDiscriminator(instructionName);
            int totalPayloadLen = 0;
            for (int i = 0; i < payloadChunks.Length; i++)
            {
                totalPayloadLen += payloadChunks[i].Length;
            }

            byte[] output = new byte[discriminator.Length + totalPayloadLen];
            Buffer.BlockCopy(discriminator, 0, output, 0, discriminator.Length);

            int cursor = discriminator.Length;
            for (int i = 0; i < payloadChunks.Length; i++)
            {
                byte[] chunk = payloadChunks[i];
                Buffer.BlockCopy(chunk, 0, output, cursor, chunk.Length);
                cursor += chunk.Length;
            }
            return output;
        }

        private static byte[] BuildDiscriminator(string instructionName)
        {
            string preimage = "global:" + instructionName;
            byte[] bytes = Encoding.UTF8.GetBytes(preimage);
            byte[] hash;

            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(bytes);
            }

            byte[] discriminator = new byte[8];
            Buffer.BlockCopy(hash, 0, discriminator, 0, discriminator.Length);
            return discriminator;
        }
    }
}
