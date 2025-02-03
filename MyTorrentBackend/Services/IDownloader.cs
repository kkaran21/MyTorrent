using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public interface IDownloader
    {
        Task<TrackerResponse> GetPeers(string AnnounnceUrls);
        Task Download();

    }
}