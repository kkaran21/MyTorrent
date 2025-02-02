namespace MyTorrentBackend.Services
{
    public interface IDownloader
    {
        Task GetPeers(string AnnounnceUrls);
        Task Download();

    }
}