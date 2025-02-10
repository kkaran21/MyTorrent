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
                    await HandlePeerMessages(client,stream);
                    // bool allPiecesDownloaded = true;
                    // int peerRetry = 0;
                    // BitArray bitArray = null;

                    // while (client.Connected && allPiecesDownloaded && peerRetry < 3)
                    // {
                    //     byte[] lengthBuff = new byte[4];
                    //     stream.Read(lengthBuff, 0, lengthBuff.Length);
                    //     Array.Reverse(lengthBuff);
                    //     int msgSize = BitConverter.ToInt32(lengthBuff);
                    //     if (msgSize > 0)
                    //     {
                    //         byte[] msgBuff = new byte[msgSize];
                    //         stream.Read(msgBuff, 0, msgBuff.Length);
                    //         if (msgBuff[0] == (byte)MessageTypes.MsgBitfield)
                    //         {

                    //             bitArray = new BitArray(msgBuff.Skip(1).ToArray());
                    //             byte[] InterestedMsg = [(byte)MessageTypes.MsgInterested];
                    //             byte[] RequestBytes = BitConverter.GetBytes(InterestedMsg.Length)
                    //                                     .Reverse() //to BigEndian Network Order
                    //                                     .Concat(InterestedMsg)
                    //                                     .ToArray();
                    //             stream.Write(RequestBytes, 0, RequestBytes.Length);

                    //         }
                    //         else if (msgBuff[0] == (byte)MessageTypes.MsgUnchoke)
                    //         {
                    //             if (bitArray.Count > 0)
                    //             {
                    //                 for (int i = 0; i < bitArray.Count; i++)
                    //                 {
                    //                     if (bitArray[i] == true && !pieces.ContainsKey(i))
                    //                     {
                    //                         byte[] InterestedMsg = [(byte)MessageTypes.MsgRequest];
                    //                         byte[] payload = InterestedMsg
                    //                                                 .Concat(BitConverter.GetBytes(i).Reverse()) //to BigEndian Network Order
                    //                                                 .Concat(BitConverter.GetBytes(0).Reverse())
                    //                                                 .Concat(BitConverter.GetBytes(16384).Reverse())
                    //                                                 .ToArray();

                    //                         byte[] RequestBytes = BitConverter.GetBytes(payload.Length).Reverse().Concat(payload).ToArray();
                    //                         stream.Write(RequestBytes, 0, RequestBytes.Length);
                    //                         //break;

                    //                         // Byte[] allBytes = new byte[_torrentFile.PieceLength];
                    //                         // int bytesRead = 0;
                    //                         // int offset = 16000;

                    //                         // while ((bytesRead = stream.Read(allBytes, offset, allBytes.Length - offset)) > 0)
                    //                         // {
                    //                         //     offset += 16000;
                    //                         // }
                    //                     }
                    //                 }
                    //             }
                    //             Console.WriteLine("we are unchocked");
                    //         }
                    //         else if (msgBuff[0] == (byte)MessageTypes.MsgPiece)
                    //         {
                    //             Console.WriteLine("Recieved Data");
                    //         }
                    //         else
                    //         {
                    //             peerRetry++;
                    //         }
                    //     }
                    //     else
                    //     {
                    //         peerRetry++;
                    //     }
                    // }
                }

            }
            // Timeout reached
            catch (OperationCanceledException)
            {
                // Do something in case of a timeout
            }
            // Network-related error
            catch (SocketException)
            {
                // Do something about other communication issues
            }
            // Some argument-related error, disposed object, ...
            catch (Exception)
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
                                await SendPieceRequest(client,stream, bitArray);  //will call HandlePeerMessages
                            }
                            break;
                        case (byte)MessageTypes.MsgPiece:
                            await RecievePieceResponse(msgBuff);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    // peerRetry++;
                    await SendKeepAliveMsg(stream);
                }
            }
        }

        private async Task RecievePieceResponse(byte[] msgBuff)
        {
            throw new NotImplementedException();
        }

        private async Task SendKeepAliveMsg(NetworkStream stream)
        {
            throw new NotImplementedException();
        }

        private async Task SendPieceRequest(TcpClient client, NetworkStream stream, BitArray bitArray)
        {
            if (bitArray.Count > 0)
            {
                for (int i = 0; i < bitArray.Count; i++)
                {
                    if (bitArray[i] == true && !pieces.ContainsKey(i))
                    {
                        byte[] InterestedMsg = [(byte)MessageTypes.MsgRequest];
                        byte[] payload = InterestedMsg
                                                .Concat(BitConverter.GetBytes(i).Reverse()) //to BigEndian Network Order
                                                .Concat(BitConverter.GetBytes(0).Reverse())
                                                .Concat(BitConverter.GetBytes(16384).Reverse())
                                                .ToArray();

                        byte[] RequestBytes = BitConverter.GetBytes(payload.Length).Reverse().Concat(payload).ToArray();
                        await stream.WriteAsync(RequestBytes, 0, RequestBytes.Length);
                        await HandlePeerMessages(client,stream);
                    }
                }
            }
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