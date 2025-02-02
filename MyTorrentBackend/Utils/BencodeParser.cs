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
            object value = key == "pieces" ? parsePieces() : parse();
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

    private object parsePieces()
    {

        string resStr = string.Empty;
        string strLen = string.Empty;
        int strSize;
        int endPositon = Array.IndexOf(_data, (byte)':', _position);
        strLen = Encoding.ASCII.GetString(_data, _position, endPositon - _position);
        _position += endPositon - _position;
        int.TryParse(strLen, out strSize);
        consume(':');

        byte[] pieceArr = new byte[strSize];
        Array.Copy(_data, _position, pieceArr, 0, strSize);
        _position += strSize;

        return getHashArr(pieceArr);
    }

    //convert pieces bytearray to list of 20 bytes array where each item
    //in the list is hash a a piece 
    private object getHashArr(byte[] pieceArr)
    {
        List<byte[]> hashArr = new List<byte[]>();
        int chunkSize = 20;

        for (int i = 0; i < pieceArr.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, pieceArr.Length - i);
            byte[] hashChunk = new byte[20];
            Array.Copy(pieceArr, i, hashChunk, 0, length);
            hashArr.Add(hashChunk);
        }

        return hashArr;
    }
}