using IPSwitch.Core.Models;
using IPSwitch.Core.Profiles;
using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Tests;

public class ProfileValidatorTests
{
    private static IpProfile Static(string name = "Labor") => new()
    {
        Name = name,
        IpAddress = "192.168.0.50",
        SubnetMask = "255.255.255.0",
        Gateway = "192.168.0.1",
        PreferredDns = "192.168.0.1",
        AlternateDns = "9.9.9.9",
    };

    [Fact]
    public void ValidStaticProfile_HasNoErrors() => Assert.True(ProfileValidator.Validate(Static()).IsValid);

    [Fact]
    public void DhcpProfile_RequiresNoAddressFields()
    {
        var result = ProfileValidator.Validate(new IpProfile { Name = "DHCP", UseDhcp = true });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void DhcpProfile_IgnoresLeftoverStaticFields()
    {
        var profile = new IpProfile { Name = "DHCP", UseDhcp = true, IpAddress = "kaputt", SubnetMask = "99" };
        Assert.True(ProfileValidator.Validate(profile).IsValid);
    }

    [Fact]
    public void StaticProfile_RequiresAddressAndMask()
    {
        var result = ProfileValidator.Validate(new IpProfile { Name = "Leer" });
        Assert.NotNull(result.ErrorFor(ProfileField.IpAddress));
        Assert.NotNull(result.ErrorFor(ProfileField.SubnetMask));
    }

    [Fact]
    public void EmptyName_IsRejected()
    {
        var profile = Static("  ");
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.Name));
    }

    [Fact]
    public void TooLongName_IsRejected()
    {
        var profile = Static(new string('x', ProfileValidator.MaxNameLength + 1));
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.Name));
    }

    [Fact]
    public void DuplicateName_IsRejectedCaseInsensitive()
    {
        var existing = Static("Labor");
        var other = Static("LABOR");
        var result = ProfileValidator.Validate(other, [existing]);
        Assert.Contains("existiert bereits", result.ErrorFor(ProfileField.Name));
    }

    [Fact]
    public void SameProfile_DoesNotConflictWithItself()
    {
        var profile = Static();
        Assert.True(ProfileValidator.Validate(profile, [profile]).IsValid);
    }

    [Fact]
    public void GatewayOutsideSubnet_IsRejected()
    {
        var profile = Static();
        profile.Gateway = "10.0.0.1";
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.Gateway));
    }

    [Fact]
    public void AlternateDnsWithoutPreferred_IsRejected()
    {
        var profile = Static();
        profile.PreferredDns = null;
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.AlternateDns));
    }

    [Fact]
    public void IdenticalDnsServers_AreRejected()
    {
        var profile = Static();
        profile.AlternateDns = profile.PreferredDns;
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.AlternateDns));
    }

    [Fact]
    public void NetworkAddressAsIp_IsRejected()
    {
        var profile = Static();
        profile.IpAddress = "192.168.0.0";
        profile.Gateway = null;
        Assert.Contains("Netzadresse", ProfileValidator.Validate(profile).ErrorFor(ProfileField.IpAddress));
    }

    [Fact]
    public void InvalidAdapterBinding_IsRejected()
    {
        var profile = Static();
        profile.Adapter = new AdapterBinding { InterfaceId = "kein-guid" };
        Assert.NotNull(ProfileValidator.Validate(profile).ErrorFor(ProfileField.Adapter));
    }

    [Fact]
    public void ToConfiguration_ConvertsPrefixToMask()
    {
        var profile = Static();
        profile.SubnetMask = "/20";
        var config = ProfileConverter.ToConfiguration(profile);

        Assert.False(config.DhcpEnabled);
        Assert.Equal(new StaticAddress("192.168.0.50", "255.255.240.0"), config.Addresses.Single());
        Assert.Equal(["192.168.0.1"], config.Gateways);
        Assert.Equal(["192.168.0.1", "9.9.9.9"], config.DnsServers);
        Assert.False(config.DnsFromDhcp);
    }

    [Fact]
    public void ToConfiguration_DhcpWithoutDns_UsesDhcpDns()
    {
        var config = ProfileConverter.ToConfiguration(new IpProfile { Name = "x", UseDhcp = true });
        Assert.True(config.DhcpEnabled);
        Assert.True(config.DnsFromDhcp);
        Assert.Empty(config.DnsServers);
    }

    [Fact]
    public void ToConfiguration_DhcpWithDns_UsesStaticDns()
    {
        var config = ProfileConverter.ToConfiguration(new IpProfile { Name = "x", UseDhcp = true, PreferredDns = "1.1.1.1" });
        Assert.True(config.DhcpEnabled);
        Assert.False(config.DnsFromDhcp);
        Assert.Equal(["1.1.1.1"], config.DnsServers);
    }

    [Fact]
    public void ToConfiguration_InvalidProfile_Throws() =>
        Assert.Throws<InvalidOperationException>(() => ProfileConverter.ToConfiguration(new IpProfile { Name = "x" }));

    [Fact]
    public void Normalize_ClearsStaticFieldsForDhcpAndTrims()
    {
        var profile = Static();
        profile.Name = "  Labor  ";
        profile.UseDhcp = true;
        profile.Description = "   ";
        var normalized = ProfileConverter.Normalize(profile);

        Assert.Equal("Labor", normalized.Name);
        Assert.Null(normalized.IpAddress);
        Assert.Null(normalized.SubnetMask);
        Assert.Null(normalized.Gateway);
        Assert.Null(normalized.Description);
        Assert.Equal("192.168.0.1", normalized.PreferredDns);
    }

    [Fact]
    public void ConfigurationValidator_RejectsStaticWithDhcpDns()
    {
        var config = new AdapterConfiguration { DhcpEnabled = false, DnsFromDhcp = true, Addresses = [new("10.0.0.2", "255.0.0.0")] };
        Assert.NotEmpty(ConfigurationValidator.Validate(config));
    }
}
