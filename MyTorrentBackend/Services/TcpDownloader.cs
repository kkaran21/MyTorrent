using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
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
            string ip = peerIp.Split(":")[0];
            int port = Convert.ToInt32(peerIp.Split(":")[1]);

            byte[] data = BitConverter.GetBytes(19)
                        .Concat(System.Text.Encoding.ASCII.GetBytes("BitTorrent protocol"))
                        .Concat(new byte[8])
                        .Concat(_torrentFile.InfoHash).ToArray()
                        .Concat(RandomNumberGenerator.GetBytes(20)).ToArray();

            var a = System.Text.Encoding.ASCII.GetString(data,0,data.Length);
            using TcpClient client = new TcpClient();
            client.ConnectAsync(ip, port).Wait(5000);

            NetworkStream stream = client.GetStream();
            stream.Write(data,0,data.Length);

            byte[] ResponseBuff = new byte[data.Length];
            int result = stream.Read(ResponseBuff,0,ResponseBuff.Length);


            
        }

        public async Task<TrackerResponse> GetPeers(string AnnounnceUrl)
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
                return _mapper.Map<TrackerResponse>(parser.parse());
            }
        }

    }
}