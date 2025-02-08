namespace MyTorrentBackend.Dtos
{
    public class PeerMessage
    {
        public int Length { get; set; }
        public int MessageId { get; set; }
        public byte[] Payload { get; set; }
    }
}