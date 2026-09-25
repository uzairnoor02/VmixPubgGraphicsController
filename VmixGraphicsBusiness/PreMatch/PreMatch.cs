using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VmixData.Models;
using VmixGraphicsBusiness.Utils;

namespace VmixGraphicsBusiness.PreMatch
{
    public partial class PreMatch(vmix_graphicsContext _vmix_GraphicsContext, IConfiguration configuration, ILogger<PreMatch> logger, MatchStateStore matchState)
    {
        string logos = configuration["LogosImages"];
    }
}
