using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using testdaemon.Models;

namespace testdaemon.Convertors;

public class LevelBrushConverter : IValueConverter
{
    public static readonly LevelBrushConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is LogLevel l ? l switch {
            LogLevel.Ok   => new SolidColorBrush(Color.Parse("#A6E3A1")),
            LogLevel.Warn => new SolidColorBrush(Color.Parse("#F9E2AF")),
            LogLevel.Err  => new SolidColorBrush(Color.Parse("#F38BA8")),
            _             => new SolidColorBrush(Color.Parse("#6C7086")),
        } : Brushes.Gray;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? new SolidColorBrush(Color.Parse("#A6E3A1"))
            : new SolidColorBrush(Color.Parse("#F38BA8"));
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotImplementedException();
}