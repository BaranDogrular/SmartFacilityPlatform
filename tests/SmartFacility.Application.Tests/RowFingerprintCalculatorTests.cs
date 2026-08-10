using SmartFacility.Application.Imports.Models;
using SmartFacility.Application.Imports.Services;
using SmartFacility.Application.Tests.TestData;

namespace SmartFacility.Application.Tests;

public sealed class RowFingerprintCalculatorTests
{
    [Fact]
    public void Work_order_fingerprint_is_deterministic_after_normalization()
    {
        var first = RawRowFactory.Row(
            "Work Orders",
            2,
            RawRowFactory.Text("D", "117415"),
            RawRowFactory.Text("I", "Water leak"));
        var repeated = RawRowFactory.Row(
            "work orders",
            500,
            RawRowFactory.Text("D", " 117415 "),
            RawRowFactory.Text("I", "Water   leak"));

        var firstHash = RowFingerprintCalculator.Calculate(ImportSourceTypes.WorkOrder, first);
        var repeatedHash = RowFingerprintCalculator.Calculate(ImportSourceTypes.WorkOrder, repeated);

        Assert.Equal(firstHash, repeatedHash);
    }
}
