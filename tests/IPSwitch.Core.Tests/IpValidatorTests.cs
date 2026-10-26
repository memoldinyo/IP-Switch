using IPSwitch.Core.Validation;

namespace IPSwitch.Core.Tests;

public class IpValidatorTests
{
    [Theory]
    [InlineData("192.168.1.10", 0xC0A8010Au)]
    [InlineData(" 10.0.0.1 ", 0x0A000001u)]
    [InlineData("255.255.255.255", 0xFFFFFFFFu)]
    [InlineData("0.0.0.0", 0u)]
    public void TryParseIPv4_AcceptsValidAddresses(string input, uint expected)
    {
        Assert.True(IpValidator.TryParseIPv4(input, out var value, out var error));
        Assert.Null(error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.256")]
    [InlineData("192.168.01.1")]
    [InlineData("10.1")]
    [InlineData("0x0A.0.0.1")]
    [InlineData("192.168.1.1.1")]
    [InlineData("a.b.c.d")]
    [InlineData("192.168.1.-1")]
    public void TryParseIPv4_RejectsInvalidInput(string input)
    {
        Assert.False(IpValidator.TryParseIPv4(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("24", 24)]
    [InlineData("/24", 24)]
    [InlineData(" / 16 ", 16)]
    [InlineData("255.255.255.0", 24)]
    [InlineData("255.255.255.252", 30)]
    [InlineData("255.255.255.255", 32)]
    [InlineData("255.0.0.0", 8)]
    [InlineData("1", 1)]
    public void TryParseSubnet_AcceptsMaskAndPrefix(string input, int expected)
    {
        Assert.True(IpValidator.TryParseSubnet(input, out var prefix, out _));
        Assert.Equal(expected, prefix);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("33")]
    [InlineData("/40")]
    [InlineData("255.0.255.0")]
    [InlineData("255.255.255.1")]
    [InlineData("0.0.0.0")]
    [InlineData("abc")]
    [InlineData("")]
    public void TryParseSubnet_RejectsInvalidInput(string input)
    {
        Assert.False(IpValidator.TryParseSubnet(input, out _, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(24, "255.255.255.0")]
    [InlineData(30, "255.255.255.252")]
    [InlineData(32, "255.255.255.255")]
    [InlineData(8, "255.0.0.0")]
    public void PrefixToMask_ProducesDottedMask(int prefix, string mask)
    {
        Assert.Equal(mask, IpValidator.PrefixToMask(prefix));
        Assert.Equal(prefix, IpValidator.MaskToPrefix(mask));
    }

    [Theory]
    [InlineData("192.168.1.0", 24)]
    [InlineData("192.168.1.255", 24)]
    [InlineData("127.0.0.1", 8)]
    [InlineData("224.0.0.5", 24)]
    [InlineData("0.1.2.3", 8)]
    [InlineData("250.1.1.1", 8)]
    public void ValidateHostAddress_RejectsUnusableAddresses(string ip, int prefix)
    {
        IpValidator.TryParseIPv4(ip, out var value, out _);
        Assert.NotNull(IpValidator.ValidateHostAddress(value, prefix));
    }

    [Theory]
    [InlineData("192.168.1.1", 24)]
    [InlineData("10.0.0.0", 31)]
    [InlineData("10.0.0.1", 31)]
    [InlineData("172.16.5.4", 32)]
    [InlineData("169.254.10.10", 16)]
    public void ValidateHostAddress_AcceptsUsableAddresses(string ip, int prefix)
    {
        IpValidator.TryParseIPv4(ip, out var value, out _);
        Assert.Null(IpValidator.ValidateHostAddress(value, prefix));
    }

    [Fact]
    public void ValidateGateway_RequiresSameSubnet()
    {
        IpValidator.TryParseIPv4("192.168.1.10", out var ip, out _);
        IpValidator.TryParseIPv4("192.168.2.1", out var foreign, out _);
        IpValidator.TryParseIPv4("192.168.1.1", out var local, out _);

        Assert.Contains("nicht im Subnetz", IpValidator.ValidateGateway(foreign, ip, 24));
        Assert.Null(IpValidator.ValidateGateway(local, ip, 24));
        Assert.Null(IpValidator.ValidateGateway(foreign, ip, 16));
        Assert.NotNull(IpValidator.ValidateGateway(ip, ip, 24));
    }

    [Fact]
    public void ValidateDnsServer_AllowsLoopbackButNotMulticast()
    {
        IpValidator.TryParseIPv4("127.0.0.1", out var loopback, out _);
        IpValidator.TryParseIPv4("239.1.1.1", out var multicast, out _);
        Assert.Null(IpValidator.ValidateDnsServer(loopback));
        Assert.NotNull(IpValidator.ValidateDnsServer(multicast));
    }
}
