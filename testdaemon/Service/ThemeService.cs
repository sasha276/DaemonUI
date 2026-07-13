using System;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace testdaemon.Service;

public partial class ThemeService : ObservableObject
{
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
    public static ThemeService Instance => _instance.Value;

    [ObservableProperty] private bool _isLight;

    private ThemeService() { }

    partial void OnIsLightChanged(bool value)
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = value ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    public void Toggle() => IsLight = !IsLight;
}