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
        public async Task WWCDStatsAsync(Match matches)
        {
            try
            {
                var playerimges = ConfigGlobal.PlayerImages;
                int playernum = 1;
                var vmixdata = await VmixDataUtils.SetVMIXDataoperations();
                List<string> apiCalls = new List<string>();



                var winnerPlayers = _vmix_GraphicsContext.PlayerStats
                    .Where(x => x.MatchId == matches.MatchId && x.StageId == matches.StageId && x.DayId == matches.MatchDayId && x.Rank == 1)
                    .ToList();
                var winneerteam = _vmix_GraphicsContext.Teams
                    .Where(x => x.TeamId == winnerPlayers.Select(x => x.TeamId).First().ToString()).First();

                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"TNAME", winneerteam.TeamName));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"MATCHNumber", matches.MatchId.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"Image1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"Image1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
                int totalTeamKills = (int)winnerPlayers.Sum(x => x.KillNum);

                int totalTeamDamage = (int)winnerPlayers.Sum(x => x.Damage);
                foreach (var player in winnerPlayers)
                {
                    double playerContribution = totalTeamKills > 0 ? (double)player.KillNum / totalTeamKills : 0;
                    var TotalCont = playerContribution.ToString("P2");
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"NAMEP{playernum}", player.PlayerName));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"MATCHN", matches.MatchId.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"ELIMSP{playernum}", player.KillNum.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"DAMAGEP{playernum}", player.Damage.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"MATCHN", matches.MatchId.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"KNOCKSP{playernum}", player.Knockouts.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"ASSISTSP{playernum}", player.Assists.ToString()));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"DMGTAKENP{playernum}", player.InDamage.ToString())); ;
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"CONTP{playernum}", TotalCont)); ;
                    apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDTEAMSTATSGuid, $"THROWABLESP{playernum}", $"{player.useBurnGrenadeNum + player.UseSmokeGrenadeNum + player.UseSmokeGrenadeNum}"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"IMAGEP{playernum}", $"{playerimges}//0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDTEAMSTATSGuid, $"IMAGEP{playernum}", $"{playerimges}//{player.PlayerUId.ToString()}.png"));
                    #region wwcd team overlay
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDoverlay, $"IMAGEP{playernum}", $"{playerimges}//0.png"));
                    apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDoverlay, $"IMAGEP{playernum}", $"{playerimges}//{player.PlayerUId.ToString()}.png"));
                    playernum++;
                }

                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"TAGT1", winneerteam.TeamName));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDoverlay, $"LOGOT1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetImageApiCall(vmixdata.WWCDoverlay, $"LOGOT1", $"{ConfigGlobal.LogosImages}//{winneerteam.TeamId}.png"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"ELIMST1", totalTeamKills.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"DAMAGET1", totalTeamDamage.ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"TOTALT1", (10 + totalTeamKills).ToString()));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"RANKT1", "#01"));
                apiCalls.Add(vmi_layerSetOnOff.GetSetTextApiCall(vmixdata.WWCDoverlay, $"/16 OR TEAMS1", "/16"));
                #endregion
                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        //public async Task WinnerTeamStats()
        //{
        //    var db = _redisConnection.GetDatabase();
        //    var playerData = await db.StringGetAsync(HelperRedis.PlayerInfolist);
        //    var teamData = await db.StringGetAsync(HelperRedis.TeamInfoList);
        //    if (playerData.IsNullOrEmpty)
        //    {
        //        Console.WriteLine("No player data found in Redis.");
        //        return;
        //    }

        //    var livePlayersList = JsonSerializer.Deserialize<LivePlayersList>(playerData)!;

        //    // Create a lookup on team
        //    var teamLookup = livePlayersList.PlayerInfoList
        //        .GroupBy(p => p.TeamId)
        //        .ToDictionary(g => g.Key, g => g.ToList());

        //    // Check which team members are alive
        //    var winningTeam = teamLookup.Values
        //        .FirstOrDefault(team => team.All(player => player.Health > 0));

        //    if (winningTeam == null)
        //    {
        //        Console.WriteLine("No winning team found.");
        //        return;
        //    }

        //    // Generate stats for the winning team
        //    foreach (var player in winningTeam)
        //    {
        //        Console.WriteLine($"Player {player.Character} Stats:");
        //        Console.WriteLine($"Eliminations: {player.Knockouts}");
        //        Console.WriteLine($"Total Damage: {player.Damage}");
        //        Console.WriteLine($"Assists: {player.Assists}"); // Assuming assists as throwables used
        //        Console.WriteLine($"Damage Taken: {player.Health}");
        //        Console.WriteLine($"Knocks: {player.Knockouts}");
        //        Console.WriteLine($"Throwables Used: {player.UseBurnGrenadeNum + player.UseFlashGrenadeNum + player.UseSmokeGrenadeNum}");
        //    }
        //}
    }
}
