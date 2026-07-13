using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace testdaemon.Service;

public enum Cmd : ushort
{
    Register         = 0x0001,
    Unregister       = 0x0002,
    Ping             = 0x0003,
    DeviceGetVersion    = 0x0004,
    DeviceCheckUart =0x0005,
    DeviceList       = 0x0010,
    DeviceInfo       = 0x0011,
    SessionCreate    = 0x0020,
    SessionClose     = 0x0021,
    SessionList      = 0x0022,
    GetVersion       = 0x0030,
    CheckUart        = 0x0031,
    StreamOpen       = 0x0040,
    StreamClose      = 0x0041,
    CanWrite         = 0x0042,
    CanPeriodicStart = 0x0043,
    CanPeriodicStop  = 0x0044,
    CanFrame         = 0x0050,
    UartConfigure    = 0x0060,
    UartWrite        = 0x0061,
    UartRead         = 0x0062,
    Error            = 0xFFFF,
}

/// Режимы работы UART-линии АППИ.
public enum UartMode : byte
{
    Rsdd     = 0x01,
    Rs232    = 0x02,
    Rs485    = 0x03,
    Disabled = 0x09,
}

// ── Data models 

public record DeviceEntry(int Index, ushort Vid, ushort Pid, byte Bus, string Port,
                          string Manufacturer = "", string Product = "")
{
    public override string ToString() =>
        $"#{Index}  {Vid:X4}:{Pid:X4}  bus={Bus} port={Port}"
        + (string.IsNullOrEmpty(Product) ? "" : $"  {Manufacturer} {Product}");
}

public record SessionEntry(ushort Id, ushort DeviceIndex, string DeviceKey)
{
    public override string ToString() => $"session {Id}  →  {DeviceKey}";
}

// ── Packet builder / parser 

public static class Packet
{
    public const int HeaderLen = 10;

    /// Build a raw packet
    public static byte[] Build(ushort clientId, ushort sessionId, ushort seq,
                                ushort cmd, byte[] payload)
    {
        var buf = new byte[HeaderLen + payload.Length];
        WU16(buf, 0, clientId);
        WU16(buf, 2, sessionId);
        WU16(buf, 4, seq);
        WU16(buf, 6, cmd);
        WU16(buf, 8, (ushort)payload.Length);
        Buffer.BlockCopy(payload, 0, buf, HeaderLen, payload.Length);
        return buf;
    }

    public static (ushort cid, ushort sid, ushort seq, ushort cmd, byte[] payload)
        Parse(byte[] buf)
    {
        if (buf.Length < HeaderLen) throw new Exception("Packet too short");
        var payLen  = RU16(buf, 8);
        var payload = new byte[payLen];
        if (buf.Length >= HeaderLen + payLen)
            Buffer.BlockCopy(buf, HeaderLen, payload, 0, payLen);
        return (RU16(buf, 0), RU16(buf, 2), RU16(buf, 4), RU16(buf, 6), payload);
    }

    public static bool IsResponse(ushort cmd) => (cmd & 0x8000) != 0;
    public static bool IsError(ushort cmd)    => cmd == 0xFFFF;

    public static ushort RU16(byte[] b, int o) => (ushort)((b[o] << 8) | b[o + 1]);
    public static uint   RU32(byte[] b, int o) =>
        ((uint)b[o] << 24) | ((uint)b[o+1] << 16) | ((uint)b[o+2] << 8) | b[o+3];

    private static void WU16(byte[] b, int o, ushort v)
    { b[o] = (byte)(v >> 8); b[o+1] = (byte)v; }

    // Helpers for building payloads
    public static byte[] From(ushort v) => [(byte)(v >> 8), (byte)v];
    public static byte[] From(uint v)   => [(byte)(v>>24),(byte)(v>>16),(byte)(v>>8),(byte)v];
}

// ── DaemonClient 

public class DaemonClient : IAsyncDisposable
{
    private readonly UdpClient  _udp;
    private readonly IPEndPoint _ep;
    private ushort _clientId;
    private ushort _seq;

    public bool   IsConnected => _clientId != 0;
    public ushort ClientId    => _clientId;

    public DaemonClient(string host, int port)
    {
        _ep  = new IPEndPoint(IPAddress.Parse(host), port);
        _udp = new UdpClient();
    }

    // ── core send/recv 

