using System.Collections.Concurrent;
using AutoMapper;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public class TcpDownloader : IDownloader
    {
        private TorrentFile _torrentFile;
        private IMapper _mapper;
        private ConcurrentDictionary<string, bool> peerList;
        public TcpDownloader(TorrentFile torrentFile, IMapper mapper)
        {
            _torrentFile = torrentFile;
            _mapper = mapper;
        }

        public async Task Download()
        {
            TrackerResponse trackerResponse = await GetPeers(_torrentFile.Announce);
            List<Task> handshakeTask = new List<Task>();
            foreach (var item in trackerResponse.Peers)
            {
                handshakeTask.Add(InitiateHandshake(item));
            }
            await Task.WhenAll(handshakeTask);
            return;
        }

        public async Task InitiateHandshake(string peerIp)
        {

        }

        public async Task<TrackerResponse> GetPeers(string AnnounnceUrl)
        {
            using (var client = new HttpClient())
            {
                var uri = new UriBuilder(AnnounnceUrl);

                Dictionary<string, string> Params = new Dictionary<string, string>
                {
                    {"info_hash", string.Concat(_torrentFile.InfoHash.Select(b => $"%{5:X2}"))},
                    {"peer_id", "00112233445566778899"},
                    {"port", "6881"},
                    {"uploaded", "0"},
                    {"downloaded", "0"},
                    {"compact", "1"},
                    {"left", _torrentFile.Length.ToString()}
                };

                string queryParams = string.Empty;

                foreach (var item in Params)
                {
                    queryParams += $"{item.Key}={item.Value}&";
                }

                var response = await client.GetByteArrayAsync($"{AnnounnceUrl}?{queryParams}");

                Utils.BencodeParser parser = new Utils.BencodeParser(response);
                return _mapper.Map<TrackerResponse>(parser.parse());
            }
        }

    }
}