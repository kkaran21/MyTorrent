namespace MyTorrentBackend.Utils
{
    public enum MessageTypes : int
    {
        MsgChoke = 0,
        MsgUnchoke = 1,
        MsgInterested = 2,
        MsgNotInterested = 3,
        MsgHave = 4,
        MsgBitfield = 5,
        MsgRequest = 6,
        MsgPiece = 7,
        MsgCancel = 8
    }
}