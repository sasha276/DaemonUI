using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;

namespace testdaemon.Service;

public partial class CommandGroup : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    public ObservableCollection<CommandBody> Items { get; set; } = [];
}

public partial class CommandBody : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _body = string.Empty;
}

public class CommandsWrapper
{
    public Dictionary<string, List<CommandBody>> Commands { get; set; } = new();
}

public class CommandsService
{
    public ObservableCollection<CommandGroup> Groups { get; set; } = [];

    public void Load(string json)
    {
        var wrapper = JsonSerializer.Deserialize<CommandsWrapper>(json);
        Groups.Clear();
        if (wrapper?.Commands == null) return;

        foreach (var (key, list) in wrapper.Commands)
        {
            var group = new CommandGroup { Name = key };
            foreach (var item in list) group.Items.Add(item);
            Groups.Add(group);
        }
    }
}

public static class FileService
{
    public static string LoadCommandsJson()
    {
        using var stream = AssetLoader.Open(new Uri("avares://testdaemon/Assets/Commands.json"));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

