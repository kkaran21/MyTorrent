using System.Collections;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
using AutoMapper;
using MyTorrentBackend.Dtos;
using MyTorrentBackend.Utils;

namespace MyTorrentBackend.Services
{
    public class TcpDownloader : IDownloader
    {
        private TorrentFile _torrentFile;
        private IMapper _mapper;
        private ConcurrentDictionary<int, byte[]> _completePieces;
        private Dictionary<int, byte[]> _incompletePieces;
        private ConcurrentQueue<int> _workQueue;
        private ConcurrentQueue<string> _failedPeers = new ConcurrentQueue<string>();

        public TcpDownloader(TorrentFile torrentFile, IMapper mapper)
        {
            _torrentFile = torrentFile;
            _mapper = mapper;
            _workQueue = new ConcurrentQueue<int>(Enumerable.Range(0, _torrentFile.Pieces.Count));
            _completePieces = new ConcurrentDictionary<int, byte[]>();
            _incompletePieces = new Dictionary<int, byte[]>();
        }

        const int TIMEOUT_MS = 3000;
        const int BLOCK_SIZE = 16384; //16KB 

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

            List<Task> IncompletedTasks = new List<Task>();

            while (_completePieces.Count != _torrentFile.Pieces.Count)
            {
                foreach (var item in _failedPeers)
                {
                    IncompletedTasks.Add(DownloadPieces(item));
                    Console.WriteLine($"task started for {item}");
                }
                await Task.WhenAll(IncompletedTasks);

            }

            return;
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
        public async Task DownloadPieces(string peerIp)
        {
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
                    Console.WriteLine("peer connected");
                    await HandlePeerMessages(client, stream);
                }

            }
            // Timeout reached
            catch (OperationCanceledException e)
            {
                _failedPeers.Enqueue(peerIp);
                // Do something in case of a timeout
            }
            // Network-related error
            catch (SocketException e)
            {
                _failedPeers.Enqueue(peerIp);

                // Do something about other communication issues
            }
            // Some argument-related error, disposed object, ...
            catch (Exception e)
            {
                _failedPeers.Enqueue(peerIp);

                // Do something about other errors
            }
            // catch (ArgumentException e)
            // {
            //     // Do something about other errors
            // }
            // catch (OutOfMemoryException e)
            // {

            // }

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
            int pieceIndex = -1;

            try
            {
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
                                    pieceIndex = GetWork(bitArray);
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
            catch (SocketException e)
            {
                _workQueue.Enqueue(pieceIndex);
                // Do something about other communication issues
            }
            // Some argument-related error, disposed object, ...
            catch (Exception e)
            {
                _workQueue.Enqueue(pieceIndex);
                // Do something about other errors
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
        private int GetWork(BitArray bitArray)
        {
            int index = -1;

            if (bitArray.Count > 0)
            {
                if (_workQueue.TryDequeue(out index))
                {
                    if (bitArray[index])
                    {
                        return index;
                    }
                    else
                    {
                        _workQueue.Enqueue(index);
                    }
                }
            }
            return index;
        }
        private async Task SendPieceRequest(NetworkStream stream, int PieceIndex)
        {
            byte[] InterestedMsg = [(byte)MessageTypes.MsgRequest];

            for (int i = 0; i < _torrentFile.PieceLength / BLOCK_SIZE; i++)
            {
                byte[] payload = InterestedMsg
                                        .Concat(BitConverter.GetBytes(PieceIndex).Reverse()) //to BigEndian Network Order
                                        .Concat(BitConverter.GetBytes(i * BLOCK_SIZE).Reverse())
                                        .Concat(BitConverter.GetBytes(Convert.ToInt32(Math.Min(BLOCK_SIZE, _torrentFile.PieceLength - i))).Reverse())
                                        .ToArray();

                byte[] RequestBytes = BitConverter.GetBytes(payload.Length).Reverse().Concat(payload).ToArray();
                await stream.WriteAsync(RequestBytes, 0, RequestBytes.Length);
            }
        }
        private async Task RecievePieceResponse(NetworkStream stream, byte[] msgBuff, BitArray bitArray)
        {
            long finalOffset = _torrentFile.PieceLength - 16384;
            int index = BitConverter.ToInt32(msgBuff.Skip(1).Take(4).Reverse().ToArray());
            int offset = BitConverter.ToInt32(msgBuff.Skip(5).Take(4).Reverse().ToArray());

            byte[] data = msgBuff.Skip(9).ToArray();
            byte[] incompleteData;

            if (_incompletePieces.ContainsKey(index))
            {
                incompleteData = _incompletePieces[index];
            }
            else
            {
                incompleteData = new byte[_torrentFile.PieceLength];
            }

            try
            {

                Array.Copy(data, 0, incompleteData, offset, BLOCK_SIZE);
            }
            catch (System.Exception)
            {

                throw;
            }

            _incompletePieces[index] = incompleteData;

            if (_torrentFile.Pieces[index].SequenceEqual(SHA1.HashData(_incompletePieces[index])))
            {
                if (_completePieces.TryAdd(index, _incompletePieces[index]))
                {
                    Console.WriteLine($"piece {index} downloaded and added");
                }
                else
                {
                    Console.WriteLine($"piece {index} downloaded but unable to add");
                }

                if (bitArray != null)
                {
                    int pieceIndex = GetWork(bitArray);
                    if (pieceIndex != -1)
                    {
                        await SendPieceRequest(stream, pieceIndex);
                    }
                }

            }

        }

        private async Task SendKeepAliveMsg(NetworkStream stream)
        {
            throw new NotImplementedException();
        }
    }
}