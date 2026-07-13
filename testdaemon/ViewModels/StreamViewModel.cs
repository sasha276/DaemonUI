using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testdaemon.Models;
using testdaemon.Views;

namespace testdaemon.ViewModels;

public partial class StreamViewModel : ViewModelBase, IAsyncDisposable
{
    
    public ushort SessionId  { get; }
    public int    LocalPort  { get; }
    public ushort ServerPort { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private int  _frameCount;
    [ObservableProperty] private string _lastFrame = "";

    [ObservableProperty] private bool floatingWindow = false;

    partial void OnFloatingWindowChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            StreamWindowManager.Open(this);
        else
            StreamWindowManager.Close(SessionId);
    }

    public ObservableCollection<LogEntry> Frames { get; } = [];

    public string Title => $"session {SessionId}  ·  :{LocalPort}";

    public string StatusText => IsActive ? "● live" : "○ closed";

    private readonly UdpClient _socket;
    private readonly int _maxFrames;

    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    public StreamViewModel(ushort sessionId, int localPort, ushort serverPort,
                           UdpClient socket, int maxFrames = 2000)
    {
        SessionId  = sessionId;
        LocalPort  = localPort;
        ServerPort = serverPort;
        _socket    = socket;
        _maxFrames = maxFrames;
    }

    public void StartReceiving()
    {
        _ = StartReceivingAsync();
    }

    [RelayCommand]
    async Task StartReceivingAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (IsActive) return;

            _cts = new CancellationTokenSource();
            IsActive = true;
            _receiveTask = ReceiveLoopAsync(_cts.Token);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    [RelayCommand]
    async Task StopReceivingAsync()
    {
        await _stateLock.WaitAsync();
        Task? taskToAwait;
        try
        {
            if (!IsActive || _cts is null) return;

            _cts.Cancel();
            IsActive = false;
            taskToAwait = _receiveTask;
        }
        finally
        {
            _stateLock.Release();
        }

        if (taskToAwait is not null)
        {
            try { await taskToAwait; }
            catch { /* цикл сам гасит свои исключения, тут просто ждём завершения */ }
        }

        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
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
                    while (Frames.Count > _maxFrames)
                        Frames.RemoveAt(Frames.Count - 1);

                    FrameCount = Frames.Count;
                    LastFrame  = line;
                });
            }
        }
        catch (OperationCanceledException) { /* нормальное завершение по Stop */ }
        catch (ObjectDisposedException)    { /* сокет закрыт (Dispose всего стрима) */ }
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
        try { _cts?.Cancel(); } catch { /* ignore */ }
        if (_receiveTask is not null)
        {
            try { await _receiveTask; } catch { /* ignore */ }
        }
        try { _socket.Dispose(); } catch { /* ignore */ }
        _cts?.Dispose();
        _stateLock.Dispose();
    }
}