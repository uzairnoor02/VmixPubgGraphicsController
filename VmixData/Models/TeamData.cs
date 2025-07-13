
using System.Text.Json.Serialization;

namespace VmixData.Models
{
    public class TeamData
    {
        [JsonPropertyName("tournament_name")]
        public string TournamentName { get; set; }

        [JsonPropertyName("stage_name")]
        public string StageName { get; set; }

        [JsonPropertyName("teams")]
        public List<TeamInfo> Teams { get; set; }
    }

    public class TeamInfo
    {
        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("team_name")]
        public string TeamName { get; set; }
    }

    public class TeamsDataRoot
    {
        [JsonPropertyName("tournaments")]
        public List<TeamData> Tournaments { get; set; }
    }
}
