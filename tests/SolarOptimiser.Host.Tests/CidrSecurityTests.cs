using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SolarOptimiser.Host.Security;

namespace SolarOptimiser.Host.Tests;

[TestClass]
public sealed class CidrSecurityTests
{
    [TestMethod]
    [DataRow("192.168.10.0/24", "192.168.10.1", true)]
    [DataRow("192.168.10.0/24", "192.168.11.1", false)]
    [DataRow("10.0.0.4/32", "10.0.0.4", true)]
    [DataRow("::1/128", "::1", true)]
    [DataRow("2001:db8::/32", "2001:db8:1::9", true)]
    public void Contains_MatchesCidrPrefix(string cidr, string address, bool expected)
    {
        Assert.IsTrue(CidrRange.TryParse(cidr, out CidrRange? range));

        Assert.AreEqual(expected, range!.Contains(IPAddress.Parse(address)));
    }

    [TestMethod]
    public void Contains_NormalizesIpv4MappedIpv6()
    {
        Assert.IsTrue(CidrRange.TryParse("127.0.0.1/32", out CidrRange? range));

        Assert.IsTrue(range!.Contains(IPAddress.Parse("::ffff:127.0.0.1")));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("not-an-address/24")]
    [DataRow("10.0.0.0")]
    [DataRow("10.0.0.0/33")]
    [DataRow("::1/129")]
    public void TryParse_InvalidInput_ReturnsFalse(string cidr)
    {
        Assert.IsFalse(CidrRange.TryParse(cidr, out CidrRange? range));
        Assert.IsNull(range);
    }

    [TestMethod]
    public void Middleware_MissingAllowlist_FailsConstruction()
    {
        SolarHostOptions options = new SolarHostOptions { AllowedCIDRRanges = Array.Empty<string>() };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new CidrAllowlistMiddleware(_ => Task.CompletedTask, Options.Create(options)));
    }

    [TestMethod]
    public void Middleware_InvalidAllowlist_FailsConstruction()
    {
        SolarHostOptions options = new SolarHostOptions { AllowedCIDRRanges = new[] { "invalid" } };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new CidrAllowlistMiddleware(_ => Task.CompletedTask, Options.Create(options)));
    }

    [TestMethod]
    public async Task Middleware_UsesSocketPeerAndIgnoresForwardedFor()
    {
        bool called = false;
        SolarHostOptions options = new SolarHostOptions { AllowedCIDRRanges = new[] { "10.0.0.0/8" } };
        CidrAllowlistMiddleware middleware = new CidrAllowlistMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            Options.Create(options));
        DefaultHttpContext context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.2");
        context.Request.Headers["X-Forwarded-For"] = "10.1.2.3";

        await middleware.InvokeAsync(context);

        Assert.AreEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.IsFalse(called);
    }
}
