using System.Text;
using MyTorrentBackend.Dtos;

namespace MyTorrentBackend.Utils;

public class BencodeParser
{
    private Byte[] _data;
    private int _position;
    public BencodeParser(Byte[] data)
    {
        this._data = data;
        this._position = 0;
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

    private Dictionary<string, object> parseDict()
    {

        Dictionary<string, object> dict = new Dictionary<string, object>();
        consume('d');
        while (peek() != 'e')
        {
            dict.Add(parseString(), parse());
        }
        consume('e');
        return dict;
    }
}