using BenchmarkDotNet.Attributes;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Benchmarks;

/// <summary>
/// Measures in-place decryption of one standard MPEG transport stream packet.
/// </summary>
[MemoryDiagnoser]
public class PacketDecryptionBenchmarks
{
    private const int BatchSize = BitSlice.BitSliceBlock.MaxLaneCount;

    private readonly byte[] _packet = new byte[188];
    private readonly byte[] _source = new byte[188];
    private readonly byte[] _packetBatch = new byte[188 * BatchSize];
    private readonly byte[] _sourceBatch = new byte[188 * BatchSize];
    private readonly byte[] _alternatingPacketBatch = new byte[188 * BatchSize];
    private readonly byte[] _alternatingSourceBatch = new byte[188 * BatchSize];
    private readonly PacketDecryptionResult[] _batchResults = new PacketDecryptionResult[BatchSize];
    private readonly byte[] _streamA = new byte[CsaKeySchedule.StreamNibbleCount];
    private readonly byte[] _streamB = new byte[CsaKeySchedule.StreamNibbleCount];
    private readonly byte[] _blockSchedule = new byte[CsaKeySchedule.BlockScheduleLength];
    private readonly byte[] _blockInput = new byte[CsaBlockCipher.BlockSize];
    private readonly byte[] _blockOutput = new byte[CsaBlockCipher.BlockSize];
    private readonly byte[] _blockInputBatch = new byte[CsaBlockCipher.BlockSize * BitSlice.BitSliceBlock.MaxLaneCount];
    private readonly byte[] _blockOutputBatch = new byte[CsaBlockCipher.BlockSize * BitSlice.BitSliceBlock.MaxLaneCount];
    private readonly byte[] _blockStateBatch = new byte[64 * BitSlice.BitSliceBlock.MaxLaneCount];
    private readonly byte[] _bitslicedInitializationBlocks = new byte[CsaStreamCipher.BlockSize * BitSlice.BitSliceBlock.MaxLaneCount];
    private readonly byte[] _bitslicedOutput = new byte[CsaStreamCipher.BlockSize * 23 * BitSlice.BitSliceBlock.MaxLaneCount];
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
        for (int packetIndex = 0; packetIndex < BatchSize; packetIndex++)
        {
            _source.CopyTo(_sourceBatch, packetIndex * _source.Length);
            _source.CopyTo(_alternatingSourceBatch, packetIndex * _source.Length);
            _alternatingSourceBatch[(packetIndex * _source.Length) + 3] = (packetIndex & 1) == 0 ? (byte)0xD0 : (byte)0x90;
        }
        for (int lane = 0; lane < BitSlice.BitSliceBlock.MaxLaneCount; lane++)
        {
            _source.AsSpan(4, CsaStreamCipher.BlockSize).CopyTo(_bitslicedInitializationBlocks.AsSpan(lane * CsaStreamCipher.BlockSize));
            _source.AsSpan(4, CsaBlockCipher.BlockSize).CopyTo(_blockInputBatch.AsSpan(lane * CsaBlockCipher.BlockSize));
        }
        _source.AsSpan(4, CsaBlockCipher.BlockSize).CopyTo(_blockInput);

        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            throw new InvalidOperationException("Failed to create the benchmark decryptor.");
        }

        _decryptor = decryptor!;
        if (!CsaKeySchedule.TryCreateStreamNibbles(odd, _streamA, _streamB))
        {
            throw new InvalidOperationException("Failed to create the benchmark stream schedule.");
        }
        if (!CsaKeySchedule.TryCreateBlockSchedule(odd, _blockSchedule))
        {
            throw new InvalidOperationException("Failed to create the benchmark block schedule.");
        }
    }

    /// <summary>
    /// Copies a scrambled packet and decrypts it in place.
    /// </summary>
    /// <returns>The result produced by the decryptor.</returns>
    [Benchmark(Baseline = true)]
    public PacketDecryptionResult DecryptPacket()
    {
        _source.CopyTo(_packet, 0);
        return _decryptor.Decrypt(_packet);
    }

    /// <summary>
    /// Copies and decrypts a contiguous packet batch through the zero-allocation batch API.
    /// </summary>
    /// <returns><see langword="true"/> when every packet result was written.</returns>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public bool DecryptPacketBatch()
    {
        _sourceBatch.CopyTo(_packetBatch, 0);
        return _decryptor.TryDecryptPackets(_packetBatch, _batchResults);
    }

    /// <summary>
    /// Copies and decrypts a batch whose complete payload packets alternate between odd and even keys.
    /// </summary>
    /// <returns><see langword="true"/> when every packet result was written.</returns>
    [Benchmark(OperationsPerInvoke = BatchSize)]
    public bool DecryptAlternatingPacketBatch()
    {
        _alternatingSourceBatch.CopyTo(_alternatingPacketBatch, 0);
        return _decryptor.TryDecryptPackets(_alternatingPacketBatch, _batchResults);
    }

    /// <summary>
    /// Generates 23 stream blocks for 128 independent lanes with the bit-sliced stream kernel.
    /// </summary>
    /// <returns><see langword="true"/> when the output buffer was populated.</returns>
    [Benchmark(OperationsPerInvoke = BitSlice.BitSliceBlock.MaxLaneCount)]
    public bool GenerateBitslicedStream()
    {
        return CsaBitslicedStreamCipher.TryGenerateBlocks(
            _streamA,
            _streamB,
            _bitslicedInitializationBlocks,
            BitSlice.BitSliceBlock.MaxLaneCount,
            23,
            _bitslicedOutput);
    }

    /// <summary>
    /// Deciphers one 8-byte CSA block with a scheduled control word.
    /// </summary>
    [Benchmark]
    public void DecipherBlock()
    {
        CsaBlockCipher.DecipherBlock(_blockSchedule, _blockInput, _blockOutput);
    }

    /// <summary>
    /// Deciphers 128 independent 8-byte CSA blocks with the scalar block core.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BitSlice.BitSliceBlock.MaxLaneCount)]
    public void DecipherBlockBatch()
    {
        for (int lane = 0; lane < BitSlice.BitSliceBlock.MaxLaneCount; lane++)
        {
            CsaBlockCipher.DecipherBlock(
                _blockSchedule,
                _blockInputBatch.AsSpan(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize),
                _blockOutputBatch.AsSpan(lane * CsaBlockCipher.BlockSize, CsaBlockCipher.BlockSize));
        }
    }

    /// <summary>
    /// Deciphers 128 blocks with the stable interleaved batch core.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BitSlice.BitSliceBlock.MaxLaneCount)]
    public void DecipherBlocksInterleaved()
    {
        CsaBlockCipher.DecipherBlocks(
            _blockSchedule,
            _blockInputBatch,
            _blockOutputBatch,
            BitSlice.BitSliceBlock.MaxLaneCount,
            _blockStateBatch);
    }

    /// <summary>
    /// Deciphers 128 blocks with the column-major vectorized core.
    /// </summary>
    [Benchmark(OperationsPerInvoke = BitSlice.BitSliceBlock.MaxLaneCount)]
    public void DecipherBlocksColumnMajor()
    {
        CsaBlockCipher.DecipherBlocksColumnMajor(
            _blockSchedule,
            _blockInputBatch,
            _blockOutputBatch,
            BitSlice.BitSliceBlock.MaxLaneCount,
            _blockStateBatch);
    }
}
