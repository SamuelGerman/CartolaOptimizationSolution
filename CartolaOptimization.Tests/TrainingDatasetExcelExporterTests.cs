using System.IO.Compression;
using CartolaOptimization.Application.Models;
using CartolaOptimization.Application.Utilities;

namespace CartolaOptimization.Tests;

public sealed class TrainingDatasetExcelExporterTests
{
    [Fact]
    public void ExportXlsx_WritesWorkbookWorksheetAndHeaders()
    {
        var rows = new[]
        {
            new TrainingAthleteDatasetRow
            {
                AthleteRoundId = 10,
                RoundId = 8,
                AthleteCartolaId = 123,
                AthleteName = "João, \"Teste\"",
                AthleteNickname = "JT",
                ClubCartolaId = 99,
                ClubName = "Clube A",
                PositionCartolaId = 5,
                PositionName = "Atacante",
                StatusCartolaId = 7,
                StatusName = "Provável",
                GamesNum = 3,
                HomeGamesNum = 1,
                AwayGamesNum = 2,
                MovingAverage = 4.5m,
                AverageScore = 4.0m,
                HomeAverageScore = 5.0m,
                AwayAverageScore = 3.5m,
                PriceNum = 10.25m,
                VariationNum = -0.11m,
                TargetScore = 8.75m,
                TargetWasInGame = true,
            },
        };

        var bytes = TrainingDatasetExcelExporter.ExportXlsx("full-context", rows);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        var workbook = archive.GetEntry("xl/workbook.xml");
        var styles = archive.GetEntry("xl/styles.xml");

        Assert.NotNull(worksheet);
        Assert.NotNull(workbook);
        Assert.NotNull(styles);

        using var worksheetReader = new StreamReader(worksheet!.Open());
        using var workbookReader = new StreamReader(workbook!.Open());

        var worksheetXml = worksheetReader.ReadToEnd();
        var workbookXml = workbookReader.ReadToEnd();

        Assert.Contains("AthleteRoundId", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("João, \"Teste\"", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("<c r=\"V2\" t=\"b\"><v>1</v></c>", worksheetXml, StringComparison.Ordinal);
        Assert.Contains("sheet name=\"full-context\"", workbookXml, StringComparison.Ordinal);
    }
}