    private async Task<byte[]> TxRxAsync(ushort sessionId, ushort cmd, byte[] payload,
                                          int timeoutMs = 8000)
    {
        var seq = _seq++;
        var pkt = Packet.Build(_clientId, sessionId, seq, cmd, payload);

        await _udp.SendAsync(pkt, pkt.Length, _ep);

        using var cts = new CancellationTokenSource(timeoutMs);
        while (true)
        {
            UdpReceiveResult res;
            try   { res = await _udp.ReceiveAsync(cts.Token); }
            catch { throw new TimeoutException($"No response to cmd=0x{cmd:X4} (seq={seq})"); }

            var (_, _, rseq, rcmd, rpay) = Packet.Parse(res.Buffer);
            if (rseq != seq) continue;   // не наш пакет — ждём дальше

            if (Packet.IsError(rcmd))
                throw new Exception(Encoding.UTF8.GetString(rpay));

            return rpay;
        }
    }

    private Task<byte[]> TxRxAsync(ushort sessionId, Cmd cmd, byte[] payload, int timeoutMs = 3000)
        => TxRxAsync(sessionId, (ushort)cmd, payload, timeoutMs);

    // ── public API 

    public async Task RegisterAsync()
    {
        var pay  = await TxRxAsync(0, Cmd.Register, []);
        _clientId = Packet.RU16(pay, 0);
    }

    public async Task UnregisterAsync()
    {
        await TxRxAsync(0, Cmd.Unregister, []);
        _clientId = 0;
    }

    public async Task<string> PingAsync()
        => Encoding.UTF8.GetString(await TxRxAsync(0, Cmd.Ping, []));

    // Devices
    public async Task<List<DeviceEntry>> ListDevicesAsync()
    {
        var p     = await TxRxAsync(0, Cmd.DeviceList, []);
        var count = Packet.RU16(p, 0);
        var list  = new List<DeviceEntry>();
        int off   = 2;
        for (int i = 0; i < count; i++)
        {
            var idx  = Packet.RU16(p, off); off += 2;
            var vid  = Packet.RU16(p, off); off += 2;
            var pid  = Packet.RU16(p, off); off += 2;
            var bus  = p[off++];
            var plen = p[off++];
            var port = Encoding.UTF8.GetString(p, off, plen); off += plen;
            var mlen = p[off++];
            var mfr  = Encoding.UTF8.GetString(p, off, mlen); off += mlen;
            var dlen = p[off++];
            var prod = Encoding.UTF8.GetString(p, off, dlen); off += dlen;
            list.Add(new DeviceEntry(idx, vid, pid, bus, port, mfr, prod));
        }
        return list;
    }

    public async Task<(ushort vid, ushort pid, byte bus, string port,
                        string manufacturer, string product)>
        GetDeviceInfoAsync(ushort idx)
    {
        var p   = await TxRxAsync(0, Cmd.DeviceInfo, Packet.From(idx));
        int off = 0;
        var vid  = Packet.RU16(p, off); off += 2;
        var pid  = Packet.RU16(p, off); off += 2;
        var bus  = p[off++];
        var portLen = p[off++];
        var port = Encoding.UTF8.GetString(p, off, portLen); off += portLen;
        var mfrLen = p[off++];
        var mfr  = Encoding.UTF8.GetString(p, off, mfrLen); off += mfrLen;
        var prodLen = p[off++];
        var prod = Encoding.UTF8.GetString(p, off, prodLen);
        return (vid, pid, bus, port, mfr, prod);
    }

    // Sessions
    public async Task<ushort> CreateSessionAsync(ushort deviceIndex)
        => Packet.RU16(await TxRxAsync(0, Cmd.SessionCreate, Packet.From(deviceIndex)), 0);

    public async Task CloseSessionAsync(ushort sessionId)
        => await TxRxAsync(sessionId, Cmd.SessionClose, Packet.From(sessionId));

    public async Task<List<SessionEntry>> ListSessionsAsync()
    {
        var p     = await TxRxAsync(0, Cmd.SessionList, []);
        var count = Packet.RU16(p, 0);
        var list  = new List<SessionEntry>();
        int off   = 2;
        for (int i = 0; i < count; i++)
        {
            var id     = Packet.RU16(p, off); off += 2;
            var devIdx = Packet.RU16(p, off); off += 2;
            var klen   = p[off++];
            var key    = Encoding.UTF8.GetString(p, off, klen); off += klen;
            list.Add(new SessionEntry(id, devIdx, key));
        }
        return list;
    }

