using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Profiles;

namespace SmartFacility.Application.Tests.TestData;

internal static class TestProfiles
{
    public static AssetImportProfile Asset() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.Asset,
        Worksheets =
        [
            new WorksheetProfileOptions
            {
                Name = "Assets",
                HeaderRowNumber = 1,
                FirstDataRowNumber = 2,
                ExpectedHeaders = new Dictionary<string, string>
                {
                    ["B"] = "Asset Code"
                }
            }
        ],
        Columns = new Dictionary<string, string>
        {
            ["AssetCode"] = "B",
            ["Name"] = "C",
            ["LastMaintenanceDate"] = "E",
            ["UpperAssetCode"] = "N"
        },
        RequiredFields = ["AssetCode"]
    });

    public static ScadaAlarmImportProfile ScadaAlarm() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.ScadaAlarm,
        Worksheets =
        [
            new WorksheetProfileOptions
            {
                Name = "SCADA",
                HeaderRowNumber = 1,
                FirstDataRowNumber = 2,
                ExpectedHeaders = new Dictionary<string, string> { ["G"] = "Description" }
            }
        ],
        Columns = new Dictionary<string, string>
        {
            ["Description"] = "G",
            ["ReceivedDate"] = "H",
            ["ReceivedTime"] = "I",
            ["ClearedDate"] = "J",
            ["ClearedTime"] = "K"
        }
    });

    public static HistoricalWorkOrderImportProfile HistoricalWorkOrder() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.HistoricalWorkOrder,
        Worksheets =
        [
            new WorksheetProfileOptions
            {
                Name = "Toplam İş Emri",
                HeaderRowNumber = 1,
                FirstDataRowNumber = 2,
                ExpectedHeaders = new Dictionary<string, string>
                {
                    ["A"] = "Şikayet Kodu"
                }
            }
        ],
        Columns = new Dictionary<string, string>
        {
            ["SourceReference"] = "A",
            ["LocationNameRaw"] = "C",
            ["PersonnelName"] = "D",
            ["ReportedDateTime"] = "E",
            ["Discipline"] = "K",
            ["Description"] = "M",
            ["ResolutionDurationRaw"] = "P"
        }
    });
}
