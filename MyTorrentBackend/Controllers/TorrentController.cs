using Microsoft.AspNetCore.Mvc;

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
            
        }
        catch(Exception e)
        {

        } 
    }

    [HttpGet]
    public void getStatus()
    {

    }

}