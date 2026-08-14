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
            ["SectionRaw"] = "A",
            ["LocationRaw"] = "B",
            ["FloorRaw"] = "C",
            ["AlarmType"] = "D",
            ["InterventionLevel"] = "E",
            ["ZoneRaw"] = "F",
            ["Description"] = "G",
            ["ReceivedDate"] = "H",
            ["ReceivedTime"] = "I",
            ["ClearedDate"] = "J",
            ["ClearedTime"] = "K",
            ["ResponsibleRaw"] = "L",
            ["StatusRaw"] = "M",
            ["Note"] = "N"
        }
    });

    public static ScadaOutageImportProfile ScadaOutage() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.ScadaOutage,
        Worksheets =
        [
            new WorksheetProfileOptions
            {
                Name = "SCADA SÜREKLLİK",
                HeaderRowNumber = 1,
                FirstDataRowNumber = 2,
                ExpectedHeaders = new Dictionary<string, string> { ["B"] = "Reason" }
            }
        ],
        Columns = new Dictionary<string, string>
        {
            ["Reason"] = "B",
            ["Description"] = "C",
            ["StartedDate"] = "D",
            ["StartedTime"] = "E",
            ["RestoredDate"] = "F",
            ["RestoredTime"] = "G",
            ["StatusRaw"] = "H",
            ["DurationRaw"] = "I"
        }
    });

    public static WorkOrderImportProfile WorkOrder() => new(new ImportProfileOptions
    {
        SourceType = ImportSourceTypes.WorkOrder,
        Worksheets =
        [
            new WorksheetProfileOptions
            {
                Name = "WorkOrders",
                HeaderRowNumber = 1,
                FirstDataRowNumber = 2,
                ExpectedHeaders = new Dictionary<string, string> { ["D"] = "Work Order Number" }
            }
        ],
        Columns = new Dictionary<string, string>
        {
            ["WorkOrderNumber"] = "D",
            ["ReportedDate"] = "E",
            ["ReportedTime"] = "F"
        },
        RequiredFields = ["WorkOrderNumber"]
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
