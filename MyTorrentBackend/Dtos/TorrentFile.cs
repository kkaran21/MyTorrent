namespace MyTorrentBackend.Dtos;
public class TorrentFile
{
    public string Announce { get; set; }
    public byte[] InfoHash { get; set; }
    public List<byte[]> Pieces { get; set; }
    public long PieceLength { get; set; }
    public long Length { get; set; }
    public string Name { get; set; }
    //for multi-files
    public List<Files> Files { get; set; }
    public List<List<string>> AnnounceList { get; set; }
}

public class Files
{
    public long length { get; set; }
    public List<string> path { get; set; }
}
