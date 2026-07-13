using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Gui.Services;

internal readonly record struct DecryptionProgress(long ProcessedBytes, long TotalBytes, long PacketCount, long DecryptedCount, TimeSpan Elapsed);
internal readonly record struct DecryptionSummary(long PacketCount, long DecryptedCount, TimeSpan Elapsed);

internal static class TsDecryptionService
{
    private const int PacketSize = 188;
    public const int DefaultPacketsPerBlock = 4096;
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);

    public static async Task<DecryptionSummary> DecryptAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        byte[] evenKey,
        byte[] oddKey,
        long packetOffset,
        long packetLimit,
        IProgress<DecryptionProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (!ControlWords.TryCreate(evenKey, oddKey, out var words) || !Decryptor.TryCreate(words, out var decryptor))
            throw new InvalidDataException(LocalizationService.Get(LocKeys.Error_ControlWords));
        if (inputPaths.Count == 0) throw new InvalidDataException(LocalizationService.Get(LocKeys.Error_NoInputFiles));

        IReadOnlyList<PreparedInput> preparedInputs = await PrepareInputsAsync(inputPaths, packetOffset, packetLimit, cancellationToken).ConfigureAwait(false);
        long totalBytes = preparedInputs.Sum(static input => input.Length);
        if (totalBytes == 0) throw new InvalidDataException(LocalizationService.Get(LocKeys.Error_PacketAlignment));

        EnsureSufficientDiskSpace(outputPath, totalBytes);
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        int workerCount = AppSettingsService.DecryptionWorkerCount;
        int packetsPerBlock = PacketBlockDecryptionService.GetPacketsPerBlock(workerCount);
        var buffer = new byte[PacketSize * packetsPerBlock];
        var results = new PacketDecryptionResult[packetsPerBlock];
        using var workerGroup = new PacketDecryptionWorkerGroup(decryptor!, workerCount);
        long processed = 0;
        long packets = 0;
        long decrypted = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan lastProgressElapsed = TimeSpan.Zero;

        foreach (PreparedInput prepared in preparedInputs)
        {
            await using var input = new FileStream(prepared.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            input.Position = prepared.StartPosition;
            long remaining = prepared.Length;

            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int blockLength = (int)Math.Min(buffer.Length, remaining);
                await input.ReadExactlyAsync(buffer.AsMemory(0, blockLength), cancellationToken).ConfigureAwait(false);
                int blockPackets = blockLength / PacketSize;
                bool succeeded = workerGroup.TryDecrypt(buffer, results, blockPackets, cancellationToken);
                if (!succeeded) throw new InvalidDataException(LocalizationService.Get(LocKeys.Error_ProcessBuffer));
                await output.WriteAsync(buffer.AsMemory(0, blockLength), cancellationToken).ConfigureAwait(false);

                processed += blockLength;
                remaining -= blockLength;
                packets += blockPackets;
                for (int packetIndex = 0; packetIndex < blockPackets; packetIndex++)
                {
                    if (results[packetIndex] == PacketDecryptionResult.Decrypted) decrypted++;
                }

                TimeSpan elapsed = started.Elapsed;
                if (processed == totalBytes || elapsed - lastProgressElapsed >= ProgressInterval)
                {
                    progress?.Report(new DecryptionProgress(processed, totalBytes, packets, decrypted, elapsed));
                    lastProgressElapsed = elapsed;
                }
            }
        }

        return new DecryptionSummary(packets, decrypted, started.Elapsed);
    }

    private static async Task<IReadOnlyList<PreparedInput>> PrepareInputsAsync(IReadOnlyList<string> inputPaths, long packetOffset, long packetLimit, CancellationToken cancellationToken)
    {
        long requestedOffset = checked(packetOffset * PacketSize);
        long remainingLimit = packetLimit == 0 ? long.MaxValue : checked(packetLimit * PacketSize);
        var prepared = new List<PreparedInput>(inputPaths.Count);

        foreach (string inputPath in inputPaths)
        {
            if (remainingLimit == 0) break;
            await using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            long syncPosition = await FindSyncPositionAsync(input, requestedOffset, cancellationToken).ConfigureAwait(false);
            if (syncPosition < 0) throw new InvalidDataException(LocalizationService.Get(LocKeys.Error_PacketAlignment));

            long availableBytes = ((input.Length - syncPosition) / PacketSize) * PacketSize;
            long length = Math.Min(availableBytes, remainingLimit);
            if (length == 0) continue;

            prepared.Add(new PreparedInput(inputPath, syncPosition, length));
            if (remainingLimit != long.MaxValue) remainingLimit -= length;
        }

        return prepared;
    }

    private static void EnsureSufficientDiskSpace(string outputPath, long requiredBytes)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        string existingDirectory = FindExistingDirectory(Path.GetDirectoryName(fullOutputPath) ?? Environment.CurrentDirectory);
        DriveInfo? volume = DriveInfo.GetDrives()
            .Where(static drive => drive.IsReady)
            .OrderByDescending(static drive => drive.RootDirectory.FullName.Length)
            .FirstOrDefault(drive => IsPathOnVolume(existingDirectory, drive.RootDirectory.FullName));

        volume ??= new DriveInfo(Path.GetPathRoot(existingDirectory) ?? Path.DirectorySeparatorChar.ToString());
        if (!volume.IsReady || volume.AvailableFreeSpace < requiredBytes)
            throw new IOException(LocalizationService.Get(LocKeys.Error_InsufficientDiskSpace));
    }

    private static string FindExistingDirectory(string directory)
    {
        string current = Path.GetFullPath(directory);
        while (!Directory.Exists(current))
        {
            DirectoryInfo? parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }
        return current;
    }

    private static bool IsPathOnVolume(string path, string root)
    {
        string normalizedRoot = Path.GetFullPath(root);
        return path.StartsWith(normalizedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static async Task<long> FindSyncPositionAsync(FileStream input, long offset, CancellationToken cancellationToken)
    {
        if (offset < 0 || offset >= input.Length) return -1;
        input.Position = offset;
        int length = (int)Math.Min(1024 * 1024, input.Length - offset);
        var probe = new byte[length];
        await input.ReadExactlyAsync(probe, cancellationToken).ConfigureAwait(false);

        for (int index = 0; index < probe.Length; index++)
        {
            if (probe[index] != 0x47) continue;
            if (IsSyncAt(probe, index + PacketSize) && IsSyncAt(probe, index + (PacketSize * 2))) return offset + index;
        }

        return -1;
    }

    private static bool IsSyncAt(byte[] probe, int index) => index >= probe.Length || probe[index] == 0x47;

    private readonly record struct PreparedInput(string Path, long StartPosition, long Length);
}
