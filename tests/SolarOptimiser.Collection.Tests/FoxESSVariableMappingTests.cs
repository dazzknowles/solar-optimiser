using SolarOptimiser.Domain;

namespace SolarOptimiser.Collection.Tests;

[TestClass]
public sealed class FoxESSVariableMappingTests
{
    public static IEnumerable<object[]> DirectMappings
    {
        get
        {
            yield return new object[] { "SoC", TelemetryQuantity.BatterySOC };
            yield return new object[] { "invBatPower", TelemetryQuantity.BatteryPowerSigned };
            yield return new object[] { "batDischargePower", TelemetryQuantity.BatteryDischargePower };
            yield return new object[] { "batChargePower", TelemetryQuantity.BatteryChargePower };
            yield return new object[] { "pvPower", TelemetryQuantity.PVPowerTotal };
            yield return new object[] { "PVEnergyTotal", TelemetryQuantity.PVEnergyTotalCumulative };
            yield return new object[] { "feedinPower", TelemetryQuantity.GridExportPower };
            yield return new object[] { "gridConsumptionPower", TelemetryQuantity.GridImportPower };
            yield return new object[] { "loadsPower", TelemetryQuantity.LoadPower };
            yield return new object[] { "loads", TelemetryQuantity.LoadEnergyCumulative };
            yield return new object[] { "meterPower", TelemetryQuantity.MeterPower };
            yield return new object[] { "meterPower2", TelemetryQuantity.MeterPower2 };
            yield return new object[] { "generationPower", TelemetryQuantity.GenerationPowerAC };
            yield return new object[] { "generation", TelemetryQuantity.GenerationEnergyCumulative };
        }
    }

    [TestMethod]
    [DynamicData(nameof(DirectMappings))]
    public void TryMap_DirectVariable_ReturnsSpecifiedQuantity(string variable, TelemetryQuantity expected)
    {
        bool mapped = FoxESSVariableMapping.TryMap(variable, out TelemetryQuantity quantity, out string? channel);

        Assert.IsTrue(mapped);
        Assert.AreEqual(expected, quantity);
        Assert.IsNull(channel);
    }

    [TestMethod]
    [DataRow("pv1Power", "1")]
    [DataRow("pv3Power", "3")]
    [DataRow("pv24Power", "24")]
    public void TryMap_PvStringPower_PreservesRawChannel(string variable, string expectedChannel)
    {
        bool mapped = FoxESSVariableMapping.TryMap(variable, out TelemetryQuantity quantity, out string? channel);

        Assert.IsTrue(mapped);
        Assert.AreEqual(TelemetryQuantity.PVStringPower, quantity);
        Assert.AreEqual(expectedChannel, channel);
    }

    [TestMethod]
    [DataRow("PV1Power")]
    [DataRow("pv1Volt")]
    [DataRow("unknown")]
    [DataRow("")]
    public void TryMap_UnrecognisedOrWrongCaseVariable_ReturnsFalse(string variable)
    {
        bool mapped = FoxESSVariableMapping.TryMap(variable, out TelemetryQuantity _, out string? channel);

        Assert.IsFalse(mapped);
        Assert.IsNull(channel);
    }
}
