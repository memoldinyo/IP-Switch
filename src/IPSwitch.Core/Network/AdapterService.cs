using System.Net.NetworkInformation;
using System.Net.Sockets;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Network;

/// <summary>
/// Ermittelt Netzwerkadapter über System.Net.NetworkInformation (IP Helper API) und ergänzt
/// die persistente Konfiguration aus der TCP/IP-Registry. Benötigt keine Administratorrechte.
/// </summary>
public sealed class AdapterService : IAdapterLocator
{
    private static readonly string[] SecondaryAdapterMarkers =
    [
        "Wi-Fi Direct Virtual Adapter",
        "WAN Miniport",
        "Kernel Debug",
        "Microsoft Network Adapter Multiplexor",
        "Teredo",
        "6to4",
        "ISATAP",
    ];

    private readonly IConfigurationReader _configurationReader;

    public AdapterService(IConfigurationReader? configurationReader = null) =>
        _configurationReader = configurationReader ?? new TcpipRegistryReader();

    /// <param name="includeSecondary">true = auch virtuelle Hilfsadapter (Wi-Fi Direct, WAN Miniport usw.) liefern.</param>
    public IReadOnlyList<AdapterInfo> GetAdapters(bool includeSecondary = false)
    {
        var result = new List<AdapterInfo>();
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException ex)
        {
            Log.Error("Netzwerkadapter konnten nicht ermittelt werden", ex);
            return result;
        }

        foreach (var nic in interfaces)
        {
            try
            {
                var adapter = CreateAdapterInfo(nic, includeSecondary);
                if (adapter is not null)
                {
                    result.Add(adapter);
                }
            }
            catch (Exception ex) when (ex is NetworkInformationException or InvalidOperationException or PlatformNotSupportedException)
            {
                Log.Warn($"Adapter {nic.Name} wird übersprungen: {ex.Message}");
            }
        }

        return result
            .OrderByDescending(a => a.IsConnected)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AdapterInfo> GetAll() => GetAdapters(includeSecondary: true);

    public AdapterInfo? Find(string interfaceId, string? macAddress) =>
        Match(GetAdapters(includeSecondary: true), interfaceId, macAddress);

    public AdapterInfo? Find(AdapterBinding binding) => Find(binding.InterfaceId, binding.MacAddress);

    /// <summary>
    /// Sucht den Adapter zuerst über die GUID. Rückfall: eine eindeutige MAC-Adresse (z. B. erhält ein Adapter
    /// nach einer Treiber-Neuinstallation eine neue GUID, behält aber seine MAC-Adresse).
    /// </summary>
    public static AdapterInfo? Match(IEnumerable<AdapterInfo> adapters, string interfaceId, string? macAddress)
    {
        var list = adapters as IReadOnlyList<AdapterInfo> ?? adapters.ToList();
        var byId = list.FirstOrDefault(a => SameId(a.Id, interfaceId));
        if (byId is not null || string.IsNullOrWhiteSpace(macAddress))
        {
            return byId;
        }

        var byMac = list.Where(a => string.Equals(a.MacAddress, macAddress, StringComparison.OrdinalIgnoreCase)).ToList();
        return byMac.Count == 1 ? byMac[0] : null;
    }

    public static AdapterInfo? Match(IEnumerable<AdapterInfo> adapters, AdapterBinding? binding) =>
        binding is null ? null : Match(adapters, binding.InterfaceId, binding.MacAddress);

    public static bool SameId(string? a, string? b) =>
        Guid.TryParse(a, out var ga) && Guid.TryParse(b, out var gb)
            ? ga == gb
            : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static string? FormatMac(PhysicalAddress? address)
    {
        var bytes = address?.GetAddressBytes();
        return bytes is { Length: > 0 } && bytes.Any(b => b != 0)
            ? string.Join("-", bytes.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)))
            : null;
    }

    private AdapterInfo? CreateAdapterInfo(NetworkInterface nic, bool includeSecondary)
    {
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return null;
        }

        if (!includeSecondary && SecondaryAdapterMarkers.Any(m => nic.Description.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!nic.Supports(NetworkInterfaceComponent.IPv4))
        {
            return null;
        }

        var properties = nic.GetIPProperties();
        IPv4InterfaceProperties? ipv4;
        try
        {
            ipv4 = properties.GetIPv4Properties();
        }
        catch (NetworkInformationException)
        {
            return null;
        }

        if (ipv4 is null)
        {
            return null;
        }

        var addresses = properties.UnicastAddresses
            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(u => new ActiveAddress(u.Address.ToString(), u.PrefixLength, u.IPv4Mask.ToString()))
            .ToList();

        var ipv6 = properties.UnicastAddresses
            .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6)
            .Select(u => $"{u.Address}/{u.PrefixLength}")
            .ToList();

        long speed;
        try
        {
            speed = nic.Speed;
        }
        catch (PlatformNotSupportedException)
        {
            speed = -1;
        }

        return new AdapterInfo
        {
            Id = nic.Id,
            Name = nic.Name,
            Description = nic.Description,
            MacAddress = FormatMac(nic.GetPhysicalAddress()),
            InterfaceType = nic.NetworkInterfaceType,
            Status = nic.OperationalStatus,
            Speed = speed,
            IPv4Index = ipv4.Index,
            DhcpEnabled = ipv4.IsDhcpEnabled,
            Addresses = addresses,
            IPv6Addresses = ipv6,
            Gateways = properties.GatewayAddresses
                .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any))
                .Select(g => g.Address.ToString()).ToList(),
            DnsServers = properties.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString()).ToList(),
            DhcpServers = properties.DhcpServerAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString()).ToList(),
            PersistentConfiguration = _configurationReader.ReadConfiguration(nic.Id),
        };
    }
}
