using System.Net.Sockets;
using System.Runtime.CompilerServices;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using OPods.Pods;

namespace OPods.Bluetooth;

/// <summary>
/// Windows RFCOMM client for OPPO/HeyMelody earphones, built on 32feet.NET.
/// Replaces the Kotlin <c>OppoRfcommSocketFactory</c>.
/// </summary>
public sealed class OppoRfcommClient : IDisposable
{
    private const int RfcommChannel = 15;

    private static readonly Guid Uuid1 = Guid.Parse("00001107-D102-11E1-9B23-00025B00A5A5");
    private static readonly Guid Uuid2 = Guid.Parse("0000079A-D102-11E1-9B23-00025B00A5A5");

    private BluetoothClient? _client;
    private NetworkStream? _stream;
    private readonly OppoPacketFramer _framer = new();

    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// Connect to the earphone using the given method. UUID mode tries the two
    /// preferred UUIDs via SDP; CHANNEL mode connects directly to SCN 15.
    /// </summary>
    public async Task ConnectAsync(BluetoothAddress address, RfcommConnectionMethod method, CancellationToken ct)
    {
        var failures = new List<Exception>();

        if (method == RfcommConnectionMethod.Uuid)
        {
            foreach (var uuid in new[] { Uuid1, Uuid2 })
            {
                ct.ThrowIfCancellationRequested();
                var client = new BluetoothClient();
                try
                {
                    await ConnectWithClientAsync(client, c => c.Connect(address, uuid), $"UUID {uuid}", ct).ConfigureAwait(false);
                    _client = client;
                    _stream = client.GetStream();
                    _framer.Reset();
                    return;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    failures.Add(e);
                    TryClose(client);
                }
            }
        }
        else
        {
            ct.ThrowIfCancellationRequested();
            var endpoint = new BluetoothEndPoint(address, BluetoothService.SerialPort, RfcommChannel);
            var client = new BluetoothClient();
            try
            {
                await ConnectWithClientAsync(client, c => c.Connect(endpoint), $"channel {RfcommChannel}", ct).ConfigureAwait(false);
                _client = client;
                _stream = client.GetStream();
                _framer.Reset();
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                failures.Add(e);
                TryClose(client);
            }
        }

        throw CreateException("Unable to connect OPPO RFCOMM socket via " + method, failures);
    }

    private static async Task ConnectWithClientAsync(BluetoothClient client, Action<BluetoothClient> connectAction, string label, CancellationToken ct)
    {
        var connectTask = Task.Run(() =>
        {
            try { connectAction(client); }
            catch (SocketException se) { throw new IOException($"RFCOMM connect failed via {label}", se); }
        }, CancellationToken.None);

        using var reg = ct.Register(() => { });
        var winner = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);

        if (winner != connectTask)
        {
            TryClose(client);
            try { await connectTask.ConfigureAwait(false); } catch { }
            ct.ThrowIfCancellationRequested();
        }

        await connectTask.ConfigureAwait(false);
    }

    /// <summary>Send a raw packet to the earphone.</summary>
    public async Task SendAsync(byte[] packet, CancellationToken ct)
    {
        var stream = _stream;
        if (stream is null) throw new InvalidOperationException("Not connected.");
        await stream.WriteAsync(packet.AsMemory(0, packet.Length), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Continuously read from the stream and yield complete OPPO frames.
    /// Completes when the stream is closed or the token is cancelled.
    /// </summary>
    public async IAsyncEnumerable<byte[]> ReadFramesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var stream = _stream;
        if (stream is null) throw new InvalidOperationException("Not connected.");

        var buffer = new byte[1024];
        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                yield break;
            }
            if (read <= 0) yield break;

            foreach (var frame in _framer.Append(buffer, read))
            {
                yield return frame;
            }
        }
    }

    public void Disconnect()
    {
        TryClose(_client);
        _stream = null;
        _client = null;
        _framer.Reset();
    }

    private static void TryClose(BluetoothClient? client)
    {
        if (client is null) return;
        try { client.Close(); } catch { }
    }

    private static IOException CreateException(string message, List<Exception> failures)
    {
        var cause = failures.Count > 0 ? failures[^1] : null;
        var ex = new IOException(message, cause);
        for (int i = 0; i < failures.Count - 1; i++) ex.Data[$"suppressed_{i}"] = failures[i].Message;
        return ex;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
