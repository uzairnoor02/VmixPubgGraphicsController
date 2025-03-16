using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmixData.Models.MatchModels
{
    public class LiveTeamPointStats
    {
        public int teamid { get; set; }
        public int score { get; set; }
        public string teamImage { get; set; }
        public string teamName { get; set; }
        public int totalScore { get; set; } = 0;

    }
}
