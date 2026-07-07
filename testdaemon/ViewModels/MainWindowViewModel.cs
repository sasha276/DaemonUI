using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testdaemon.Models;
using testdaemon.Service;
using testdaemon.Views;

namespace testdaemon.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] string  _host      = "127.0.0.1";
    [ObservableProperty] string  _port      = "1001";
    [ObservableProperty] bool    _connected;
    [ObservableProperty] string  _connLabel = "● Disconnected";

    [ObservableProperty] ObservableCollection<DeviceEntry>  _devices  = [];
    [ObservableProperty] DeviceEntry?                        _selectedDevice;

    [ObservableProperty] ObservableCollection<SessionEntry> _sessions = [];
    [ObservableProperty] SessionEntry?                       _selectedSession;

    [ObservableProperty] string _canId       = "001";
    [ObservableProperty] string _canData     = "DE AD BE EF 00 00 00 00";
    [ObservableProperty] string _canInterval = "100";
    [ObservableProperty] bool   _isPeriodic;

    [ObservableProperty] int    _canIfaceIndex;


    [ObservableProperty] int    _uartModeIndex   = 1;

    [ObservableProperty] string _uartData        = "48 65 6C 6C 6F";
    [ObservableProperty] bool   _uartDataAsAscii;
    [ObservableProperty] string _uartStatus      = "";

    [ObservableProperty] string _rawCmd     = "0003";
    [ObservableProperty] string _rawPayload = "";
    [ObservableProperty] string _rawSession = "0";

    [ObservableProperty] ObservableCollection<LogEntry> _logs = [];

    [ObservableProperty] ObservableCollection<StreamViewModel> _streams = [];
    [ObservableProperty] StreamViewModel? _selectedStream;

    [ObservableProperty] private CommandsService _commands = new();

    public MainWindowViewModel()
    {
        try
        {
            var json = FileService.LoadCommandsJson();
            Commands.Load(json);
        }
        catch (Exception ex) { Log($"LoadCommands: {ex.Message}", LogLevel.Err); }
    }

    [RelayCommand]
    async Task SendPresetCommandAsync(CommandBody? cmd)
    {
        if (cmd == null) return;
        if (!CheckSession()) return;
        try
        {
            var canId = Convert.ToUInt16(cmd.Id.Replace("0x", "").Trim(), 16);
            var iface = (byte)(CanIfaceIndex + 1);
            var data  = ParseHexBytes(cmd.Body);

            if (data.Length > 8)
            {
                Log($"Preset '{cmd.Name}': {data.Length} bytes, max 8", LogLevel.Warn);
                return;
            }

            await _client!.CanWriteAsync(SelectedSession!.Id, iface, canId, data);
            Log($"CanWrite preset '{cmd.Name}'  id=0x{canId:X3}  data=[{cmd.Body}]", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    private const int BaseDataPort = 15001;

    private DaemonClient? _client;

    private static readonly string[] CanIfaceNames = ["CAN1", "CAN2", "CAN3", "CAN4", "CANTech"];

   
    
    
    [RelayCommand]
    async Task ConnectAsync()
    {
        try
        {
            if (_client != null) await _client.DisposeAsync();
            if (!int.TryParse(Port, out var p)) { Log("bad port", LogLevel.Warn); return; }
            _client = new DaemonClient(Host, p);
            await _client.RegisterAsync();
            Connected  = true;
            ConnLabel  = $"● Connected  client_id={_client.ClientId}";
            Log($"Registered  client_id={_client.ClientId}", LogLevel.Ok);
            await DoDeviceListAsync();
        }
        catch (Exception ex) { Log($"Connect: {ex.Message}", LogLevel.Err); }
    }

    [RelayCommand]
    async Task DisconnectAsync()
    {
        try
        {
            await CloseAllStreamsAsync();
            if (_client != null) { await _client.DisposeAsync(); _client = null; }
            Connected = false;
            ConnLabel = "● Disconnected";
            Devices.Clear();
            Sessions.Clear();
            Log("Disconnected", LogLevel.Info);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task PingAsync()
    {
        if (!Check()) return;
        try { Log($"Ping → {await _client!.PingAsync()}", LogLevel.Ok); }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task DeviceListAsync() => await DoDeviceListAsync();

    private async Task DoDeviceListAsync()
    {
        if (!Check()) return;
        try
        {
            var list = await _client!.ListDevicesAsync();
            Devices.Clear();
            foreach (var d in list) Devices.Add(d);
            Log($"DeviceList → {list.Count} device(s)", list.Count > 0 ? LogLevel.Ok : LogLevel.Warn);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task DeviceInfoAsync()
    {
        if (!Check() || SelectedDevice == null) { Log("Select a device", LogLevel.Warn); return; }
        try
        {
            var (vid, pid, bus, port, mfr, prod) =
                await _client!.GetDeviceInfoAsync((ushort)SelectedDevice.Index);
            Log($"DeviceInfo  {vid:X4}:{pid:X4}  bus={bus} port={port}  \"{mfr}\" / \"{prod}\"",
                LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task SessionCreateAsync()
    {
        if (!Check() || SelectedDevice == null) { Log("Select a device first", LogLevel.Warn); return; }
        try
        {
            var sid = await _client!.CreateSessionAsync((ushort)SelectedDevice.Index);
            Log($"SessionCreate → session_id={sid}", LogLevel.Ok);
            await DoSessionListAsync();
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task SessionListAsync() => await DoSessionListAsync();

    private async Task DoSessionListAsync()
    {
        if (!Check()) return;
        try
        {
            var list = await _client!.ListSessionsAsync();
            Sessions.Clear();
            foreach (var s in list) Sessions.Add(s);
            Log($"SessionList → {list.Count} session(s)", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task SessionCloseAsync()
    {
        if (!Check() || SelectedSession == null) { Log("Select a session", LogLevel.Warn); return; }
        try
        {
            await _client!.CloseSessionAsync(SelectedSession.Id);
            Log($"SessionClose  id={SelectedSession.Id}", LogLevel.Ok);
            await DoSessionListAsync();
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }


    [RelayCommand]
    async Task GetVersionAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var v = await _client!.GetVersionAsync(SelectedSession!.Id);
            Log($"GetVersion → \"{v}\"", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task CheckUartAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var has = await _client!.CheckUartAsync(SelectedSession!.Id);
            Log($"CheckUart → {(has ? "present" : "not present")}", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }


    [RelayCommand]
    async Task CanSendAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var canId  = Convert.ToUInt16(CanId.Replace("0x","").Trim(), 16);
            var iface  = (byte)(CanIfaceIndex + 1);
            var data   = ParseHexBytes(CanData);

            if (data.Length > 8)
            {
                Log($"CAN data is {data.Length} bytes, max 8", LogLevel.Warn);
                return;
            }

            var ifaceName = iface >= 1 && iface <= CanIfaceNames.Length
                ? CanIfaceNames[iface - 1]
                : $"iface{iface}";

            if (IsPeriodic)
            {
                if (!ushort.TryParse(CanInterval, out var ms))
                    { Log("Bad interval", LogLevel.Warn); return; }
                await _client!.CanPeriodicStartAsync(SelectedSession!.Id, ms, iface, canId, data);
                Log($"CanPeriodicStart  {ifaceName}  id=0x{canId:X3}  interval={ms}ms  " +
                    $"data=[{CanData.Trim()}]", LogLevel.Ok);
            }
            else
            {
                await _client!.CanWriteAsync(SelectedSession!.Id, iface, canId, data);
                Log($"CanWrite  {ifaceName}  id=0x{canId:X3}  data=[{CanData.Trim()}]", LogLevel.Ok);
            }
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task CanPeriodicStopAsync()
    {
        if (!CheckSession()) return;
        try
        {
            await _client!.CanPeriodicStopAsync(SelectedSession!.Id);
            Log("CanPeriodicStop", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    static UartMode UartModeFromIndex(int i) => i switch
    {
        0 => UartMode.Rsdd,
        1 => UartMode.Rs232,
        2 => UartMode.Rs485,
        _ => UartMode.Disabled,
    };

    [RelayCommand]
    async Task UartConfigureAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var mode = UartModeFromIndex(UartModeIndex);
            await _client!.UartConfigureAsync(SelectedSession!.Id, mode);
            Log($"UartConfigure  mode={mode} (0x{(byte)mode:X2})", LogLevel.Ok);
            UartStatus = $"configured: {mode}";
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task UartWriteAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var bytes = UartDataAsAscii
                ? Encoding.UTF8.GetBytes(UartData)
                : ParseHexBytes(UartData);

            if (bytes.Length == 0) { Log("UART: nothing to send", LogLevel.Warn); return; }

            await _client!.UartWriteAsync(SelectedSession!.Id, bytes);

            var preview = UartDataAsAscii
                ? $"\"{UartData}\""
                : $"[{string.Join(' ', bytes.Select(b => b.ToString("X2")))}]";
            Log($"UartWrite  {bytes.Length} byte(s)  {preview}", LogLevel.Ok);
            UartStatus = $"sent {bytes.Length} byte(s)";
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand]
    async Task UartReadAsync()
    {
        if (!CheckSession()) return;
        try
        {
            var data = await _client!.UartReadAsync(SelectedSession!.Id);
            if (data.Length == 0)
            {
                Log("UartRead: 0 bytes (note: daemon's uart_read for appi2 is a stub)", LogLevel.Warn);
                UartStatus = "read: 0 bytes";
                return;
            }
            var hex   = string.Join(' ', data.Select(b => b.ToString("X2")));
            var ascii = string.Concat(data.Select(b => b >= 0x20 && b < 0x7F ? (char)b : '.'));
            Log($"UartRead  {data.Length} byte(s)  [{hex}]  \"{ascii}\"", LogLevel.Ok);
            UartStatus = $"read {data.Length} byte(s)";
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }


    [RelayCommand]
    async Task SendRawAsync()
    {
        if (!Check()) return;
        try
        {
            if (!ushort.TryParse(RawCmd.Replace("0x",""), System.Globalization.NumberStyles.HexNumber,
                                  null, out var cmd))
                { Log("Bad cmd hex", LogLevel.Warn); return; }

            ushort.TryParse(RawSession, out var sessId);

            var payload = RawPayload.Trim().Length > 0 ? ParseHexBytes(RawPayload) : [];

            Log($"Raw send  cmd=0x{cmd:X4}  session={sessId}  payload=[{RawPayload.Trim()}]",
                LogLevel.Info);

            var resp = await _client!.SendRawAsync(sessId, cmd, payload);
            var hex  = string.Join(' ', resp.Select(b => b.ToString("X2")));
            Log($"Raw recv  {resp.Length} byte(s)  [{hex}]", LogLevel.Ok);
        }
        catch (Exception ex) { Log(ex.Message, LogLevel.Err); }
    }

    [RelayCommand] void ClearLog() => Logs.Clear();

    private void Log(string msg, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry(DateTime.Now, level, msg);
        Dispatcher.UIThread.Post(() => Logs.Insert(0, entry));
    }


    private bool Check()
    {
        if (_client == null || !Connected) { Log("Not connected", LogLevel.Warn); return false; }
        return true;
    }

    private bool CheckSession()
    {
        if (!Check()) return false;
        if (SelectedSession == null) { Log("Select a session first", LogLevel.Warn); return false; }
        return true;
    }

    private static byte[] ParseHexBytes(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new byte[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = Convert.ToByte(parts[i].Replace("0x",""), 16);
        return result;
    }

    [RelayCommand]
    async Task StreamOpenAsync()
    {
        if (!CheckSession()) return;

        var sessionId = SelectedSession!.Id;

        foreach (var existing in Streams)
        {
            if (existing.SessionId == sessionId && existing.IsActive)
            {
                Log($"Stream for session {sessionId} is already open", LogLevel.Warn);
                SelectedStream = existing;
                return;
            }
        }

        UdpClient? socket = null;
        try
        {
            var (sock, localPort) = OpenFreeUdpSocket();
            socket = sock;

            var serverPort = await _client!.OpenStreamAsync(sessionId, (ushort)localPort);

            var stream = new StreamViewModel(sessionId, localPort, serverPort, socket);
            socket = null;
            stream.StartReceiving();

            Streams.Add(stream);
            SelectedStream = stream;

            Log($"StreamOpen  session={sessionId}  server_port={serverPort}  " +
                $"listening on :{localPort}", LogLevel.Ok);
        }
        catch (Exception ex)
        {
            socket?.Dispose();
            Log($"StreamOpen: {ex.Message}", LogLevel.Err);
        }
    }

    [RelayCommand]
    async Task OpenStreamWindowAsync()
    {
        var window = new CanStreamView();
        window.Show();
    }

    [RelayCommand]
    async Task StreamCloseAsync()
    {
        if (!Check()) return;

        var stream = SelectedStream
                     ?? FindStreamForSession(SelectedSession?.Id);

        if (stream == null) { Log("No stream selected to close", LogLevel.Warn); return; }

        await CloseStreamInternalAsync(stream);
    }

    [RelayCommand]
    async Task CloseStreamAsync(StreamViewModel? stream)
    {
        if (stream == null) return;
        await CloseStreamInternalAsync(stream);
    }

    private async Task CloseStreamInternalAsync(StreamViewModel stream)
    {
        try
        {
            if (_client != null)
                await _client.CloseStreamAsync(stream.SessionId);
        }
        catch (Exception ex) { Log($"StreamClose: {ex.Message}", LogLevel.Err); }

        await stream.DisposeAsync();
        Streams.Remove(stream);

        if (ReferenceEquals(SelectedStream, stream))
            SelectedStream = Streams.Count > 0 ? Streams[0] : null;

        Log($"StreamClose  session={stream.SessionId}  (was :{stream.LocalPort})", LogLevel.Ok);
    }

    [RelayCommand]
    void ClearStreamFrames() => SelectedStream?.ClearFrames();

    private StreamViewModel? FindStreamForSession(ushort? sessionId)
    {
        if (sessionId == null) return null;
        foreach (var s in Streams)
            if (s.SessionId == sessionId) return s;
        return null;
    }

    private (UdpClient socket, int port) OpenFreeUdpSocket()
    {
        var used = new System.Collections.Generic.HashSet<int>();
        foreach (var s in Streams) used.Add(s.LocalPort);

        for (int port = BaseDataPort; port < BaseDataPort + 1000; port++)
        {
            if (used.Contains(port)) continue;
            try { return (new UdpClient(port), port); }
            catch (SocketException) {}
        }
        throw new Exception("No free UDP port available for a new stream");
    }

    private async Task CloseAllStreamsAsync()
    {
        foreach (var s in Streams.ToArray())
        {
            try { if (_client != null) await _client.CloseStreamAsync(s.SessionId); }
            catch { }
            await s.DisposeAsync();
        }
        Streams.Clear();
        SelectedStream = null;
    }

    [RelayCommand]
    public void Test()
    {
        var testwindow = new CanStreamView();

        testwindow.Show();
    }
}
