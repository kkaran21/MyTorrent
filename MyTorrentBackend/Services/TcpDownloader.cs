using System.Collections.Concurrent;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Services
{
    public class TcpDownloader : IDownloader
    {
        private TorrentFile _torrentFile;
        private ConcurrentDictionary<string, bool> peerList;
        public TcpDownloader(TorrentFile torrentFile)
        {
            _torrentFile = torrentFile;
        }

        public async Task Download()
        {
            await GetPeers(_torrentFile.Announce);
            return;
        }

        public async Task GetPeers(string AnnounnceUrl)
        {
            using (var client = new HttpClient())
            {
                var uri = new UriBuilder(AnnounnceUrl);

                Dictionary<string, string> Params = new Dictionary<string, string>
                {
                    {"info_hash", string.Concat(_torrentFile.InfoHash.Select(b => $"%{b:X2}"))},
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
                var b = parser.parse();
            }
        }

    }
}