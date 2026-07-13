using System;
using CommunityToolkit.Mvvm.ComponentModel;
using testdaemon.ViewModels;

namespace testdaemon.Service;

public partial class NavigationService : ObservableObject
{
    private static readonly Lazy<NavigationService> _instance = new(() => new NavigationService());
    public static NavigationService Instance => _instance.Value;

    public HomeViewModel Home { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    [ObservableProperty] private ObservableObject _currentPage;

    private NavigationService()
    {
        CurrentPage = Home;
    }

    public void GoHome()       => CurrentPage = Home;
    public void GoToSettings() => CurrentPage = Settings;
}