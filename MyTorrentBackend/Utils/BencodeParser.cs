using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Utils;

public class BencodeParser
{
    private Byte[] ByteArr;
    public BencodeParser(Byte[] ByteArr)
    {
        this.ByteArr = ByteArr;
    }

    public TorrentFile ParseTorrent()
    {
        TorrentFile torrentFile = new TorrentFile();
        return torrentFile;
    }

    public TrackerResponse ParseTrackerResponse()
    {
        TrackerResponse trackerResponse = new TrackerResponse();
        return trackerResponse;
    }
} 