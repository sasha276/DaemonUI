using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using testdaemon.Models;

namespace testdaemon.Service;

public partial class CommandNode : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _body = string.Empty;

    public ObservableCollection<CommandNode>? Children { get; set; }

    public bool IsGroup => Children != null;
}


file class CommandBodyDto
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

file class CommandsWrapper
{
    public Dictionary<string, List<CommandBodyDto>> Commands { get; set; } = new();
}

public class CommandsService
{
    public ObservableCollection<CommandNode> Groups { get; } = [];

    public void Load(string json)
    {
        var wrapper = JsonSerializer.Deserialize<CommandsWrapper>(json);
        Groups.Clear();
        if (wrapper?.Commands == null) return;

        foreach (var (groupName, items) in wrapper.Commands)
        {
            var children = new ObservableCollection<CommandNode>();
            foreach (var item in items)
            {
                children.Add(new CommandNode
                {
                    Name = item.Name,
                    Id   = item.Id,
                    Body = item.Body,
                });
            }

            Groups.Add(new CommandNode
            {
                Name     = groupName,
                Children = children, // не null — это группа
            });
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

    public static string LoadSettingsJson()
    {
        using var stream = AssetLoader.Open(new Uri("avares://testdaemon/Assets/Settings.json"));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}