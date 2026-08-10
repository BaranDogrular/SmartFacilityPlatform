using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Processors;
using SmartFacility.Application.Tests.TestData;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Application.Tests;

public sealed class ScadaAlarmImportProcessorTests
{
    [Fact]
    public async Task Invalid_datetime_does_not_discard_alarm()
    {
        var profile = TestProfiles.ScadaAlarm();
        var processor = new ScadaAlarmImportProcessor();
        var row = RawRowFactory.Row(
            "SCADA",
            2,
            RawRowFactory.Text("G", "Communication alarm"),
            RawRowFactory.Number("H", 45248),
            RawRowFactory.Text("I", "15.:15"));

        var result = await processor.ProcessAsync(row, profile, CancellationToken.None);
        var alarm = Assert.IsType<ScadaAlarmEvent>(result.Entity);

        Assert.Equal(ImportRowDisposition.Success, result.Disposition);
        Assert.Null(alarm.ReceivedAt);
        Assert.Contains("Received:InvalidTime", alarm.DateTimeParseStatus);
    }
}
