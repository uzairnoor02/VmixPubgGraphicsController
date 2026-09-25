
using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;

namespace VmixGraphicsBusiness;

public class TeamLiveStats
{
    public int TeamRank { get; set; }
    public bool TeamEliminated { get; set; }
    public string Logo { get; set; }
    public string Tag { get; set; }

    // The team's full name. NOTE: the Teams table currently has only TeamId + TeamName (no
    // separate short-tag column), so today this carries the same string as Tag - exposing it
    // under the name the web client actually wants means the overlay stops calling a full name a
    // "tag", and the day the schema gains a real short tag only the population line below changes,
    // not the overlay. Nullable so an older snapshot deserialises cleanly.
    public string? TeamName { get; set; }
    public int TotalPoints { get; set; }
    public int Eliminations { get; set; }
    public string? Player1Health { get; set; }
    public string? Player2Health { get; set; }
    public string? Player3Health { get; set; }
    public string? Player4Health { get; set; }
    public string TeamBackground { get; set; }

    // Numeric liveState (0 Normal,1 OnPlane,2 OnParachute,3 OnVehicle,4 Knocked,5 Dead,
    // 6 Disconnected - per the official PC-OB API) and health percent (0-100) alongside the
    // existing image-path fields above, so a web client (the Studio preview, the live overlay,
    // the Live tab's ALIVE/TOTAL count) can drive its own rendering instead of only being able to
    // display the pre-rendered vMix image path. Left nullable/defaulted to "dead" (5) so a team
    // with fewer than 4 players reporting doesn't read as "alive" by omission.
    public int Player1LiveState { get; set; } = 5;
    public int Player2LiveState { get; set; } = 5;
    public int Player3LiveState { get; set; } = 5;
    public int Player4LiveState { get; set; } = 5;
    public int Player1HealthPercent { get; set; }
    public int Player2HealthPercent { get; set; }
    public int Player3HealthPercent { get; set; }
    public int Player4HealthPercent { get; set; }

    /// <summary>Players on the roster (3 for a 3-man team), so the overlay shows 3 health bars
    /// instead of a phantom dead 4th player.</summary>
    public int PlayerCount { get; set; }
}


public class ExcelCreator
{
    public List<IList<object>> ReadExcelData(string filePath)
    {
        List<IList<object>> excelData = new List<IList<object>>();

        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
            ExcelWorksheet worksheet = package.Workbook.Worksheets[0]; // Assuming data is in the first worksheet

            int rowCount = worksheet.Dimension.Rows;
            int colCount = worksheet.Dimension.Columns;

            for (int row = 1; row <= rowCount; row++)
            {
                List<object> rowData = new List<object>();
                for (int col = 1; col <= colCount; col++)
                {
                    rowData.Add(worksheet.Cells[row, col].Value);
                }
                excelData.Add(rowData);
            }
        }

        return excelData;
    }
    public void CreateExcel(List<TeamLiveStats> teamLiveStats, string folderPath)
    {
        string filePath = Path.Combine(folderPath, "TeamLiveStats.xlsx");

        using (ExcelPackage package = new ExcelPackage())
        {
            // Add a new worksheet to the empty workbook
            ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Team Live Stats");

            // Add the headers
            worksheet.Cells[1, 1].Value = "Team Rank";
            worksheet.Cells[1, 2].Value = "Logo";
            worksheet.Cells[1, 3].Value = "Tag";
            worksheet.Cells[1, 4].Value = "Total Points";
            worksheet.Cells[1, 5].Value = "Eliminations";
            worksheet.Cells[1, 6].Value = "Player 1 Health";
            worksheet.Cells[1, 7].Value = "Player 2 Health";
            worksheet.Cells[1, 8].Value = "Player 3 Health";
            worksheet.Cells[1, 9].Value = "Player 4 Health";
            worksheet.Cells[1, 10].Value = "Team Background";

            // Add the team live stats data
            for (int i = 0; i < teamLiveStats.Count; i++)
            {
                var stats = teamLiveStats[i];
                worksheet.Cells[i + 2, 1].Value = stats.TeamRank;
                worksheet.Cells[i + 2, 2].Value = stats.Logo;
                worksheet.Cells[i + 2, 3].Value = stats.Tag;
                worksheet.Cells[i + 2, 4].Value = stats.TotalPoints;
                worksheet.Cells[i + 2, 5].Value = stats.Eliminations;
                worksheet.Cells[i + 2, 6].Value = stats.Player1Health;
                worksheet.Cells[i + 2, 7].Value = stats.Player2Health;
                worksheet.Cells[i + 2, 8].Value = stats.Player3Health;
                worksheet.Cells[i + 2, 9].Value = stats.Player4Health;
                worksheet.Cells[i + 2, 10].Value = stats.TeamBackground;
            }

            // Save the Excel package
            FileInfo fi = new FileInfo(filePath);
            package.SaveAs(fi);
        }
    }
}
