// UDPで音声を受信し、既定の再生デバイスで鳴らす（バックグラウンドスレッドで動作）
using System.Net;
using System.Net.Sockets;
using LanAudio.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LanAudio.Receiver;

public sealed class ReceiverEngine : IDisposable
{
    readonly UdpClient udp;
    readonly Thread thread;
    readonly int targetLatencyMs;     // ジッタ吸収バッファ
    readonly int maxLatencyMs;        // これを超えたら古い音を捨てる（急なまとめ届き対策）

    /// <summary>再生デバイス側のバッファ(ms)</summary>
    const int OutputLatencyMs = 20;
    /// <summary>この間隔ごとにバッファの最低水位を見て、溜まりすぎなら1パケット捨てる（クロック差対策）</summary>
    const int DriftWindowMs = 1000;

    long received, lost, dropped;
    double bufferedMs;
    string? source;

    public int Port { get; }
    public long Received => Interlocked.Read(ref received);
    public long Lost => Interlocked.Read(ref lost);
    public long Dropped => Interlocked.Read(ref dropped);
    public double BufferedMs => Volatile.Read(ref bufferedMs);
    /// <summary>受信中の送信元（"IP 48000Hz 2ch"）。受信していなければ null</summary>
    public string? Source => Volatile.Read(ref source);

    /// <summary>再生デバイスのエラー等で止まったとき（バックグラウンドスレッドから呼ばれる）</summary>
    public event Action<Exception>? Failed;

    public ReceiverEngine(int port, int targetLatencyMs)
    {
        Port = port;
        this.targetLatencyMs = targetLatencyMs;
        maxLatencyMs = targetLatencyMs + Math.Max(40, targetLatencyMs);
        udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
        udp.Client.ReceiveTimeout = 500;
        thread = new Thread(Run) { IsBackground = true, Name = "LanAudio receiver" };
        thread.Start();
    }

    public void Dispose()
    {
        udp.Close();
        thread.Join(2000);
    }

    void Run()
    {
        AudioThreadPriority.EnsureCurrentThread();
        WasapiOut? output = null;
        BufferedWaveProvider? buffer = null;
        int curRate = 0, curCh = 0;
        bool playing = false;
        bool haveSeq = false;
        uint lastSeq = 0;
        double windowMinMs = double.MaxValue;
        long windowStart = Environment.TickCount64;
        var remote = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            while (true)
            {
                byte[] data;
                try { data = udp.Receive(ref remote); }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // 送信が途絶えたら再生を止め、再開時は目標遅延までためてから鳴らす
                    if (playing) output!.Stop();
                    buffer?.ClearBuffer();
                    playing = false; haveSeq = false;
                    Volatile.Write(ref bufferedMs, 0);
                    Volatile.Write(ref source, null);
                    continue;
                }
                catch (SocketException) { break; }        // Close() で抜ける
                catch (ObjectDisposedException) { break; }

                if (!AudioPacket.TryRead(data, out int ch, out int rate, out uint seq, out var pcm))
                    continue;

                // フォーマットが変わったら出力を作り直す
                if (output == null || rate != curRate || ch != curCh)
                {
                    output?.Dispose();
                    buffer = new BufferedWaveProvider(new WaveFormat(rate, 16, ch))
                    {
                        BufferDuration = TimeSpan.FromSeconds(1),
                        DiscardOnBufferOverflow = true,
                        ReadFully = true,          // 足りないときは無音で埋める
                    };
                    output = new WasapiOut(AudioClientShareMode.Shared, true, OutputLatencyMs);
                    output.Init(new PrioritizedProvider(buffer));
                    curRate = rate; curCh = ch;
                    playing = false; haveSeq = false;
                }
                if (Source == null)
                    Volatile.Write(ref source, $"{remote.Address}  {rate}Hz {ch}ch");

                // 順序チェック（古い・重複パケットは捨てる。大きく戻ったら送信側の再起動とみなす）
                if (haveSeq)
                {
                    int diff = unchecked((int)(seq - lastSeq));
                    if (diff <= 0 && diff > -1000) continue;
                    if (diff > 1 && diff < 1000) Interlocked.Add(ref lost, diff - 1);
                }
                lastSeq = seq; haveSeq = true;
                Interlocked.Increment(ref received);

                double levelMs = buffer!.BufferedDuration.TotalMilliseconds;

                // 遅延が大きく溜まったら捨てる（ネットワークの詰まり後のまとめ届き等）
                if (levelMs > maxLatencyMs)
                {
                    Interlocked.Increment(ref dropped);
                    continue;
                }

                // 送信側と受信側のクロック差で少しずつ溜まる遅延を戻す:
                // 一定時間のあいだ一度も目標を下回らなかった（＝常に余っている）なら1パケット捨てる
                if (playing)
                {
                    windowMinMs = Math.Min(windowMinMs, levelMs);
                    long now = Environment.TickCount64;
                    if (now - windowStart >= DriftWindowMs)
                    {
                        bool excess = windowMinMs > targetLatencyMs;
                        windowMinMs = double.MaxValue;
                        windowStart = now;
                        if (excess)
                        {
                            Interlocked.Increment(ref dropped);
                            continue;
                        }
                    }
                }
                else
                {
                    windowMinMs = double.MaxValue;
                    windowStart = Environment.TickCount64;
                }
                buffer.AddSamples(pcm.ToArray(), 0, pcm.Length);
                Volatile.Write(ref bufferedMs, buffer.BufferedDuration.TotalMilliseconds);

                // 目標遅延分たまってから再生開始
                if (!playing && buffer.BufferedDuration.TotalMilliseconds >= targetLatencyMs)
                {
                    output!.Play();
                    playing = true;
                }
            }
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
        finally
        {
            output?.Dispose();
        }
    }
}

/// <summary>再生スレッド（NAudio 内部）で最初に読まれたときにそのスレッドを優先させるラッパー</summary>
sealed class PrioritizedProvider : IWaveProvider
{
    readonly IWaveProvider source;
    public PrioritizedProvider(IWaveProvider source) => this.source = source;
    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        AudioThreadPriority.EnsureCurrentThread();
        return source.Read(buffer, offset, count);
    }
}
