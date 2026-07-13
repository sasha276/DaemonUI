using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using testdaemon.Models;

namespace testdaemon.Service;

file class SettingDto
{
    public string Name { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public bool   Cheked { get; set; }
}

file class SettingsWrapper
{
    public List<SettingDto> Setting { get; set; } = new();
}

public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    public ObservableCollection<Settings> Items { get; } = [];

    public static class Names
    {
        public const string AutoShowPanel  = "Авто показ данных";
        public const string AutoShowWindow = "Авто показ даных в отдельлном окне";
        public const string ShowLogs       = "Показывать логи";
    }

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "testdaemon", "settings.json");

    private SettingsService()
    {
        var json = File.Exists(SettingsFilePath)
            ? File.ReadAllText(SettingsFilePath)
            : FileService.LoadSettingsJson();

        Load(json);

        if (!File.Exists(SettingsFilePath))
            Save();
    }

    public void Load(string json)
    {
        foreach (var old in Items)
            old.PropertyChanged -= OnItemPropertyChanged;

        var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
        Items.Clear();
        if (wrapper?.Setting == null) return;

        foreach (var item in wrapper.Setting)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            var setting = new Settings
            {
                Name   = item.Name,
                Desc   = item.Desc,
                Cheked = item.Cheked,
            };
            setting.PropertyChanged += OnItemPropertyChanged;
            Items.Add(setting);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.Cheked))
            Save();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);

            var wrapper = new SettingsWrapper
            {
                Setting = Items.Select(i => new SettingDto
                {
                    Name   = i.Name,
                    Desc   = i.Desc,
                    Cheked = i.Cheked,
                }).ToList()
            };

            var json = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsService.Save failed: {ex.Message}");
        }
    }

    public bool IsEnabled(string settingName)
    {
        foreach (var item in Items)
            if (item.Name == settingName) return item.Cheked;
        return false;
    }

    public Settings? Find(string settingName)
    {
        foreach (var item in Items)
            if (item.Name == settingName) return item;
        return null;
    }
}