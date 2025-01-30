using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyTorrentBackend.Dtos;
using MyTorrentBackend.Utils;

namespace MyTorrentBackend.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class TorrentController : ControllerBase
{
    private readonly IMapper _mapper;

    public TorrentController(IMapper mapper)
    {
        _mapper = mapper;
    }
    [HttpPost]
    public IActionResult download([FromFormAttribute] IFormFile torrentFile)
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
            BencodeParser parser = new BencodeParser(allBytes);
            var parsedTrnt = (Dictionary<string, object>)parser.parse();
            return Ok(_mapper.Map<TorrentFile>(parsedTrnt));
        }
        catch (Exception e)
        {
            return BadRequest("Error processing torrent file.");
        }
    }

    [HttpGet]
    public void getStatus()
    {

    }

}