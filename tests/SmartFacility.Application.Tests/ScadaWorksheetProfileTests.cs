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
    public void Fire_alarm_configuration_uses_second_row_header_third_row_data_and_reference_date()
    {
        var alarmProfile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
        var fire = alarmProfile.GetWorksheet("YANGIN");

        Assert.Equal(2, fire.HeaderRowNumber);
        Assert.Equal(3, fire.FirstDataRowNumber);
        Assert.Equal(new DateTime(2026, 8, 7), fire.ReferenceDate);
        Assert.Equal(14, fire.ExpectedHeaders.Count);
    }

    [Fact]
    public void Energy_alarm_configuration_uses_second_row_header_third_row_data_and_reference_date()
    {
        var alarmProfile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
        var energy = alarmProfile.GetWorksheet("ENERJİ");

        Assert.Equal(2, energy.HeaderRowNumber);
        Assert.Equal(3, energy.FirstDataRowNumber);
        Assert.Equal(new DateTime(2026, 8, 7), energy.ReferenceDate);
        Assert.Equal(14, energy.ExpectedHeaders.Count);
        Assert.Equal("A", alarmProfile.Columns["SectionRaw"]);
        Assert.Equal("N", alarmProfile.Columns["Note"]);
        Assert.DoesNotContain("S", alarmProfile.Columns.Values);
        Assert.DoesNotContain("T", alarmProfile.Columns.Values);
    }

    [Fact]
    public void Campus_tracking_configuration_uses_exact_sheet_rows_and_A_through_N_mapping()
    {
        var alarmProfile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
        var campus = alarmProfile.GetWorksheet("KAMPÜS TAKİP");

        Assert.Equal("KAMPÜS TAKİP", campus.Name);
        Assert.Equal(2, campus.HeaderRowNumber);
        Assert.Equal(3, campus.FirstDataRowNumber);
        Assert.Equal(new DateTime(2026, 8, 7), campus.ReferenceDate);
        Assert.Equal(14, campus.ExpectedHeaders.Count);
        Assert.Equal("A", alarmProfile.Columns["SectionRaw"]);
        Assert.Equal("B", alarmProfile.Columns["LocationRaw"]);
        Assert.Equal("C", alarmProfile.Columns["FloorRaw"]);
        Assert.Equal("D", alarmProfile.Columns["AlarmType"]);
        Assert.Equal("E", alarmProfile.Columns["InterventionLevel"]);
        Assert.Equal("F", alarmProfile.Columns["ZoneRaw"]);
        Assert.Equal("G", alarmProfile.Columns["Description"]);
        Assert.Equal("H", alarmProfile.Columns["ReceivedDate"]);
        Assert.Equal("I", alarmProfile.Columns["ReceivedTime"]);
        Assert.Equal("J", alarmProfile.Columns["ClearedDate"]);
        Assert.Equal("K", alarmProfile.Columns["ClearedTime"]);
        Assert.Equal("L", alarmProfile.Columns["ResponsibleRaw"]);
        Assert.Equal("M", alarmProfile.Columns["StatusRaw"]);
        Assert.Equal("N", alarmProfile.Columns["Note"]);
        Assert.DoesNotContain("S", alarmProfile.Columns.Values);
        Assert.DoesNotContain("T", alarmProfile.Columns.Values);
        Assert.DoesNotContain("U", alarmProfile.Columns.Values);
    }

    [Fact]
    public async Task Fire_header_is_validated_and_not_classified_as_data()
    {
        var filePath = CreateScadaFixture();

        try
        {
            var profile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
            var rows = await ReadRowsAsync(filePath, profile);
            var worksheet = profile.GetWorksheet("YANGIN");
            var fireRows = rows.Where(row => row.SheetName == worksheet.Name).ToArray();
            var header = Assert.Single(fireRows, row => row.RowNumber == worksheet.HeaderRowNumber);
            var data = Assert.Single(fireRows, row => row.RowNumber >= worksheet.FirstDataRowNumber);

            Assert.Empty(ProfileHeaderValidator.Validate(header, worksheet));
            Assert.Equal(3, data.RowNumber);
            Assert.DoesNotContain(fireRows, row => row.RowNumber == 1);
            Assert.DoesNotContain(
                fireRows.Where(row => row.RowNumber >= worksheet.FirstDataRowNumber),
                row => row.RowNumber == worksheet.HeaderRowNumber);
        }
        finally
        {
            File.Delete(filePath);
        }
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

    [Fact]
    public async Task Energy_header_is_validated_and_first_data_row_maps_A_through_N()
    {
        var filePath = CreateScadaFixture();

        try
        {
            var profile = new ScadaAlarmImportProfile(LoadProfileOptions(ImportProfileKeys.ScadaAlarm));
            var rows = await ReadRowsAsync(filePath, profile);
            var worksheet = profile.GetWorksheet("ENERJİ");
            var energyRows = rows.Where(row => row.SheetName == worksheet.Name).ToArray();
            var header = Assert.Single(energyRows, row => row.RowNumber == worksheet.HeaderRowNumber);
            var data = Assert.Single(energyRows, row => row.RowNumber >= worksheet.FirstDataRowNumber);

            Assert.Empty(ProfileHeaderValidator.Validate(header, worksheet));
            Assert.Equal(3, data.RowNumber);
            Assert.DoesNotContain(energyRows, row => row.RowNumber == 1);

            var decision = await new ScadaAlarmImportProcessor()
                .ProcessAsync(data, profile, CancellationToken.None);
            var alarm = Assert.IsType<ScadaAlarmEvent>(decision.Entity);

            Assert.Equal("ENERJİ", alarm.SourceSheet);
            Assert.Equal("ENERJİ", alarm.SectionRaw);
            Assert.Equal("ENERJİ ALARMI", alarm.AlarmType);
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

        var fire = workbook.AddWorksheet("YANGIN");
        fire.Cell("A1").Value = "YANGIN - SCADA KONTROL";
        WriteAlarmHeaders(fire, 2);
        WriteAlarmData(fire, 3, "YANGIN", "YANGIN ALARMI");

        var energy = workbook.AddWorksheet("ENERJİ");
        energy.Cell("A1").Value = "ENERJİ - SCADA KONTROL";
        WriteAlarmHeaders(energy, 2);
        WriteAlarmData(energy, 3, "ENERJİ", "ENERJİ ALARMI");
        energy.Cell("S3").Value = "Helper intervention";
        energy.Cell("T3").Value = "Helper alarm type";

        var campus = workbook.AddWorksheet("KAMPÜS TAKİP");
        campus.Cell("A1").Value = "KAMPÜS TAKİP - SCADA KONTROL";
        WriteAlarmHeaders(campus, 2);
        WriteAlarmData(campus, 3, "KAMPÜS", "KAMPÜS ALARMI");
        campus.Cell("S3").Value = "Helper intervention";
        campus.Cell("T3").Value = "Helper alarm type";

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
