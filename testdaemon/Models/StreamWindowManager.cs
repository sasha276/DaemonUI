using System.Collections.Generic;
using testdaemon.Views;

namespace testdaemon.ViewModels;

public static class StreamWindowManager
{
    private static readonly Dictionary<ushort, CanStreamView> _openWindows = new();

    public static void Open(StreamViewModel svm)
    {
        if (_openWindows.TryGetValue(svm.SessionId, out var existing))
        {
            existing.Activate(); 
            return;
        }

        var window = new CanStreamView(svm);
        window.Closed += (_, _) =>
        {
            _openWindows.Remove(svm.SessionId);
            svm.FloatingWindow = false;
        };

        _openWindows[svm.SessionId] = window;
        window.Show();
    }

    public static void Close(ushort sessionId)
    {
        if (_openWindows.TryGetValue(sessionId, out var window))
            window.Close();
    }
}