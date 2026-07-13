using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private SettingsService()
    {
        Load(FileService.LoadSettingsJson());
    }

    public void Load(string json)
    {
        var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
        Items.Clear();
        if (wrapper?.Setting == null) return;

        foreach (var item in wrapper.Setting)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            Items.Add(new Settings
            {
                Name   = item.Name,
                Desc   = item.Desc,
                Cheked = item.Cheked,
            });
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