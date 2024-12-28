using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Utils;

public class BencodeParser
{
    private Stream Stream;
    public BencodeParser(Stream stream)
    {
        Stream = stream;
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