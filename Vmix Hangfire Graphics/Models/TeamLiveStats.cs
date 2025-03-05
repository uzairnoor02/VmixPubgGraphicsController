
using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;

namespace Vmix_Hangfire_Graphics.Models;

public class TeamLiveStats
{
    public int TeamRank { get; set; }
    public bool TeamEliminated { get; set; }
    public string Logo { get; set; }
    public string Tag { get; set; }
    public int TotalPoints { get; set; }
    public int Eliminations { get; set; }
    public string? Player1Health { get; set; }
    public string? Player2Health { get; set; }
    public string? Player3Health { get; set; }
    public string? Player4Health { get; set; }
    public string TeamBackground { get; set; }
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
