
using AutoMapper;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public class UdpDownloader : IDownloader
    {
        public UdpDownloader(TorrentFile torrentFile, IMapper mapper)
        {

        }

        public Task Download()
        {
            throw new NotImplementedException();
        }

        public Task<TrackerResponse> GetPeers(string AnnounnceUrls)
        {
            throw new NotImplementedException();
        }
    }
}