using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using AutoMapper;
using MyTorrentBackend.Dtos;
using MyTorrentBackend.Utils;

namespace MyTorrentBackend.Services
{
    public class TcpDownloader : IDownloader
    {
        private TorrentFile _torrentFile;
        private IMapper _mapper;
        private ConcurrentDictionary<int, byte[]> pieces = new ConcurrentDictionary<int, byte[]>();
        private Dictionary<int, byte[]> Incompletepieces = new Dictionary<int, byte[]>();
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
            const int TIMEOUT_MS = 3000;

            try
            {
                string ip = peerIp.Split(":")[0];
                int port = Convert.ToInt32(peerIp.Split(":")[1]);

                TcpClient client = new TcpClient();

                using (var cts = new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(TIMEOUT_MS)
                ))
                {
                    await client.ConnectAsync(
                        ip,
                        port,
                        cts.Token
                    );
                }

                NetworkStream stream = client.GetStream();

                bool isHandshakeSuccess = await PerformHandshake(stream);

                if (isHandshakeSuccess)
                {
                    await HandlePeerMessages(client, stream);
                }

            }
            // Timeout reached
            catch (OperationCanceledException e)
            {
                // Do something in case of a timeout
            }
            // Network-related error
            catch (SocketException e)
            {
                // Do something about other communication issues
            }
            // Some argument-related error, disposed object, ...
            catch (Exception e)
            {
                // Do something about other errors
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

        private async Task<bool> PerformHandshake(Stream stream)
        {
            byte[] data = PeerReqUtil.getHandshakeReq(_torrentFile.InfoHash);
            await stream.WriteAsync(data, 0, data.Length);

            byte[] ResponseBuff = new byte[data.Length];
            await stream.ReadAsync(ResponseBuff, 0, ResponseBuff.Length);

            return _torrentFile.InfoHash.SequenceEqual(PeerReqUtil.getInfoHashFromHandshake(ResponseBuff));
        }

        private async Task HandlePeerMessages(TcpClient client, NetworkStream stream)
        {
            int PeerRetry = 0;
            BitArray bitArray = null;

            while (client.Connected)
            {
                byte[] lengthBuff = new byte[4];
                await stream.ReadAsync(lengthBuff, 0, lengthBuff.Length);
                Array.Reverse(lengthBuff);
                int msgSize = BitConverter.ToInt32(lengthBuff, 0);

                if (msgSize > 0)
                {
                    byte[] msgBuff = new byte[msgSize];
                    await stream.ReadAsync(msgBuff, 0, msgBuff.Length);

                    switch (msgBuff.FirstOrDefault())
                    {
                        case (byte)MessageTypes.MsgBitfield:
                            bitArray = ProcessBitfield(msgBuff);
                            await SendInterestedMsg(stream);
                            break;
                        case (byte)MessageTypes.MsgUnchoke:
                            if (bitArray != null)
                            {
                                int pieceIndex = GetFirstAvailablePiece(bitArray);
                                if (pieceIndex != -1)
                                {
                                    await SendPieceRequest(stream, pieceIndex);
                                }
                            }
                            break;
                        case (byte)MessageTypes.MsgPiece:
                            if (bitArray != null)
                            {
                                await RecievePieceResponse(stream, msgBuff, bitArray);
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    PeerRetry++;
                    //await SendKeepAliveMsg(stream);
                }
            }
        }

        private int GetFirstAvailablePiece(BitArray bitArray)
        {
            if (bitArray.Count > 0)
            {
                for (int i = 0; i < bitArray.Count; i++)
                {
                    if (bitArray[i] == true && !pieces.ContainsKey(i))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private async Task RecievePieceResponse(NetworkStream stream1, byte[] msgBuff, BitArray bitArray)
        {
            throw new NotImplementedException();
        }

        private async Task SendKeepAliveMsg(NetworkStream stream)
        {
            throw new NotImplementedException();
        }

        private async Task SendPieceRequest(NetworkStream stream, int PieceIndex)
        {
             const int blockSize = 16384; //16KB 
             byte[] InterestedMsg = [(byte)MessageTypes.MsgRequest];

            // for (int i = 0; i < _torrentFile.PieceLength / blockSize; i++)
            // {
            //     byte[] payload = InterestedMsg
            //                             .Concat(BitConverter.GetBytes(PieceIndex).Reverse()) //to BigEndian Network Order
            //                             .Concat(BitConverter.GetBytes(i * blockSize).Reverse())
            //                             .Concat(BitConverter.GetBytes(Math.Min(blockSize, _torrentFile.PieceLength - i)).Reverse())
            //                             .ToArray();

            //     byte[] RequestBytes = BitConverter.GetBytes(payload.Length).Reverse().Concat(payload).ToArray();
            //     await stream.WriteAsync(RequestBytes, 0, RequestBytes.Length);
            //     break;
            // }
                byte[] payload = InterestedMsg
                                        .Concat(BitConverter.GetBytes(PieceIndex).Reverse()) //to BigEndian Network Order
                                        .Concat(BitConverter.GetBytes(0).Reverse())
                                        .Concat(BitConverter.GetBytes(blockSize).Reverse())
                                        .ToArray();

                byte[] RequestBytes = BitConverter.GetBytes(payload.Length).Reverse().Concat(payload).ToArray();
                await stream.WriteAsync(RequestBytes, 0, RequestBytes.Length);
        }

        private BitArray ProcessBitfield(byte[] msgBuff)
        {
            return new BitArray(msgBuff.Skip(1).ToArray());
        }

        private async Task SendInterestedMsg(NetworkStream stream)
        {
            byte[] InterestedMsg = [(byte)MessageTypes.MsgInterested];
            byte[] RequestBytes = BitConverter.GetBytes(InterestedMsg.Length)
                                    .Reverse() //to BigEndian Network Order
                                    .Concat(InterestedMsg)
                                    .ToArray();
            await stream.WriteAsync(RequestBytes, 0, RequestBytes.Length);
        }
    }
}