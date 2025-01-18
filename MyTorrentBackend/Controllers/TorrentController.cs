using Microsoft.AspNetCore.Mvc;
using MyTorrentBackend.Utils;

namespace MyTorrentBackend.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class TorrentController : ControllerBase
{
    [HttpPost]
    public void download([FromFormAttribute] IFormFile torrentFile)
    {
        try
        {
            Stream stream = torrentFile.OpenReadStream();
            Byte[] allBytes = new byte[stream.Length];
            int bytesRead = 0;
            int offset = 0;

            while ((bytesRead = stream.Read(allBytes, offset, allBytes.Length - offset)) > 0)
            {
                offset += bytesRead;
            }

            // Console.WriteLine((char)allBytes[0]);
            // Console.WriteLine(System.Text.Encoding.UTF8.GetString(allBytes, 0, allBytes.Length));
            BencodeParser parser = new BencodeParser(allBytes);
            var a = parser.parse();

        }
        catch (Exception e)
        {

        }
    }

    [HttpGet]
    public void getStatus()
    {

    }

}