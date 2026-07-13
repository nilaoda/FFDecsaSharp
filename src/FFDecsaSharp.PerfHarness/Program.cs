using System.Diagnostics;
using System.Globalization;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using FFDecsaSharp.CSA;

namespace FFDecsaSharp.PerfHarness;

internal static class Program
{
    private const int BatchSize = 128;
    private const int PacketSize = 188;
    private const int PayloadSize = 184;
    private const int WarmupBatches = 5_000;
    private const int MeasurementBatches = 30_000;
    private const int ProbeSampleCount = 7;
    private const int ProbeWarmupIterations = 250;
    private const int ProbeMeasurementIterations = 5_000;
    private const ulong ExpectedOutputHash = 0x76DC3CFC07B7D0F2UL;

    private static int Main(string[] args)
    {
        if (args.AsSpan().Contains("--parallel-probe", StringComparer.Ordinal))
        {
            return RunParallelProbe();
        }

        return args.AsSpan().Contains("--probe", StringComparer.Ordinal)
            ? RunProbe()
            : RunProtocol();
    }

    private static int RunProtocol()
    {
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            return 1;
        }

        Decryptor initializedDecryptor = decryptor!;

        byte[] source = new byte[PacketSize * BatchSize];
        byte[] packets = new byte[source.Length];
        PacketDecryptionResult[] results = new PacketDecryptionResult[BatchSize];
        CreateSourcePackets(source);

        if (!DecryptOnce(initializedDecryptor, source, packets, results))
        {
            return 2;
        }

