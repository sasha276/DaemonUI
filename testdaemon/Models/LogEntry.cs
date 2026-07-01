using System;

namespace testdaemon.Models;

public enum LogLevel { Info, Ok, Warn, Err }

public record LogEntry(DateTime Time, LogLevel Level, string Message)
{
    public string Prefix => Level switch
    {
        LogLevel.Ok   => "✓",
        LogLevel.Warn => "!",
        LogLevel.Err  => "✗",
        _             => "·",
    };
    public string Display => $"[{Time:HH:mm:ss.fff}] {Prefix} {Message}";
}