using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using AutoMapper;
using MyTorrentBackend.Dtos;
using MyTorrentBackend.Utils;

namespace MyTorrentBackend.Services
{
    public class TcpDownloader : IDownloader
    {
        private TorrentFile _torrentFile;
        private IMapper _mapper;
        private ConcurrentDictionary<int, byte[]> pieces;
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
                handshakeTask.Add(DownloadPieces(item));
                Console.WriteLine($"task started for {item}");
            }
            await Task.WhenAll(handshakeTask);
            return;
        }

        public async Task DownloadPieces(string peerIp)
        {
            try
            {
                string ip = peerIp.Split(":")[0];
                int port = Convert.ToInt32(peerIp.Split(":")[1]);

                byte[] data = PeerReqUtil.getHandshakeReq(_torrentFile.InfoHash);

                using TcpClient client = new TcpClient();
                client.ConnectAsync(ip, port).Wait(3000);
                if (!client.Connected)
                {
                    return;
                }
                NetworkStream stream = client.GetStream();
                Console.WriteLine($"{ip}:{port} connected");
                stream.Write(data, 0, data.Length);

                byte[] ResponseBuff = new byte[data.Length];
                int result = stream.Read(ResponseBuff, 0, ResponseBuff.Length);

                if (_torrentFile.InfoHash.SequenceEqual(PeerReqUtil.getInfoHashFromHandshake(ResponseBuff)))
                {
                    bool allPiecesDownloaded = true;
                    int peerRetry = 0;
                    while (client.Connected && allPiecesDownloaded && peerRetry < 3)
                    {
                        byte[] lengthBuff = new byte[4];
                        stream.Read(lengthBuff, 0, lengthBuff.Length);
                        int msgSize = BitConverter.ToInt32(lengthBuff.Reverse().ToArray(), 0);
                        if (msgSize > 0)
                        {
                            byte[] msgBuff = new byte[msgSize];
                            stream.Read(msgBuff, 0, msgBuff.Length);
                            if (msgBuff[0] == (byte)MessageTypes.MsgBitfield)
                            {
                                BitArray bitArray = new BitArray(msgBuff.Skip(1).Take(_torrentFile.Pieces.Count / 8).ToArray());

                                byte[] InterestedMsg = [(byte)MessageTypes.MsgInterested];
                                byte[] RequestBytes = BitConverter.GetBytes(InterestedMsg.Length)
                                                        .Reverse() //to BigEndian Network Order
                                                        .Concat(InterestedMsg)
                                                        .ToArray();
                                stream.Write(RequestBytes, 0, RequestBytes.Length);
                            }
                            else if (msgBuff[0] == (byte)MessageTypes.MsgUnchoke)
                            {
                                Console.WriteLine("we are unchocked");
                            }
                        }
                        else
                        {
                            peerRetry++;
                        }
                    }
                }

            }
            catch (System.Exception e)
            {
                Console.WriteLine($"{peerIp} {e.Message} ");
                return;
                throw;
            }

        }

        public async Task<TrackerResponse> GetPeers(string AnnounnceUrl)
        {
            using (var client = new HttpClient())
            {
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