using Google.Apis.Sheets.v4;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VmixPubgGraphicsController.Business;
using VmixPubgGraphicsController.Models;

namespace VmixPubgGraphicsController.Controllers;


[Route("api/[controller]")]
[ApiController]
public class LiveMatchController(IConfiguration configuration, VMIXDataoperations vMIXDataoperations) : ControllerBase
{
    LiveStatsBusiness liveStatsBusiness = new LiveStatsBusiness(configuration, vMIXDataoperations);
    [HttpPost("TotalPlayerList")] 
    public async Task<List<TeamLiveStats>> GetTotalPlayerList(PlayerInfoList livePlayerInfo)
    {
        return await  liveStatsBusiness.CreateLiveStats(livePlayerInfo);
        
    }
}
