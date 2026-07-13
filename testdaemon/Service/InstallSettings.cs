using System;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace testdaemon.Service;

public partial class InstallSettings : ObservableObject
{
    private static readonly Lazy<InstallSettings> _instance = new(() => new InstallSettings());
    public static InstallSettings Instance => _instance.Value;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "testdaemon", "install.json");

    [ObservableProperty] private string? _appdFolder;

    private bool _loading;

    private InstallSettings()
    {
        _loading = true;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<InstallData>(json);
                AppdFolder = data?.AppdFolder;
            }
        }
        catch {  }
        finally { _loading = false; }
    }

    partial void OnAppdFolderChanged(string? value)
    {
        if (_loading) return; 
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new InstallData { AppdFolder = value },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch {  }
    }

    private class InstallData
    {
        public string? AppdFolder { get; set; }
    }
}