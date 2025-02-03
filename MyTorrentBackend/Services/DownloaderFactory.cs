using AutoMapper;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public class DownloaderFactory
    {
        public IDownloader GetDownloader(TorrentFile torrentFile,IMapper mapper)
        {
            if (torrentFile.AnnounceList.Count == 0)
            {
                if (torrentFile.Announce.StartsWith("http"))
                {
                    return new TcpDownloader(torrentFile,mapper);
                }
                else if (torrentFile.Announce.StartsWith("udp"))
                {
                    return new UdpDownloader(torrentFile,mapper);
                }
                else
                {
                    throw new ArgumentException("Invalid Announce Url!");
                }
            }
            else
            {
                List<List<string>> httpTrackers = new List<List<string>>();
                List<List<string>> udpTrackers = new List<List<string>>();

                foreach (var item in torrentFile.AnnounceList)
                {
                    if (item[0].StartsWith("http"))
                    {
                        httpTrackers.Add(new List<string> { item[0] });
                    }
                    else if (item[0].StartsWith("udp"))
                    {
                        udpTrackers.Add(new List<string> { item[0] });
                    }

                }

                if (httpTrackers.Count > 0)
                {
                    torrentFile.AnnounceList = httpTrackers;
                    return new TcpDownloader(torrentFile,mapper);
                }
                else
                {
                    torrentFile.AnnounceList = udpTrackers;
                    return new UdpDownloader(torrentFile,mapper);
                }
            }
        }
    }
}