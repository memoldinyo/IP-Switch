using IPSwitch.Core.Localization;
using IPSwitch.Core.Models;

namespace IPSwitch.Core.Validation;

public enum ProfileField
{
    Name,
    Description,
    IpAddress,
    SubnetMask,
    Gateway,
    PreferredDns,
    AlternateDns,
    Adapter,
}

public sealed record ValidationError(ProfileField Field, string Message);

public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;

    public IReadOnlyList<ValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public string? ErrorFor(ProfileField field) => Errors.FirstOrDefault(e => e.Field == field)?.Message;

    public string Summary => string.Join(Environment.NewLine, Errors.Select(e => "• " + e.Message));
}

/// <summary>Validiert Profile vor dem Speichern, Importieren und Anwenden.</summary>
public static class ProfileValidator
{
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 500;

    /// <param name="profile">Zu prüfendes Profil.</param>
    /// <param name="otherProfiles">Andere vorhandene Profile (für die Prüfung auf eindeutige Namen).</param>
    public static ValidationResult Validate(IpProfile profile, IEnumerable<IpProfile>? otherProfiles = null)
    {
        var errors = new List<ValidationError>();
        void Add(ProfileField field, string message) => errors.Add(new ValidationError(field, message));

        var name = profile.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            Add(ProfileField.Name, Loc.T("profile.nameEmpty"));
        }
        else if (name.Length > MaxNameLength)
        {
            Add(ProfileField.Name, Loc.T("profile.nameTooLong", MaxNameLength));
        }
        else if (name.Any(char.IsControl))
        {
            Add(ProfileField.Name, Loc.T("profile.nameControlChars"));
        }
        else if (otherProfiles is not null && otherProfiles.Any(o => o.Id != profile.Id &&
                     string.Equals(o.Name?.Trim(), name, StringComparison.CurrentCultureIgnoreCase)))
        {
            Add(ProfileField.Name, Loc.T("profile.nameExists", name));
        }

        if ((profile.Description?.Length ?? 0) > MaxDescriptionLength)
        {
            Add(ProfileField.Description, Loc.T("profile.descriptionTooLong", MaxDescriptionLength));
        }

        if (profile.Adapter is not null && !Guid.TryParse(profile.Adapter.InterfaceId, out _))
        {
            Add(ProfileField.Adapter, Loc.T("profile.adapterInvalid"));
        }

        if (!profile.UseDhcp)
        {
            ValidateStatic(profile, Add);
        }

        ValidateDns(profile, Add);
        return new ValidationResult(errors);
    }

    private static void ValidateStatic(IpProfile profile, Action<ProfileField, string> add)
    {
        var ipOk = IpValidator.TryParseIPv4(profile.IpAddress, out var ip, out var ipError);
        if (!ipOk)
        {
            add(ProfileField.IpAddress, string.IsNullOrWhiteSpace(profile.IpAddress)
                ? Loc.T("profile.ipRequired")
                : ipError!);
        }

        var maskOk = IpValidator.TryParseSubnet(profile.SubnetMask, out var prefix, out var maskError);
        if (!maskOk)
        {
            add(ProfileField.SubnetMask, string.IsNullOrWhiteSpace(profile.SubnetMask)
                ? Loc.T("profile.maskRequired")
                : maskError!);
        }

        if (ipOk && maskOk && IpValidator.ValidateHostAddress(ip, prefix) is { } hostError)
        {
            add(ProfileField.IpAddress, hostError);
            ipOk = false;
        }

        if (!string.IsNullOrWhiteSpace(profile.Gateway))
        {
            if (!IpValidator.TryParseIPv4(profile.Gateway, out var gw, out var gwError))
            {
                add(ProfileField.Gateway, Loc.T("fmt.labelled", Loc.T("label.gateway"), gwError));
            }
            else if (ipOk && maskOk && IpValidator.ValidateGateway(gw, ip, prefix) is { } gatewayError)
            {
                add(ProfileField.Gateway, gatewayError);
            }
        }
    }

    private static void ValidateDns(IpProfile profile, Action<ProfileField, string> add)
    {
        var hasPreferred = !string.IsNullOrWhiteSpace(profile.PreferredDns);
        var hasAlternate = !string.IsNullOrWhiteSpace(profile.AlternateDns);
        uint preferred = 0;

        if (hasPreferred)
        {
            if (!IpValidator.TryParseIPv4(profile.PreferredDns, out preferred, out var error))
            {
                add(ProfileField.PreferredDns, Loc.T("fmt.labelled", Loc.T("label.dnsPreferred"), error));
                hasPreferred = false;
            }
            else if (IpValidator.ValidateDnsServer(preferred, Loc.T("role.dnsPreferred")) is { } dnsError)
            {
                add(ProfileField.PreferredDns, dnsError);
            }
        }

        if (hasAlternate)
        {
            if (string.IsNullOrWhiteSpace(profile.PreferredDns))
            {
                add(ProfileField.AlternateDns, Loc.T("profile.alternateWithoutPreferred"));
            }
            else if (!IpValidator.TryParseIPv4(profile.AlternateDns, out var alternate, out var error))
            {
                add(ProfileField.AlternateDns, Loc.T("fmt.labelled", Loc.T("label.dnsAlternate"), error));
            }
            else if (IpValidator.ValidateDnsServer(alternate, Loc.T("role.dnsAlternate")) is { } dnsError)
            {
                add(ProfileField.AlternateDns, dnsError);
            }
            else if (hasPreferred && alternate == preferred)
            {
                add(ProfileField.AlternateDns, Loc.T("profile.dnsDuplicate"));
            }
        }
    }
}
