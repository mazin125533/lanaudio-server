// デスクトップ側（サーバー）: UDPで音声を受信し、既定の再生デバイスで鳴らす
// 使い方: AudioReceiver.exe [ポート] [目標遅延ms]
using System.Net;
using System.Net.Sockets;
using LanAudio.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;

int port = args.Length >= 1 ? int.Parse(args[0]) : AudioPacket.DefaultPort;
int targetLatencyMs = args.Length >= 2 ? int.Parse(args[1]) : 60;   // ジッタ吸収バッファ
int maxLatencyMs = targetLatencyMs + 120;                            // これを超えたら古い音を捨てる

using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
Console.WriteLine($"待受中: UDP {port}  (目標遅延 {targetLatencyMs}ms)  Ctrl+C で終了");
foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
    if (ip.AddressFamily == AddressFamily.InterNetwork)
        Console.WriteLine($"  このPCのIP: {ip}");

Console.CancelKeyPress += (_, e) => { e.Cancel = true; udp.Close(); };

WasapiOut? output = null;
BufferedWaveProvider? buffer = null;
int curRate = 0, curCh = 0;
bool playing = false;
bool haveSeq = false;
uint lastSeq = 0;
long received = 0, lost = 0, dropped = 0;
var statusTimer = System.Diagnostics.Stopwatch.StartNew();
var remote = new IPEndPoint(IPAddress.Any, 0);

try
{
    while (true)
    {
        byte[] data;
        try { data = udp.Receive(ref remote); }
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
            output = new WasapiOut(AudioClientShareMode.Shared, true, 20);
            output.Init(buffer);
            curRate = rate; curCh = ch;
            playing = false; haveSeq = false;
            Console.WriteLine($"\n受信開始: {remote.Address}  {rate}Hz {ch}ch");
        }

        // 順序チェック（古い・重複パケットは捨てる）
        if (haveSeq)
        {
            int diff = unchecked((int)(seq - lastSeq));
            if (diff <= 0) continue;
            if (diff > 1) lost += diff - 1;
        }
        lastSeq = seq; haveSeq = true;
        received++;

        // 遅延が溜まりすぎたら捨てる（送信側と受信側のクロック差対策）
        if (buffer!.BufferedDuration.TotalMilliseconds > maxLatencyMs)
        {
            dropped++;
            continue;
        }
        buffer.AddSamples(pcm.ToArray(), 0, pcm.Length);

        // 目標遅延分たまってから再生開始
        if (!playing && buffer.BufferedDuration.TotalMilliseconds >= targetLatencyMs)
        {
            output!.Play();
            playing = true;
        }

        if (statusTimer.ElapsedMilliseconds >= 1000)
        {
            statusTimer.Restart();
            Console.Write($"\r受信:{received}  欠落:{lost}  破棄:{dropped}  バッファ:{buffer.BufferedDuration.TotalMilliseconds:0}ms   ");
        }
    }
}
finally
{
    output?.Dispose();
    Console.WriteLine("\n終了しました");
}
