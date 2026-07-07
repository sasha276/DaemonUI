using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using testdaemon.Models;

namespace testdaemon.ViewModels;

public partial class CanStreamViewModel:ObservableObject
{
    public ObservableCollection<StreamObject> Elements { get; set; } = [];
    
     public ushort SessionId  { get; }
    public int    LocalPort  { get; }
    public ushort ServerPort { get; }

    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private int  _frameCount;
    [ObservableProperty] private string _lastFrame = "";

    
    public ObservableCollection<LogEntry> Frames { get; } = [];

    
    public string Title => $"session {SessionId}  ·  :{LocalPort}";

    public string StatusText => IsActive ? "● live" : "○ closed";

    private readonly UdpClient _socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _maxFrames;

    public CanStreamViewModel(ushort sessionId, int localPort, ushort serverPort,
                           UdpClient socket, int maxFrames = 2000)
    {
        SessionId  = sessionId;
        LocalPort  = localPort;
        ServerPort = serverPort;
        _socket    = socket;
        _maxFrames = maxFrames;
    }

    /// <summary>Запускает фоновый приём фреймов. Вызывать сразу после создания.</summary>
    public void StartReceiving()
    {
        _ = ReceiveLoopAsync(_cts.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(ct);
                var line = Encoding.UTF8.GetString(result.Buffer).TrimEnd();
                var entry = new LogEntry(DateTime.Now, LogLevel.Info, line);

                Dispatcher.UIThread.Post(() =>
                {
                    Frames.Insert(0, entry);
                    // Ограничиваем размер, чтобы не разрасталась память.
                    while (Frames.Count > _maxFrames)
                        Frames.RemoveAt(Frames.Count - 1);

                    FrameCount = Frames.Count;
                    LastFrame  = line;
                });
            }
        }
        catch (OperationCanceledException) { /* нормальное завершение */ }
        catch (ObjectDisposedException)    { /* сокет закрыт */ }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
                Frames.Insert(0, new LogEntry(DateTime.Now, LogLevel.Err,
                    $"stream error: {ex.Message}")));
        }
    }

    public void ClearFrames()
    {
        Frames.Clear();
        FrameCount = 0;
        LastFrame  = "";
    }

    public async ValueTask DisposeAsync()
    {
        IsActive = false;
        try { _cts.Cancel(); } catch { /* ignore */ }
        try { _socket.Dispose(); } catch { /* ignore */ }
        _cts.Dispose();
        await Task.CompletedTask;
    }

}

public class StreamObject
{
    public string Desc { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;
}