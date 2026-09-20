using SolarOptimiser.Domain;

namespace SolarOptimiser.Domain.Tests;

[TestClass]
public sealed class DomainConstructionTests
{
    [TestMethod]
    public void Site_DefaultConstruction_UsesEmptyRequiredStringsAndNullOptionalValues()
    {
        Site site = new Site();

        Assert.AreEqual(string.Empty, site.ProviderKey);
        Assert.AreEqual(string.Empty, site.ProviderSiteID);
        Assert.AreEqual(string.Empty, site.Name);
        Assert.IsNull(site.TimeZone);
    }

    [TestMethod]
    public void Device_RoundTripsProviderIdentityAndCapabilities()
    {
        Device device = new Device
        {
            ID = 11,
            SiteID = 7,
            ProviderDeviceID = "SN-001",
            ModuleSerial = "LOGGER-9",
            Status = "Online",
            Model = "H3",
            HasPV = true,
            HasBattery = true,
            LastAlertedOutcome = "Transport"
        };

        Assert.AreEqual(11L, device.ID);
        Assert.AreEqual(7L, device.SiteID);
        Assert.AreEqual("SN-001", device.ProviderDeviceID);
        Assert.AreEqual("LOGGER-9", device.ModuleSerial);
        Assert.AreEqual("Online", device.Status);
        Assert.AreEqual("H3", device.Model);
        Assert.IsTrue(device.HasPV);
        Assert.IsTrue(device.HasBattery);
        Assert.AreEqual("Transport", device.LastAlertedOutcome);
    }

    [TestMethod]
    public void QualityEnum_ContainsOnlySpecifiedStates()
    {
        string[] names = Enum.GetNames<TelemetryQuality>();

        CollectionAssert.AreEqual(new[] { "Ok", "Missing", "Invalid", "Stale" }, names);
    }
}
