using FFDecsaSharp.CSA;

namespace FFDecsaSharp.Gui.Services;

internal static class PacketBlockDecryptionService
{
    public const int PacketsPerBitsliceBatch = 128;

    public static int GetPacketsPerBlock(int requestedWorkerCount) => Math.Max(
        TsDecryptionService.DefaultPacketsPerBlock,
        checked(AppSettingsService.CoerceDecryptionWorkerCount(requestedWorkerCount) * TsDecryptionService.DefaultPacketsPerBlock));
}
