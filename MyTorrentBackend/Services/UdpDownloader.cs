
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public class UdpDownloader : IDownloader
    {
        public UdpDownloader(TorrentFile torrentFile)
        {

        }

        public Task Download()
        {
            throw new NotImplementedException();
        }

        public Task GetPeers(string AnnounnceUrls)
        {
            throw new NotImplementedException();
        }
    }
}