namespace IPSwitch.Core.Models;

/// <summary>
/// Ein benanntes IPv4-Profil, das auf einen Netzwerkadapter angewendet werden kann.
/// </summary>
public sealed class IpProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>true = IP-Adresse per DHCP beziehen.</summary>
    public bool UseDhcp { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>Subnetzmaske ("255.255.255.0") oder Präfixlänge ("24" bzw. "/24").</summary>
    public string? SubnetMask { get; set; }

    public string? Gateway { get; set; }

    public string? PreferredDns { get; set; }

    public string? AlternateDns { get; set; }

    /// <summary>Optionale Zuordnung zu einem Netzwerkadapter.</summary>
    public AdapterBinding? Adapter { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.Now;

    public IpProfile Clone() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        UseDhcp = UseDhcp,
        IpAddress = IpAddress,
        SubnetMask = SubnetMask,
        Gateway = Gateway,
        PreferredDns = PreferredDns,
        AlternateDns = AlternateDns,
        Adapter = Adapter?.Clone(),
        CreatedAt = CreatedAt,
        ModifiedAt = ModifiedAt,
    };

    public override string ToString() => Name;
}
