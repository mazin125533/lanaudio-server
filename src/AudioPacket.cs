using System;
using System.Buffers.Binary;

namespace LanAudio.Shared;

/// <summary>
/// UDPで送る1パケットの形式
///  0-1 : マジック "LA"
///  2   : バージョン
///  3   : チャンネル数 (1 or 2)
///  4-7 : サンプルレート (Hz, little endian)
///  8-11: シーケンス番号 (little endian)
///  12- : PCM 16bit little endian (インターリーブ)
/// </summary>
public static class AudioPacket
{
    public const int HeaderSize = 12;
    public const byte Version = 1;
    public const int DefaultPort = 50005;
    /// <summary>IP断片化を避けるためのペイロード上限(バイト)</summary>
    public const int MaxPayload = 1200;

    public static int Write(Span<byte> dst, int channels, int sampleRate, uint seq, ReadOnlySpan<byte> pcm16)
    {
        dst[0] = (byte)'L';
        dst[1] = (byte)'A';
        dst[2] = Version;
        dst[3] = (byte)channels;
        BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(4, 4), sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(dst.Slice(8, 4), seq);
        pcm16.CopyTo(dst.Slice(HeaderSize));
        return HeaderSize + pcm16.Length;
    }

    public static bool TryRead(ReadOnlySpan<byte> src, out int channels, out int sampleRate, out uint seq, out ReadOnlySpan<byte> pcm16)
    {
        channels = 0; sampleRate = 0; seq = 0; pcm16 = default;
        if (src.Length < HeaderSize) return false;
        if (src[0] != 'L' || src[1] != 'A' || src[2] != Version) return false;
        channels = src[3];
        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(src.Slice(4, 4));
        seq = BinaryPrimitives.ReadUInt32LittleEndian(src.Slice(8, 4));
        if (channels is < 1 or > 2 || sampleRate is < 8000 or > 192000) return false;
        pcm16 = src.Slice(HeaderSize);
        return pcm16.Length % (channels * 2) == 0;
    }
}
