namespace MyTorrentBackend.Dtos;

public class TrackerResponse
{
    public string FailureReason { get; set; }
    public string WarningMessage { get; set; }
    public int Interval { get; set; } //in seconds
    public string TrackerId { get; set; }
    public string Complete { get; set; } //Seeders
    public string Incomplete { get; set; } //Leechers
    public List<string> Peers { get; set; }

}