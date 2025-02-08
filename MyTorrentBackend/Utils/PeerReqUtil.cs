namespace MyTorrentBackend.Utils
{
    public static class PeerReqUtil
    {
        private static readonly byte pstrlen = 19;
        private static readonly string pstr = "BitTorrent protocol";
        private static readonly byte[] reservedBytes = new byte[8];

        public static byte[] getHandshakeReq(byte[] info_hash)
        {
            byte[] peer_id = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20); // Generate per request

            return new byte[] { pstrlen }
                .Concat(System.Text.Encoding.ASCII.GetBytes(pstr))
                .Concat(reservedBytes)
                .Concat(info_hash)
                .Concat(peer_id)
                .ToArray();
        }

        public static byte[] getInfoHashFromHandshake(byte[] handShakeResponse)
        {
            return handShakeResponse.Skip(28).Take(20).ToArray();
        }
    }
}
