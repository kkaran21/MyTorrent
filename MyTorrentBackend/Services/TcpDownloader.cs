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
        private Dictionary<int, byte[]> _incompletePieces;
        private List<int> _completedPiecesIndices;
        private ConcurrentQueue<int> _workQueue;
        private ConcurrentQueue<string> _failedPeers = new ConcurrentQueue<string>();

        public TcpDownloader(TorrentFile torrentFile, IMapper mapper)
        {
            _torrentFile = torrentFile;
            _mapper = mapper;
            _workQueue = new ConcurrentQueue<int>(Enumerable.Range(0, _torrentFile.Pieces.Count));
            _incompletePieces = new Dictionary<int, byte[]>();
            _completedPiecesIndices = new List<int>();
        }

        const int TIMEOUT_MS = 4000;
        const int BLOCK_SIZE = 16384; //16KB 

        public async Task Download()
        {
            TrackerResponse trackerResponse = await GetPeers(_torrentFile.Announce);
            List<Task> downloadTask = new List<Task>();
            foreach (var item in trackerResponse.Peers)
            {
                downloadTask.Add(DownloadPieces(item));
                Console.WriteLine($"task started for {item}");
            }

            await Task.WhenAll(downloadTask);

            List<Task> IncompletedTasks = new List<Task>();

            while (_completedPiecesIndices.Count != _torrentFile.Pieces.Count)
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
                    {"peer_id", "00112233445566778869"},
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
            catch (Exception e)
            {
                _failedPeers.Enqueue(peerIp);
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
            int pieceIndex = -1;

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
            int index;

            if (bitArray.Count > 0 && _workQueue.TryDequeue(out index))
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

            return -1;
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
            long finalOffset = _torrentFile.PieceLength - BLOCK_SIZE;
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


            Array.Copy(data, 0, incompleteData, offset, BLOCK_SIZE);

            _incompletePieces[index] = incompleteData;

            if (_torrentFile.Pieces[index].SequenceEqual(SHA1.HashData(_incompletePieces[index])))
            {
                using (var fileStream = new FileStream($"C:\\Users\\khadk\\OneDrive\\Desktop\\MyTorrent\\MyTorrentBackend\\Pieces\\{index}.piece", FileMode.OpenOrCreate, FileAccess.Write))
                {
                    await fileStream.WriteAsync(_incompletePieces[index], 0, _incompletePieces[index].Length);
                }

                _incompletePieces.Remove(index);
                _completedPiecesIndices.Add(index);

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