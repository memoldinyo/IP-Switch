using IPSwitch.Core.Models;
using IPSwitch.Core.Storage;

namespace IPSwitch.Core.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "IPSwitchTests", Guid.NewGuid().ToString("N"));

    public StorageTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string SamplePath => Path.Combine(AppContext.BaseDirectory, "TestData", "beispielprofile.json");

    private ProfileStore NewStore()
    {
        var store = new ProfileStore(Path.Combine(_dir, "profiles.json"));
        Assert.Null(store.Load());
        return store;
    }

    [Fact]
    public void Save_PersistsAndReloads()
    {
        var store = NewStore();
        var result = store.Save(new IpProfile { Name = " Labor ", IpAddress = "10.0.0.5", SubnetMask = "8", Description = "Test" });
        Assert.True(result.IsValid);

        var reloaded = new ProfileStore(store.FilePath);
        Assert.Null(reloaded.Load());
        var profile = Assert.Single(reloaded.Profiles);
        Assert.Equal("Labor", profile.Name);
        Assert.Equal("10.0.0.5", profile.IpAddress);
        Assert.Equal("Test", profile.Description);
    }

    [Fact]
    public void Save_RejectsInvalidProfileWithoutWriting()
    {
        var store = NewStore();
        var result = store.Save(new IpProfile { Name = "Kaputt", IpAddress = "999.1.1.1", SubnetMask = "24" });
        Assert.False(result.IsValid);
        Assert.Empty(store.Profiles);
        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void Save_UpdatesExistingProfile()
    {
        var store = NewStore();
        var profile = new IpProfile { Name = "A", UseDhcp = true };
        store.Save(profile);
        profile.Name = "B";
        store.Save(profile);
        Assert.Equal("B", Assert.Single(store.Profiles).Name);
    }

    [Fact]
    public void Duplicate_CreatesUniqueNames()
    {
        var store = NewStore();
        var profile = new IpProfile { Name = "Labor", UseDhcp = true };
        store.Save(profile);

        var first = store.Duplicate(profile.Id);
        var second = store.Duplicate(profile.Id);

        Assert.Equal("Labor (Kopie)", first.Name);
        Assert.Equal("Labor (Kopie 2)", second.Name);
        Assert.NotEqual(profile.Id, first.Id);
        Assert.Equal(3, store.Profiles.Count);
    }

    [Fact]
    public void Delete_RemovesProfile()
    {
        var store = NewStore();
        var profile = new IpProfile { Name = "Weg", UseDhcp = true };
        store.Save(profile);
        Assert.True(store.Delete(profile.Id));
        Assert.Empty(store.Profiles);
        Assert.False(store.Delete(profile.Id));
    }

    [Fact]
    public void Load_CorruptFile_KeepsCopyAndReportsError()
    {
        var path = Path.Combine(_dir, "profiles.json");
        File.WriteAllText(path, "{ kein json");
        var store = new ProfileStore(path);

        var error = store.Load();

        Assert.NotNull(error);
        Assert.Empty(store.Profiles);
        Assert.Single(Directory.GetFiles(_dir, "profiles.json.defekt-*"));
    }

    [Fact]
    public void Import_SampleFile_ImportsAllProfiles()
    {
        var store = NewStore();
        var report = ProfileTransfer.Import(SamplePath, store);

        Assert.Equal(6, report.Imported);
        Assert.Empty(report.Skipped);
        Assert.Contains(store.Profiles, p => p.Name == "SPS-Inbetriebnahme" && p.SubnetMask == "24");
    }

    [Fact]
    public void Import_Twice_RenamesConflictsAndAssignsNewIds()
    {
        var store = NewStore();
        ProfileTransfer.Import(SamplePath, store);
        var report = ProfileTransfer.Import(SamplePath, store);

        Assert.Equal(6, report.Imported);
        Assert.Equal(6, report.Renamed.Count);
        Assert.Contains(store.Profiles, p => p.Name == "Büro (DHCP) (2)");
        Assert.Equal(12, store.Profiles.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Import_SkipsInvalidProfiles()
    {
        var file = Path.Combine(_dir, "import.json");
        File.WriteAllText(file, """
            [
              { "name": "Gut", "useDhcp": true },
              { "name": "Schlecht", "ipAddress": "300.1.1.1", "subnetMask": "24" },
              { "name": "", "useDhcp": true }
            ]
            """);
        var store = NewStore();

        var report = ProfileTransfer.Import(file, store);

        Assert.Equal(1, report.Imported);
        Assert.Equal(2, report.Skipped.Count);
        Assert.Equal("Gut", Assert.Single(store.Profiles).Name);
    }

    [Fact]
    public void Import_UnknownFormat_Throws()
    {
        var file = Path.Combine(_dir, "fremd.json");
        File.WriteAllText(file, """{ "format": "Etwas anderes", "profiles": [] }""");
        Assert.Throws<InvalidDataException>(() => ProfileTransfer.Import(file, NewStore()));
    }

    [Fact]
    public void Import_InvalidJson_Throws()
    {
        var file = Path.Combine(_dir, "kaputt.json");
        File.WriteAllText(file, "nicht json");
        Assert.Throws<InvalidDataException>(() => ProfileTransfer.Import(file, NewStore()));
    }

    [Fact]
    public void ExportImport_RoundTripPreservesFields()
    {
        var source = NewStore();
        var profile = new IpProfile
        {
            Name = "Rundreise",
            Description = "Beschreibung mit Umlauten äöü",
            IpAddress = "172.16.0.10",
            SubnetMask = "255.255.0.0",
            Gateway = "172.16.0.1",
            PreferredDns = "172.16.0.1",
            AlternateDns = "8.8.8.8",
            Adapter = new AdapterBinding { InterfaceId = Guid.NewGuid().ToString("B"), MacAddress = "00-11-22-33-44-55", LastKnownName = "Ethernet" },
        };
        source.Save(profile);
        var file = Path.Combine(_dir, "export.json");
        ProfileTransfer.Export(file, source.Profiles);

        var target = new ProfileStore(Path.Combine(_dir, "other.json"));
        target.Load();
        ProfileTransfer.Import(file, target);

        var imported = Assert.Single(target.Profiles);
        Assert.Equal(profile.Description, imported.Description);
        Assert.Equal(profile.Gateway, imported.Gateway);
        Assert.Equal(profile.AlternateDns, imported.AlternateDns);
        Assert.Equal(profile.Adapter.InterfaceId, imported.Adapter?.InterfaceId);
    }

    [Fact]
    public void BackupStore_ListsNewestFirstAndPrunes()
    {
        var store = new BackupStore(Path.Combine(_dir, "backups"));
        var id = Guid.NewGuid().ToString("B");
        var start = DateTimeOffset.Now.AddHours(-1);
        for (var i = 0; i < BackupStore.MaxBackupsPerAdapter + 5; i++)
        {
            store.Save(new ConfigurationBackup
            {
                InterfaceId = id,
                CreatedAt = start.AddSeconds(i),
                Reason = $"#{i}",
                Configuration = new AdapterConfiguration { DhcpEnabled = true, DnsFromDhcp = true },
            });
        }

        store.Save(new ConfigurationBackup { InterfaceId = Guid.NewGuid().ToString("B") });

        var list = store.List(id);
        Assert.Equal(BackupStore.MaxBackupsPerAdapter, list.Count);
        Assert.Equal($"#{BackupStore.MaxBackupsPerAdapter + 4}", list[0].Reason);
        Assert.Equal(list[0].Reason, store.Latest(id)?.Reason);
        Assert.Equal(BackupStore.MaxBackupsPerAdapter + 1, store.List().Count);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var path = Path.Combine(_dir, "settings.json");
        new AppSettings { ShowAllAdapters = true, ShowNotifications = false }.Save(path);
        var loaded = AppSettings.Load(path);
        Assert.True(loaded.ShowAllAdapters);
        Assert.False(loaded.ShowNotifications);
    }
}
