using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmixData.Models;

namespace VmixGraphicsBusiness.PreMatch
{
    public partial class PreMatch(vmix_graphicsContext _vmix_GraphicsContext, IConfiguration configuration,ILogger<PreMatch> logger)
    {
        string logos = configuration["LogosImages"];
    }
}
