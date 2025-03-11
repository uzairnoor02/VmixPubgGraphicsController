using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;
using VmixData.Models;
using VmixData.Models.MatchModels;
using VmixGraphicsBusiness.Utils;
using VmixGraphicsBusiness.vmixutils;

namespace VmixGraphicsBusiness.PostMatchStats
{
    public partial class PostMatch
    {
        public async Task WWCDStatsAsync(LivePlayersList livePlayersList, TeamInfoList teamInfoList, int match_id)
        {
            var playerimges = ConfigGlobal.PlayerImages;
            int playernum = 1;
            var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
            List<string> apiCalls = new List<string>();

            var winnerPlayers = livePlayersList.PlayerInfoList.Where(x => x.Rank == 1).ToList();
            var winneerteam = _vmix_GraphicsContext.Teams.Where(x => x.TeamId == winnerPlayers.Select(x => x.TeamId).First().ToString()).First();

            apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"TEAMNAME1", winneerteam.TeamName));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"MATCHNumber", match_id.ToString()));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"Image1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
            apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"Image1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
            foreach (var player in winnerPlayers)
            {
                var players = _vmix_GraphicsContext.Players.Where(x => x.PlayerUid == player.UId.ToString()).First();

                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"NAMEP{playernum}", players.PlayerDisplayName));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"ELIMSP{playernum}", player.KillNum.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"DAMAGEP{playernum}", player.Damage.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"KNOCKSP{playernum}", player.Knockouts.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"ASSISTSP{playernum}", player.Assists.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"DMGTAKENP{playernum}", player.InDamage.ToString()));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"THROWABLESP{playernum}", $"{player.UseBurnGrenadeNum + player.UseFlashGrenadeNum + player.UseSmokeGrenadeNum}"));
                apiCalls.Add(vmi_LayerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"IMAGEP{playernum}", $"{playerimges}//{player.UId.ToString()}.png"));
            }

            SetTexts setTexts = new SetTexts();
            await setTexts.CallApiAsync(apiCalls);
        }


        public async Task WinnerTeamStats()
        {
            var db = _redisConnection.GetDatabase();
            var playerData = await db.StringGetAsync(HelperRedis.PlayerInfolist);
            var teamData = await db.StringGetAsync(HelperRedis.TeamInfoList);
            if (playerData.IsNullOrEmpty)
            {
                Console.WriteLine("No player data found in Redis.");
                return;
            }

            var livePlayersList = JsonSerializer.Deserialize<LivePlayersList>(playerData)!;

            // Create a lookup on team
            var teamLookup = livePlayersList.PlayerInfoList
                .GroupBy(p => p.TeamId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Check which team members are alive
            var winningTeam = teamLookup.Values
                .FirstOrDefault(team => team.All(player => player.Health > 0));

            if (winningTeam == null)
            {
                Console.WriteLine("No winning team found.");
                return;
            }

            // Generate stats for the winning team
            foreach (var player in winningTeam)
            {
                Console.WriteLine($"Player {player.Character} Stats:");
                Console.WriteLine($"Eliminations: {player.Knockouts}");
                Console.WriteLine($"Total Damage: {player.Damage}");
                Console.WriteLine($"Assists: {player.Assists}"); // Assuming assists as throwables used
                Console.WriteLine($"Damage Taken: {player.Health}");
                Console.WriteLine($"Knocks: {player.Knockouts}");
                Console.WriteLine($"Throwables Used: {player.UseBurnGrenadeNum + player.UseFlashGrenadeNum + player.UseSmokeGrenadeNum}");
            }
        }
    }
}