    // Device commands
    public async Task<string> GetVersionAsync(ushort sessionId)
        => Encoding.UTF8.GetString(await TxRxAsync(sessionId, Cmd.GetVersion, []));
    
    public async Task<string> GetVersionDeviceAsync(ushort deviceIndex)
        => Encoding.UTF8.GetString(await TxRxAsync(0, Cmd.DeviceGetVersion, Packet.From(deviceIndex)));

    public async Task<bool> CheckUartAsync(ushort sessionId)
    {
        var p = await TxRxAsync(sessionId, Cmd.CheckUart, []);
        return p.Length > 0 && p[0] != 0;
    }

    public async Task<bool> CheckUartDeviceAsync(ushort deviceIndex)
    {
        var p = await TxRxAsync(0, Cmd.DeviceCheckUart, Packet.From(deviceIndex));
        return p.Length > 0 && p[0] != 0;
    }

    // Stream
    public async Task<ushort> OpenStreamAsync(ushort sessionId, ushort dataPort, byte canIface = 1)
    {
        var pay = new byte[4];
        Packet.From(dataPort).CopyTo(pay, 0);
        pay[2] = 0;
        pay[3] = canIface;  // 1 = CAN1, 2 = CAN2
        return Packet.RU16(await TxRxAsync(sessionId, Cmd.StreamOpen, pay), 0);
    }

    public async Task CloseStreamAsync(ushort sessionId)
        => await TxRxAsync(sessionId, Cmd.StreamClose, []);

    // CAN
    // payload: [iface:u8][id:u16 BE][data: 0..=8 байт]
    public async Task CanWriteAsync(ushort sessionId, byte canIface, ushort canId, byte[] data)
    {
        var pay = new byte[3 + data.Length];
        pay[0] = canIface;                 // 1=CAN1 2=CAN2 3=CAN3 4=CAN4 5=CANTech
        Packet.From(canId).CopyTo(pay, 1); // u16 big-endian
        data.CopyTo(pay, 3);
        await TxRxAsync(sessionId, Cmd.CanWrite, pay);
    }

    // payload: [interval:u16 BE][iface:u8][id:u16 BE][data: 0..=8 байт]
    public async Task CanPeriodicStartAsync(ushort sessionId, ushort intervalMs,
                                             byte canIface, ushort canId, byte[] data)
    {
        var pay = new byte[5 + data.Length];
        Packet.From(intervalMs).CopyTo(pay, 0); // u16 big-endian
        pay[2] = canIface;                      // 1=CAN1 2=CAN2 3=CAN3 4=CAN4 5=CANTech
        Packet.From(canId).CopyTo(pay, 3);      // u16 big-endian
        data.CopyTo(pay, 5);
        await TxRxAsync(sessionId, Cmd.CanPeriodicStart, pay);
    }

    public async Task CanPeriodicStopAsync(ushort sessionId)
        => await TxRxAsync(sessionId, Cmd.CanPeriodicStop, []);

    //  UART 
    // payload: [mode:u8]. Демон сам формирует USB-пакет [0x01,0x01,mode,0x00].
    public async Task UartConfigureAsync(ushort sessionId, UartMode mode)
        => await TxRxAsync(sessionId, Cmd.UartConfigure, [(byte)mode]);

    // payload: сырые байты данных. Демон сам оборачивает их в [0x01,0x02,0x00,...,len_hi,len_lo,data].
    public async Task UartWriteAsync(ushort sessionId, byte[] data)
        => await TxRxAsync(sessionId, Cmd.UartWrite, data);

    // Возвращает принятые из UART байты. ВНИМАНИЕ: в текущей версии демона
    // для appi2 функция uart_read — заглушка и всегда возвращает пустой массив.
    public async Task<byte[]> UartReadAsync(ushort sessionId)
        => await TxRxAsync(sessionId, Cmd.UartRead, []);

    //  Raw 
    // Отправляет произвольную команду напрямую — для тестирования протокола,
    // пока для конкретной команды нет отдельного метода (например, у Phlox
    // сейчас реализован только Master-хендшейк, всё остальное — через Raw).
    public Task<byte[]> SendRawAsync(ushort sessionId, ushort cmd, byte[] payload)
        => TxRxAsync(sessionId, cmd, payload);

    public async ValueTask DisposeAsync()
    {
        if (IsConnected)
            try { await UnregisterAsync(); } catch { /* ignore */ }
        _udp.Dispose();
    }
}