        if (ComputeFnv1a64(packets) != ExpectedOutputHash)
        {
            return 5;
        }
        for (int iteration = 0; iteration < WarmupBatches; iteration++)
        {
            if (!DecryptOnce(initializedDecryptor, source, packets, results))
            {
                return 3;
            }
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long elapsedTicks = 0;
        for (int iteration = 0; iteration < MeasurementBatches; iteration++)
        {
            source.CopyTo(packets, 0);
            long start = Stopwatch.GetTimestamp();
            bool decrypted = initializedDecryptor.TryDecryptPackets(packets, results);
            elapsedTicks += Stopwatch.GetTimestamp() - start;
            if (!decrypted)
            {
                return 4;
            }
        }

        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        ulong actualHash = ComputeFnv1a64(packets);
        if (actualHash != ExpectedOutputHash)
        {
            return 5;
        }

        double elapsedNanoseconds = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency);
        double packetsProcessed = MeasurementBatches * (double)BatchSize;
        double nanosecondsPerPacket = elapsedNanoseconds / packetsProcessed;
        double packetsPerSecond = 1_000_000_000d / nanosecondsPerPacket;
        double megabitsPerSecond = (packetsPerSecond * PayloadSize * 8d) / 1_000_000d;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":\"ffdecsa-compare-v1\",\"implementation\":\"csharp\",\"runtime\":\"{Environment.Version}\",\"architecture\":\"{RuntimeInformation.ProcessArchitecture}\",\"parallelism\":{BatchSize},\"batch_packets\":{BatchSize},\"warmup_batches\":{WarmupBatches},\"measurement_batches\":{MeasurementBatches},\"timed_scope\":\"decrypt_only\",\"copy_in_timed_scope\":false,\"payload_bytes_per_packet\":{PayloadSize},\"packets_processed\":{packetsProcessed:F0},\"elapsed_ns\":{elapsedNanoseconds:F0},\"nanoseconds_per_packet\":{nanosecondsPerPacket:F3},\"packets_per_second\":{packetsPerSecond:F3},\"megabits_per_second\":{megabitsPerSecond:F3},\"managed_allocated_bytes\":{allocatedBytes},\"output_fnv1a64\":\"{actualHash:X16}\",\"verified\":true}}"));
        return 0;
    }

    private static int RunProbe()
    {
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        byte[] streamA = new byte[CsaKeySchedule.StreamNibbleCount];
        byte[] streamB = new byte[CsaKeySchedule.StreamNibbleCount];
        byte[] blockSchedule = new byte[CsaKeySchedule.BlockScheduleLength];
        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor)
            || !CsaKeySchedule.TryCreateStreamNibbles(even, streamA, streamB)
            || !CsaKeySchedule.TryCreateBlockSchedule(even, blockSchedule))
        {
            return 1;
        }

        Decryptor initializedDecryptor = decryptor!;
        byte[] source = new byte[PacketSize * BatchSize];
        byte[] packets = new byte[source.Length];
        PacketDecryptionResult[] results = new PacketDecryptionResult[BatchSize];
        CreateSourcePackets(source);
        if (!DecryptOnce(initializedDecryptor, source, packets, results))
        {
            return 2;
        }
        if (ComputeFnv1a64(packets) != ExpectedOutputHash)
        {
            return 5;
        }

        byte[] initializationBlocks = new byte[BatchSize * CsaStreamCipher.BlockSize];
        byte[] streamOutput = new byte[BatchSize * 23 * CsaStreamCipher.BlockSize];
        byte[] blockInput = new byte[BatchSize * CsaBlockCipher.BlockSize];
        byte[] blockOutput = new byte[blockInput.Length];
        byte[] blockState = new byte[BatchSize * 64];
        for (int lane = 0; lane < BatchSize; lane++)
        {
            source.AsSpan((lane * PacketSize) + 4, CsaStreamCipher.BlockSize)
                .CopyTo(initializationBlocks.AsSpan(lane * CsaStreamCipher.BlockSize));
            source.AsSpan((lane * PacketSize) + 4, CsaBlockCipher.BlockSize)
                .CopyTo(blockInput.AsSpan(lane * CsaBlockCipher.BlockSize));
        }

        double[] endToEndSamples = MeasureDecrypt(initializedDecryptor, source, packets, results);
        double[] streamSamples = MeasureStream(streamA, streamB, initializationBlocks, streamOutput);
        double[] blockSamples = MeasureBlock(blockSchedule, blockInput, blockOutput, blockState);
        double medianEndToEnd = Median(endToEndSamples);
        double bytesPerSecond = PayloadSize * (1_000_000_000d / medianEndToEnd);
        string processor = EscapeJson(Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown");

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":\"ffdecsa-x64-probe-v1\",\"runtime\":\"{Environment.Version}\",\"architecture\":\"{RuntimeInformation.ProcessArchitecture}\",\"processor\":\"{processor}\",\"is_64_bit_process\":{Environment.Is64BitProcess.ToString().ToLowerInvariant()},\"avx2\":{Avx2.IsSupported.ToString().ToLowerInvariant()},\"avx512_vbmi\":{Avx512Vbmi.IsSupported.ToString().ToLowerInvariant()},\"vector256\":{Vector256.IsHardwareAccelerated.ToString().ToLowerInvariant()},\"vector512\":{Vector512.IsHardwareAccelerated.ToString().ToLowerInvariant()},\"block_core_backend\":\"{CsaBlockCipher.ColumnMajor128CoreBackend}\",\"block_state_update_backend\":\"{CsaBlockCipher.ColumnMajor128StateUpdateBackend}\",\"block_lookup_backend\":\"{CsaBlockCipher.TransformLookupBackend}\",\"block_transform_output_layout\":\"{CsaBlockCipher.TransformOutputLayoutBackend}\",\"batch_packets\":{BatchSize},\"samples_per_metric\":{ProbeSampleCount},\"measurement_batches_per_sample\":{ProbeMeasurementIterations},\"end_to_end_ns_per_packet\":{{\"samples\":[{FormatSamples(endToEndSamples)}],\"min\":{endToEndSamples.Min():F3},\"median\":{medianEndToEnd:F3},\"max\":{endToEndSamples.Max():F3}}},\"end_to_end_megabytes_per_second\":{bytesPerSecond / 1_000_000d:F3},\"stream_ns_per_packet\":{{\"samples\":[{FormatSamples(streamSamples)}],\"min\":{streamSamples.Min():F3},\"median\":{Median(streamSamples):F3},\"max\":{streamSamples.Max():F3}}},\"block_ns_per_block\":{{\"samples\":[{FormatSamples(blockSamples)}],\"min\":{blockSamples.Min():F3},\"median\":{Median(blockSamples):F3},\"max\":{blockSamples.Max():F3}}},\"verified\":true}}"));
        return 0;
    }

    private static int RunParallelProbe()
    {
        ReadOnlySpan<byte> even = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
        ReadOnlySpan<byte> odd = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];
        if (!ControlWords.TryCreate(even, odd, out ControlWords controlWords)
            || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
        {
            return 1;
        }

        Decryptor initializedDecryptor = decryptor!;
        int[] workerCounts = GetParallelWorkerCounts(Environment.ProcessorCount);
        var results = new ParallelProbeResult[workerCounts.Length];
        for (int index = 0; index < workerCounts.Length; index++)
        {
            int workerCount = workerCounts[index];
            ParallelWorkerState[] workers = CreateParallelWorkers(workerCount);
            RunParallelDecrypt(initializedDecryptor, workers, ProbeWarmupIterations);

            double[] samples = new double[ProbeSampleCount];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                long criticalPathTicks = RunParallelDecrypt(initializedDecryptor, workers, ProbeMeasurementIterations);
                samples[sample] = criticalPathTicks * (1_000_000_000d / Stopwatch.Frequency)
                    / (ProbeMeasurementIterations * (double)BatchSize);
            }

            if (!VerifyParallelWorkers(workers))
            {
                return 5;
            }

            results[index] = new ParallelProbeResult(workerCount, samples);
        }

        double oneWorkerMedian = Median(results[0].NanosecondsPerPacketSamples);
        string rows = string.Join(',', results.Select(result => FormatParallelProbeResult(result, oneWorkerMedian)));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"format\":\"ffdecsa-parallel-probe-v1\",\"runtime\":\"{Environment.Version}\",\"architecture\":\"{RuntimeInformation.ProcessArchitecture}\",\"logical_processors\":{Environment.ProcessorCount},\"worker_execution\":\"dedicated_long_running_tasks\",\"decryptor_instances\":1,\"packet_buffers_per_worker\":1,\"batch_packets_per_worker\":{BatchSize},\"samples_per_worker_count\":{ProbeSampleCount},\"warmup_batches_per_worker\":{ProbeWarmupIterations},\"measurement_batches_per_worker\":{ProbeMeasurementIterations},\"timed_scope\":\"decrypt_only_per_worker_critical_path\",\"copy_in_timed_scope\":false,\"payload_bytes_per_packet\":{PayloadSize},\"output_fnv1a64\":\"{ExpectedOutputHash:X16}\",\"results\":[{rows}],\"verified\":true}}"));
        return 0;
    }

    private static double[] MeasureDecrypt(Decryptor decryptor, byte[] source, byte[] packets, PacketDecryptionResult[] results)
    {
        for (int iteration = 0; iteration < ProbeWarmupIterations; iteration++)
        {
            if (!DecryptOnce(decryptor, source, packets, results))
            {
                throw new InvalidOperationException("Probe warmup failed.");
            }
        }

        double[] samples = new double[ProbeSampleCount];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long elapsedTicks = 0;
            for (int iteration = 0; iteration < ProbeMeasurementIterations; iteration++)
            {
                source.CopyTo(packets, 0);
                long started = Stopwatch.GetTimestamp();
                if (!decryptor.TryDecryptPackets(packets, results))
                {
                    throw new InvalidOperationException("Probe measurement failed.");
                }

                elapsedTicks += Stopwatch.GetTimestamp() - started;
            }

            samples[sample] = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency) / (ProbeMeasurementIterations * (double)BatchSize);
        }

        return samples;
    }

    private static double[] MeasureStream(byte[] streamA, byte[] streamB, byte[] initializationBlocks, byte[] destination)
    {
        for (int iteration = 0; iteration < ProbeWarmupIterations; iteration++)
        {
            if (!CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, BatchSize, 23, destination))
            {
                throw new InvalidOperationException("Stream probe warmup failed.");
            }
        }

        double[] samples = new double[ProbeSampleCount];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long started = Stopwatch.GetTimestamp();
            for (int iteration = 0; iteration < ProbeMeasurementIterations; iteration++)
            {
                if (!CsaBitslicedStreamCipher.TryGenerateBlocks(streamA, streamB, initializationBlocks, BatchSize, 23, destination))
                {
                    throw new InvalidOperationException("Stream probe measurement failed.");
                }
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - started;
            samples[sample] = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency) / (ProbeMeasurementIterations * (double)BatchSize);
        }

        return samples;
    }

    private static double[] MeasureBlock(byte[] blockSchedule, byte[] input, byte[] output, byte[] state)
    {
        for (int iteration = 0; iteration < ProbeWarmupIterations; iteration++)
        {
            CsaBlockCipher.DecipherBlocksColumnMajor(blockSchedule, input, output, BatchSize, state);
        }

        double[] samples = new double[ProbeSampleCount];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long started = Stopwatch.GetTimestamp();
            for (int iteration = 0; iteration < ProbeMeasurementIterations; iteration++)
            {
                CsaBlockCipher.DecipherBlocksColumnMajor(blockSchedule, input, output, BatchSize, state);
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - started;
            samples[sample] = elapsedTicks * (1_000_000_000d / Stopwatch.Frequency) / (ProbeMeasurementIterations * (double)BatchSize);
        }

        return samples;
    }

    private static double Median(double[] samples)
    {
        double[] ordered = samples.ToArray();
        Array.Sort(ordered);
        return ordered[ordered.Length / 2];
    }

    private static string FormatSamples(IEnumerable<double> samples) => string.Join(
        ',',
        samples.Select(sample => sample.ToString("F3", CultureInfo.InvariantCulture)));

    private static string EscapeJson(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static int[] GetParallelWorkerCounts(int logicalProcessorCount)
    {
        var workerCounts = new List<int>();
        for (int workerCount = 1; workerCount < logicalProcessorCount; workerCount *= 2)
        {
            workerCounts.Add(workerCount);
        }

        if (workerCounts.Count == 0 || workerCounts[^1] != logicalProcessorCount)
        {
            workerCounts.Add(logicalProcessorCount);
        }

        return workerCounts.ToArray();
    }

    private static ParallelWorkerState[] CreateParallelWorkers(int workerCount)
    {
        var workers = new ParallelWorkerState[workerCount];
        for (int index = 0; index < workers.Length; index++)
        {
            byte[] source = new byte[PacketSize * BatchSize];
            CreateSourcePackets(source);
            workers[index] = new ParallelWorkerState(source, new byte[source.Length], new PacketDecryptionResult[BatchSize]);
        }

        return workers;
    }

    private static long RunParallelDecrypt(Decryptor decryptor, ParallelWorkerState[] workers, int batchCount)
    {
        using var ready = new CountdownEvent(workers.Length);
        using var completed = new CountdownEvent(workers.Length);
        using var start = new ManualResetEventSlim(false);
        Task[] tasks = new Task[workers.Length];

        for (int index = 0; index < workers.Length; index++)
        {
            ParallelWorkerState worker = workers[index];
            tasks[index] = Task.Factory.StartNew(
                () =>
                {
                    ready.Signal();
                    start.Wait();

                    long elapsedTicks = 0;
                    try
                    {
                        for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
                        {
                            worker.Source.CopyTo(worker.Packets, 0);
                            long started = Stopwatch.GetTimestamp();
                            if (!decryptor.TryDecryptPackets(worker.Packets, worker.Results))
                            {
                                throw new InvalidOperationException("Parallel decryption failed.");
                            }

                            elapsedTicks += Stopwatch.GetTimestamp() - started;
                        }

                        worker.ElapsedTicks = elapsedTicks;
                    }
                    catch (Exception exception)
                    {
                        worker.Exception = exception;
                    }
                    finally
                    {
                        completed.Signal();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        ready.Wait();
        start.Set();
        completed.Wait();
        Task.WaitAll(tasks);

        Exception? exception = workers.Select(static worker => worker.Exception).FirstOrDefault(static exception => exception is not null);
        if (exception is not null)
        {
            throw new InvalidOperationException("Parallel probe failed.", exception);
        }

        return workers.Max(static worker => worker.ElapsedTicks);
    }

    private static bool VerifyParallelWorkers(IEnumerable<ParallelWorkerState> workers)
    {
        foreach (ParallelWorkerState worker in workers)
        {
            if (ComputeFnv1a64(worker.Packets) != ExpectedOutputHash)
            {
                return false;
            }

            foreach (PacketDecryptionResult result in worker.Results)
            {
                if (result != PacketDecryptionResult.Decrypted)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string FormatParallelProbeResult(ParallelProbeResult result, double oneWorkerMedian)
    {
        double median = Median(result.NanosecondsPerPacketSamples);
        double packetsPerSecond = result.WorkerCount * (1_000_000_000d / median);
        double megabytesPerSecond = (packetsPerSecond * PayloadSize) / 1_000_000d;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"workers\":{result.WorkerCount},\"nanoseconds_per_packet\":{{\"samples\":[{FormatSamples(result.NanosecondsPerPacketSamples)}],\"min\":{result.NanosecondsPerPacketSamples.Min():F3},\"median\":{median:F3},\"max\":{result.NanosecondsPerPacketSamples.Max():F3}}},\"aggregate_packets_per_second\":{packetsPerSecond:F3},\"aggregate_payload_megabytes_per_second\":{megabytesPerSecond:F3},\"scaling_vs_one_worker\":{oneWorkerMedian / median * result.WorkerCount:F3}}}");
    }

    private static void CreateSourcePackets(Span<byte> source)
    {
        for (int packetIndex = 0; packetIndex < BatchSize; packetIndex++)
        {
            Span<byte> packet = source.Slice(packetIndex * PacketSize, PacketSize);
            packet[0] = 0x47;
            packet[3] = 0xD0;
            for (int payloadIndex = 0; payloadIndex < PayloadSize; payloadIndex++)
            {
                packet[payloadIndex + 4] = (byte)((packetIndex * 29) + (payloadIndex * 37));
            }
        }
    }

    private static bool DecryptOnce(Decryptor decryptor, byte[] source, byte[] packets, PacketDecryptionResult[] results)
    {
        source.CopyTo(packets, 0);
        if (!decryptor.TryDecryptPackets(packets, results))
        {
            return false;
        }

        for (int index = 0; index < results.Length; index++)
        {
            if (results[index] != PacketDecryptionResult.Decrypted)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong ComputeFnv1a64(ReadOnlySpan<byte> data)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in data)
        {
            hash = (hash ^ value) * 1099511628211UL;
        }

        return hash;
    }

    private sealed class ParallelWorkerState
    {
        public ParallelWorkerState(byte[] source, byte[] packets, PacketDecryptionResult[] results)
        {
            Source = source;
            Packets = packets;
            Results = results;
        }

        public byte[] Source { get; }

        public byte[] Packets { get; }

        public PacketDecryptionResult[] Results { get; }

        public long ElapsedTicks { get; set; }

        public Exception? Exception { get; set; }
    }

    private readonly record struct ParallelProbeResult(int WorkerCount, double[] NanosecondsPerPacketSamples);
}
