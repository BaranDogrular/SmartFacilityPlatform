using System.Text.Json;
using ClosedXML.Excel;
using SmartFacility.Application.Imports.Abstractions;
using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Imports.Profiles;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Domain.Entities;
using SmartFacility.Infrastructure.Imports;

namespace SmartFacility.Application.Tests;

public sealed class ScadaWorksheetProfileTests
{
    [Fact]
    public void Electric_alarm_configuration_keeps_existing_row_boundaries()
    {
        var alarmProfile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
        var electric = alarmProfile.GetWorksheet("ELEKTRİK ARIZALARI");

        Assert.Equal(1, electric.HeaderRowNumber);
        Assert.Equal(2, electric.FirstDataRowNumber);
    }

    [Fact]
    public void Mechanical_alarm_configuration_uses_second_row_header_and_third_row_data()
    {
        var alarmProfile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
        var mechanical = alarmProfile.GetWorksheet("MEKANİK");

        Assert.Equal(2, mechanical.HeaderRowNumber);
        Assert.Equal(3, mechanical.FirstDataRowNumber);
    }

    [Fact]
    public void Scada_outage_configuration_keeps_existing_row_boundaries()
    {
        var outageProfile = new ScadaOutageImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaOutage));
        var outage = outageProfile.GetWorksheet("SCADA SÜREKLLİK");

        Assert.Equal(1, outage.HeaderRowNumber);
        Assert.Equal(2, outage.FirstDataRowNumber);
    }

    [Fact]
    public async Task Mechanical_second_row_header_is_not_classified_as_data()
    {
        var filePath = CreateScadaFixture();

        try
        {
            var profile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
            var rows = await ReadRowsAsync(filePath, profile);
            var worksheet = profile.GetWorksheet("MEKANİK");
            var mechanicalRows = rows
                .Where(row => row.SheetName == worksheet.Name)
                .ToArray();

            Assert.DoesNotContain(mechanicalRows, row => row.RowNumber == 1);

            var dataRows = mechanicalRows
                .Where(row => row.RowNumber >= worksheet.FirstDataRowNumber)
                .ToArray();
            var dataRow = Assert.Single(dataRows);
            Assert.Equal(3, dataRow.RowNumber);
            Assert.DoesNotContain(dataRows, row => row.RowNumber == 2);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Mechanical_header_validation_runs_on_second_row()
    {
        var filePath = CreateScadaFixture();

        try
        {
            var profile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
            var rows = await ReadRowsAsync(filePath, profile);
            var worksheet = profile.GetWorksheet("MEKANİK");
            var header = Assert.Single(
                rows,
                row => row.SheetName == worksheet.Name &&
                       row.RowNumber == worksheet.HeaderRowNumber);

            Assert.Equal(2, header.RowNumber);
            Assert.Empty(ProfileHeaderValidator.Validate(header, worksheet));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Mechanical_first_real_alarm_row_is_read_and_processed()
    {
        var filePath = CreateScadaFixture();

        try
        {
            var profile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
            var rows = await ReadRowsAsync(filePath, profile);
            var worksheet = profile.GetWorksheet("MEKANİK");
            var row = Assert.Single(
                rows,
                candidate => candidate.SheetName == worksheet.Name &&
                             candidate.RowNumber >= worksheet.FirstDataRowNumber);

            var decision = await new ScadaAlarmImportProcessor()
                .ProcessAsync(row, profile, CancellationToken.None);
            var alarm = Assert.IsType<ScadaAlarmEvent>(decision.Entity);

            Assert.Equal(ImportRowDisposition.Success, decision.Disposition);
            Assert.Equal("MEKANİK", alarm.SectionRaw);
            Assert.Equal("BASINÇ", alarm.AlarmType);
            Assert.Equal("Fixture alarm", alarm.Description);
            Assert.Equal(new DateTime(2026, 8, 7, 8, 30, 0), alarm.ReceivedAt);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static async Task<List<RawExcelRow>> ReadRowsAsync(
        string filePath,
        IImportSourceProfile profile)
    {
        var request = new ExcelReadRequest(
            filePath,
            profile.Worksheets
                .Select(worksheet => new WorksheetReadRequest(
                    worksheet.Name,
                    Math.Min(worksheet.HeaderRowNumber, worksheet.FirstDataRowNumber)))
                .ToArray());
        var rows = new List<RawExcelRow>();
        var reader = new ClosedXmlWorkbookReader();

        await foreach (var row in reader.ReadRowsAsync(request))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static ImportProfileOptions LoadProfileOptions(string profileKey)
    {
        var appSettingsPath = FindAppSettingsPath();
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var profile = document.RootElement
            .GetProperty("ImportProfiles")
            .GetProperty(profileKey)
            .Deserialize<ImportProfileOptions>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return profile ?? throw new InvalidOperationException(
            $"Import profile '{profileKey}' could not be loaded from appsettings.json.");
    }

    private static string FindAppSettingsPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var solutionPath = Path.Combine(directory.FullName, "SmartFacilityPlatform.sln");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(
                    directory.FullName,
                    "backend",
                    "SmartFacility.Api",
                    "appsettings.json");
            }
        }

        throw new InvalidOperationException("SmartFacilityPlatform solution root could not be located.");
    }

    private static string CreateScadaFixture()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"smart-facility-scada-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();

        var electric = workbook.AddWorksheet("ELEKTRİK ARIZALARI");
        WriteAlarmHeaders(electric, 1);
        WriteAlarmData(electric, 2, "ELEKTRİK", "SENSÖR");

        var mechanical = workbook.AddWorksheet("MEKANİK");
        mechanical.Cell("A1").Value = "MEKANİK - SCADA KONTROL";
        WriteAlarmHeaders(mechanical, 2);
        WriteAlarmData(mechanical, 3, "MEKANİK", "BASINÇ");

        var outage = workbook.AddWorksheet("SCADA SÜREKLLİK");
        var outageHeaders = new[]
        {
            "Yıl",
            "KESİNTİ NEDENİ",
            "KESİNTİ AÇIKLAMASI",
            "KESİNTİ TARİHİ",
            "KESİNTİ ZAMANI",
            "DEVREYE ALMA TARİHİ",
            "DEVREYE ALMA ZAMANI",
            "DURUM",
            "ETKİLENEN SÜRE"
        };
        for (var column = 1; column <= outageHeaders.Length; column++)
        {
            outage.Cell(1, column).Value = outageHeaders[column - 1];
        }

        outage.Cell("A2").Value = 2026;
        outage.Cell("B2").Value = "Fixture reason";
        outage.Cell("C2").Value = "Fixture outage";
        outage.Cell("D2").Value = new DateTime(2026, 8, 7);
        outage.Cell("E2").Value = new TimeSpan(8, 0, 0);
        outage.Cell("F2").Value = new DateTime(2026, 8, 7);
        outage.Cell("G2").Value = new TimeSpan(8, 15, 0);
        outage.Cell("H2").Value = "TAMAMLANDI";
        outage.Cell("I2").Value = new TimeSpan(0, 15, 0);

        workbook.SaveAs(filePath);
        return filePath;
    }

    private static void WriteAlarmHeaders(IXLWorksheet worksheet, int rowNumber)
    {
        var headers = new[]
        {
            "BÖLÜM",
            "MAHAL",
            "KAT",
            "ALARM TİPİ",
            "MÜDAHALE SEVİYESİ",
            "ZON NUMARASI",
            "AÇIKLAMA",
            "GELİŞ TARİHİ",
            "GELİŞ SAATİ",
            "GİDERİLME TARİHİ",
            "GİDERİLME SAATİ",
            "SORUMLU",
            "DURUM",
            "NOT"
        };

        for (var column = 1; column <= headers.Length; column++)
        {
            worksheet.Cell(rowNumber, column).Value = headers[column - 1];
        }
    }

    private static void WriteAlarmData(
        IXLWorksheet worksheet,
        int rowNumber,
        string section,
        string alarmType)
    {
        worksheet.Cell(rowNumber, 1).Value = section;
        worksheet.Cell(rowNumber, 2).Value = "Fixture location";
        worksheet.Cell(rowNumber, 4).Value = alarmType;
        worksheet.Cell(rowNumber, 5).Value = "NORMAL";
        worksheet.Cell(rowNumber, 7).Value = "Fixture alarm";
        worksheet.Cell(rowNumber, 8).Value = new DateTime(2026, 8, 7);
        worksheet.Cell(rowNumber, 9).Value = new TimeSpan(8, 30, 0);
        worksheet.Cell(rowNumber, 12).Value = "Fixture responsible";
        worksheet.Cell(rowNumber, 13).Value = "TAMAMLANDI";
    }
}
