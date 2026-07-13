using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using testdaemon.ViewModels;
using testdaemon.Views;

namespace testdaemon.Models;

public partial class StreamsHub : ObservableObject
{
    private static readonly Lazy<StreamsHub> _instance = new(() => new StreamsHub());
    public static StreamsHub Instance => _instance.Value;
    private StreamsHub() { }

    public ObservableCollection<StreamViewModel> Streams { get; } = [];
    [ObservableProperty] private StreamViewModel? _selectedStream;

    private readonly Dictionary<ushort, CanStreamView> _openWindows = new();

    public void OpenStreamWindow(StreamViewModel svm)
    {
        if (_openWindows.TryGetValue(svm.SessionId, out var existing)) { existing.Activate(); return; }
        var window = new CanStreamView(svm);
        window.Closed += (_, _) => _openWindows.Remove(svm.SessionId);
        _openWindows[svm.SessionId] = window;
        window.Show();
    }

    public void CloseStreamWindow(ushort sessionId)
    {
        if (_openWindows.TryGetValue(sessionId, out var window)) window.Close();
    }
}