using System.Text;

namespace MyTorrentBackend.Utils;

public class BencodeParser
{
    private Byte[] _data;
    private int _position;
    private int _infoHashStartPosition;
    private int _infoHashEndPosition;
    private bool _isInfoHashDict;
    public BencodeParser(Byte[] data)
    {
        this._data = data;
        this._position = 0;
        this._infoHashStartPosition = 0;
        this._infoHashEndPosition = 0;
        this._isInfoHashDict = false;
    }

    private char peek()
    {
        return (char)_data[_position];
    }
    private void consume(char c)
    {
        if (peek() == c)
        {
            _position++;
        }
        else
        {
            throw new Exception("consume failed");
        }
    }

    public object parse()
    {
        char current = peek();
        return current switch
        {
            'i' => parseLong(),
            'l' => parseList(),
            'd' => parseDict(),
            _ => char.IsDigit(current) ? parseString() : throw new Exception("parse failed")
        };
    }

    private string parseString()
    {

        string resStr = string.Empty;
        string strLen = string.Empty;
        int strSize;
        int endPositon = Array.IndexOf(_data, (byte)':', _position);
        strLen = Encoding.ASCII.GetString(_data, _position, endPositon - _position);
        _position += endPositon - _position;

        int.TryParse(strLen, out strSize);

        consume(':');

        resStr = Encoding.UTF8.GetString(_data, _position, strSize);
        _position += strSize;
        return resStr;
    }

    private long parseLong()
    {
        consume('i');
        long num;
        string strNum = string.Empty;
        int endPositon = Array.IndexOf(_data, (byte)'e', _position);
        strNum = Encoding.ASCII.GetString(_data, _position, endPositon - _position);
        long.TryParse(strNum, out num);
        _position += endPositon - _position;
        consume('e');
        return num;
    }


    private List<object> parseList()
    {
        List<object> list = new List<object>();
        consume('l');
        while (peek() != 'e')
        {
            list.Add(parse());
        }
        consume('e');
        return list;
    }

    private object parseDict()
    {

        Dictionary<string, object> dict = new Dictionary<string, object>();
        _infoHashStartPosition = _position;
        consume('d');
        while (peek() != 'e')
        {
            string key = parseString();

            object value;

            switch (key)
            {
                case "pieces":
                    value = parseBinaryBlob(20);
                    break;
                case "peers":
                    value = parsePeersAsString(parseBinaryBlob(6));
                    break;
                default:
                    value = parse();
                    break;
            }

            dict[key] = value;

            if (key == "info")
            {
                _isInfoHashDict = true;
                _infoHashEndPosition = _position;
            }
        }
        if (_isInfoHashDict)
        {
            byte[] infoDictBytes = new byte[_infoHashEndPosition - _infoHashStartPosition];
            Array.Copy(_data, _infoHashStartPosition, infoDictBytes, 0, _infoHashEndPosition - _infoHashStartPosition);
            var hash = System.Security.Cryptography.SHA1.HashData(infoDictBytes);
            dict["info hash"] = hash;
            _isInfoHashDict = false;
        }
        consume('e');
        return dict;
    }

    private List<string> parsePeersAsString(List<byte[]> peersBytes)
    {
        List<string> peersString = new List<string>();
        foreach (var item in peersBytes)
        {
            int portNo = item[4] << 8 | item[5];
            string ip = $"{item[0]}.{item[1]}.{item[2]}.{item[3]}:{portNo}";
            peersString.Add(ip);
        }
        return peersString;
    }

    private List<byte[]> parseBinaryBlob(int BlobSize)
    {
        string resStr = string.Empty;
        string strLen = string.Empty;
        int strSize;
        int endPositon = Array.IndexOf(_data, (byte)':', _position);
        strLen = Encoding.ASCII.GetString(_data, _position, endPositon - _position);
        _position += endPositon - _position;
        int.TryParse(strLen, out strSize);
        consume(':');

        byte[] Arr = new byte[strSize];
        Array.Copy(_data, _position, Arr, 0, strSize);
        _position += strSize;

        return getByteArrList(Arr, BlobSize);
    }

    //Converts A ByteArray to a list of fixed size byte array
    private List<byte[]> getByteArrList(byte[] Arr, int chunkSize)
    {
        List<byte[]> ArrOfChunk = new List<byte[]>();

        for (int i = 0; i < Arr.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, Arr.Length - i);
            byte[] Chunk = new byte[chunkSize];
            Array.Copy(Arr, i, Chunk, 0, length);
            ArrOfChunk.Add(Chunk);
        }

        return ArrOfChunk;
    }
}