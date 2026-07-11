using BenchmarkDotNet.Attributes;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Benchmarks;

/// <summary>
/// Measures in-place decryption of one standard MPEG transport stream packet.
/// </summary>
[MemoryDiagnoser]
public class PacketDecryptionBenchmarks
{
    private readonly byte[] _packet = new byte[188];
    private readonly byte[] _source = new byte[188];
    private readonly Decryptor _decryptor;

    /// <summary>
    /// Initializes the benchmark packet and control words.
    /// </summary>
    public PacketDecryptionBenchmarks()
    {
        ReadOnlySpan<byte> even = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        ReadOnlySpan<byte> odd = [0x07, 0xE0, 0x1B, 0x02, 0xC9, 0xE0, 0x45, 0xEE];
        ReadOnlySpan<byte> encryptedPrefix =
        [
            0xDE, 0xCF, 0x0A, 0x0D, 0xB2, 0xD7, 0xC4, 0x40,
            0xDE, 0x5D, 0x63, 0x18, 0x5A, 0x98, 0x17, 0xAA,
            0xC9, 0xBC, 0x27, 0xC6, 0xCB, 0x49, 0x40, 0x48,
        ];

        _source[0] = 0x47;
        _source[3] = 0xD0;
        encryptedPrefix.CopyTo(_source.AsSpan(4));

        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            throw new InvalidOperationException("Failed to create the benchmark decryptor.");
        }

        _decryptor = decryptor!;
    }

    /// <summary>
    /// Copies a scrambled packet and decrypts it in place.
    /// </summary>
    /// <returns>The result produced by the decryptor.</returns>
    [Benchmark]
    public PacketDecryptionResult DecryptPacket()
    {
        _source.CopyTo(_packet, 0);
        return _decryptor.Decrypt(_packet);
    }
}
