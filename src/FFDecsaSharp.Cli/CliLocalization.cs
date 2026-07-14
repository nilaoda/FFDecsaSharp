using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FFDecsaSharp.Cli;

internal enum CliLanguage
{
    Auto,
    English,
    SimplifiedChinese,
    TraditionalChinese,
}

internal sealed partial class CliLocalizer
{
    private const int LocaleNameMaxLength = 85;
    private const string GitHubUrl = "https://github.com/nilaoda/FFDecsaSharp";

    private CliLocalizer(CliLanguage language) => Language = language;

    public CliLanguage Language { get; }

    public static CliLocalizer Create(CliLanguage requested) => new(requested == CliLanguage.Auto ? DetectSystemLanguage() : requested);

    public static bool TryParseLanguage(string text, out CliLanguage language)
    {
        language = text.Trim().ToLowerInvariant() switch
        {
            "auto" => CliLanguage.Auto,
            "en" or "en-us" or "english" => CliLanguage.English,
            "zh" or "zh-hans" or "zh-cn" or "zh-sg" or "simplified-chinese" => CliLanguage.SimplifiedChinese,
            "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo" or "traditional-chinese" => CliLanguage.TraditionalChinese,
            _ => CliLanguage.Auto,
        };
        return text.Trim().ToLowerInvariant() is
            "auto" or "en" or "en-us" or "english" or "zh" or "zh-hans" or "zh-cn" or "zh-sg" or "simplified-chinese"
            or "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo" or "traditional-chinese";
    }

    public string Usage => Language switch
    {
        CliLanguage.SimplifiedChinese => """
FFDecsaSharp 命令行工具 — DVB-CSA MPEG-TS 解密

用法：
  ffdecsasharp decrypt --input <文件.ts> [--input <文件.ts> ...] --output <输出.ts>
                       (--cw <12 或 16 位十六进制> | --even-cw <12 或 16 位十六进制> --odd-cw <12 或 16 位十六进制>)
                       [--workers <数量>] [--offset <包数>] [--limit <包数>]
                       [--overwrite] [--json] [--no-progress]
  ffdecsasharp benchmark [--workers <数量>] [--batches <数量>] [--json]

选项：
  -i, --input       输入 MPEG-TS 文件；可重复指定以按顺序合并。
  -o, --output      输出 MPEG-TS 文件。
      --cw          同时用于偶、奇校验包的控制字。
      --even-cw     偶校验包控制字；必须同时指定 --odd-cw。
      --odd-cw      奇校验包控制字；必须同时指定 --even-cw。
  -w, --workers     解密线程数，范围为 1 到 CPU 线程数（默认：1）。
      --offset      从第几个包开始处理（默认：0）。
      --limit       最多处理多少个包；0 表示全部剩余包（默认：0）。
      --overwrite   仅在成功解密后替换已有输出文件。
      --json        将最终结果以 JSON 输出到标准输出。
      --no-progress 禁止向标准错误输出进度。
      --batches     Benchmark 测量工作量（默认：15000）。
      --lang        语言：auto、en、zh-Hans 或 zh-Hant（默认：auto）。
  -h, --help        显示本帮助。
      --version     显示版本信息。
""",
        CliLanguage.TraditionalChinese => """
FFDecsaSharp 命令列工具 — DVB-CSA MPEG-TS 解密

用法：
  ffdecsasharp decrypt --input <檔案.ts> [--input <檔案.ts> ...] --output <輸出.ts>
                       (--cw <12 或 16 位十六進位> | --even-cw <12 或 16 位十六進位> --odd-cw <12 或 16 位十六進位>)
                       [--workers <數量>] [--offset <封包數>] [--limit <封包數>]
                       [--overwrite] [--json] [--no-progress]
  ffdecsasharp benchmark [--workers <數量>] [--batches <數量>] [--json]

選項：
  -i, --input       輸入 MPEG-TS 檔案；可重複指定以依序合併。
  -o, --output      輸出 MPEG-TS 檔案。
      --cw          同時用於偶、奇校驗封包的控制字。
      --even-cw     偶校驗封包控制字；必須同時指定 --odd-cw。
      --odd-cw      奇校驗封包控制字；必須同時指定 --even-cw。
  -w, --workers     解密執行緒數，範圍為 1 到 CPU 執行緒數（預設：1）。
      --offset      從第幾個封包開始處理（預設：0）。
      --limit       最多處理多少個封包；0 表示全部剩餘封包（預設：0）。
      --overwrite   僅在成功解密後取代已有輸出檔案。
      --json        將最終結果以 JSON 輸出至標準輸出。
      --no-progress 禁止向標準錯誤輸出進度。
      --batches     Benchmark 測量工作量（預設：15000）。
      --lang        語言：auto、en、zh-Hans 或 zh-Hant（預設：auto）。
  -h, --help        顯示本說明。
      --version     顯示版本資訊。
""",
        _ => """
FFDecsaSharp CLI — DVB-CSA MPEG-TS decryption

Usage:
  ffdecsasharp decrypt --input <file.ts> [--input <file.ts> ...] --output <output.ts>
                       (--cw <12-or-16-hex> | --even-cw <12-or-16-hex> --odd-cw <12-or-16-hex>)
                       [--workers <count>] [--offset <packets>] [--limit <packets>]
                       [--overwrite] [--json] [--no-progress]
  ffdecsasharp benchmark [--workers <count>] [--batches <count>] [--json]

Options:
  -i, --input       Input MPEG-TS file. Repeat for concatenated inputs.
  -o, --output      Output MPEG-TS file.
      --cw          One control word used for both even and odd packets.
      --even-cw     Even control word; must be supplied with --odd-cw.
      --odd-cw      Odd control word; must be supplied with --even-cw.
  -w, --workers     Decryption workers, from 1 to the processor count (default: 1).
      --offset      First packet to process (default: 0).
      --limit       Maximum packets to process; 0 means all remaining packets (default: 0).
      --overwrite   Replace an existing output file after a successful decrypt.
      --json        Write the final result as JSON to standard output.
      --no-progress Disable progress output on standard error.
      --batches     Benchmark measurement workload (default: 15000).
      --lang        Language: auto, en, zh-Hans, or zh-Hant (default: auto).
  -h, --help        Show this help text.
      --version     Show version information.
""",
    };

    public string ErrorPrefix => Select("error", "错误", "錯誤");
    public string ProjectHomepage => Select($"Project homepage: {GitHubUrl}", $"项目主页：{GitHubUrl}", $"專案首頁：{GitHubUrl}");
    public string UsageHint => Select("Run 'ffdecsasharp --help' for usage.", "运行 'ffdecsasharp --help' 查看用法。", "執行 'ffdecsasharp --help' 查看用法。");
    public string Cancelled => Select("Cancelled.", "已取消。", "已取消。");
    public string UnknownCommand(string value) => Select($"Unknown command '{value}'.", $"未知命令“{value}”。", $"未知命令「{value}」。");
    public string UnknownOption(string value) => Select($"Unknown option '{value}'.", $"未知选项“{value}”。", $"未知選項「{value}」。");
    public string OptionRequiresValue(string value) => Select($"{value} requires a value.", $"{value} 需要一个值。", $"{value} 需要一個值。");
    public string DuplicateLanguageOption => Select("--lang may be specified only once.", "--lang 只能指定一次。", "--lang 只能指定一次。");
    public string InvalidLanguage(string value) => Select($"Unsupported language '{value}'. Use auto, en, zh-Hans, or zh-Hant.", $"不支持语言“{value}”。可使用 auto、en、zh-Hans 或 zh-Hant。", $"不支援語言「{value}」。可使用 auto、en、zh-Hans 或 zh-Hant。");
    public string NoInput => Select("At least one --input is required.", "至少需要一个 --input。", "至少需要一個 --input。");
    public string OutputRequired => Select("--output is required.", "需要指定 --output。", "需要指定 --output。");
    public string MixedControlWords => Select("Use either --cw or --even-cw/--odd-cw, not both.", "请使用 --cw 或 --even-cw/--odd-cw 之一，不能同时使用。", "請使用 --cw 或 --even-cw/--odd-cw 其中之一，不能同時使用。");
    public string MissingControlWords => Select("Specify --cw or both --even-cw and --odd-cw.", "请指定 --cw，或同时指定 --even-cw 与 --odd-cw。", "請指定 --cw，或同時指定 --even-cw 與 --odd-cw。");
    public string InvalidEvenControlWord => Select("The even control word must be 12 or 16 hexadecimal characters.", "偶控制字必须是 12 或 16 位十六进制字符。", "偶控制字必須是 12 或 16 位十六進位字元。");
    public string InvalidOddControlWord => Select("The odd control word must be 12 or 16 hexadecimal characters.", "奇控制字必须是 12 或 16 位十六进制字符。", "奇控制字必須是 12 或 16 位十六進位字元。");
    public string PositiveInteger(string option) => Select($"{option} must be a positive integer.", $"{option} 必须是正整数。", $"{option} 必須是正整數。");
    public string NonNegativeInteger(string option) => Select($"{option} must be a non-negative integer.", $"{option} 必须是非负整数。", $"{option} 必須是非負整數。");
    public string WorkerLimit(int maximum) => Select($"--workers must not exceed the available processor count ({maximum}).", $"--workers 不能超过可用 CPU 线程数（{maximum}）。", $"--workers 不得超過可用 CPU 執行緒數（{maximum}）。");
    public string OutputMatchesInput => Select("The output path must not be one of the input paths.", "输出路径不能与输入路径相同。", "輸出路徑不能與輸入路徑相同。");
    public string OutputExists(string path) => Select($"Output file already exists: '{path}'. Use --overwrite to replace it.", $"输出文件已存在：“{path}”。请使用 --overwrite 替换。", $"輸出檔案已存在：「{path}」。請使用 --overwrite 取代。");
    public string DecryptionStarting(int inputCount, string outputPath, int workers) => Select($"Decrypting {inputCount} input file(s) to {outputPath} with {workers} worker(s).", $"正在解密 {inputCount} 个输入文件到 {outputPath}，使用 {workers} 个工作线程。", $"正在解密 {inputCount} 個輸入檔案到 {outputPath}，使用 {workers} 個工作執行緒。");
    public string DecryptionComplete(long decrypted, long packets, double elapsedSeconds) => Select($"Decrypted {decrypted:N0} of {packets:N0} packets in {elapsedSeconds:F3}s.", $"已解密 {packets:N0} 个包中的 {decrypted:N0} 个，耗时 {elapsedSeconds:F3} 秒。", $"已解密 {packets:N0} 個封包中的 {decrypted:N0} 個，耗時 {elapsedSeconds:F3} 秒。");
    public string AverageThroughput(string speed) => Select($"Average throughput: {speed}/s.", $"平均吞吐：{speed}/s。", $"平均輸送量：{speed}/s。");
    public string SkippedLeadingBytes(long count) => Select($"Skipped {count:N0} leading byte(s) while resynchronizing MPEG-TS packets.", $"重同步 MPEG-TS 包时跳过了前导 {count:N0} bytes。", $"重新同步 MPEG-TS 封包時略過了前導 {count:N0} bytes。");
    public string IgnoredTrailingBytes(long count) => Select($"Ignored {count:N0} trailing byte(s) that do not form a complete 188-byte TS packet.", $"已忽略末尾 {count:N0} bytes，未构成完整的 188-byte TS 包。", $"已忽略末尾 {count:N0} bytes，未構成完整的 188-byte TS 封包。");
    public string OutputLabel(string path) => Select($"Output: {path}", $"输出：{path}", $"輸出：{path}");
    public string BenchmarkResult(double megabytesPerSecond, int workers, int batches) => Select($"{megabytesPerSecond:F1} MB/s payload, {workers} worker(s), {batches:N0} batches.", $"有效负载 {megabytesPerSecond:F1} MB/s，{workers} 个工作线程，{batches:N0} 个批次。", $"有效負載 {megabytesPerSecond:F1} MB/s，{workers} 個工作執行緒，{batches:N0} 個批次。");

    private string Select(string english, string simplifiedChinese, string traditionalChinese) => Language switch
    {
        CliLanguage.SimplifiedChinese => simplifiedChinese,
        CliLanguage.TraditionalChinese => traditionalChinese,
        _ => english,
    };

    private static CliLanguage DetectSystemLanguage()
    {
        foreach (string name in GetPreferredLanguageNames())
        {
            string normalized = name.Trim().Trim('"').Replace('_', '-');
            int dotIndex = normalized.IndexOf('.', StringComparison.Ordinal);
            if (dotIndex >= 0) normalized = normalized[..dotIndex];
            if (normalized.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("-TW", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("-HK", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("-MO", StringComparison.OrdinalIgnoreCase))
                return CliLanguage.TraditionalChinese;
            if (normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return CliLanguage.SimplifiedChinese;
            if (normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return CliLanguage.English;
        }

        return CliLanguage.English;
    }

    private static IEnumerable<string> GetPreferredLanguageNames()
    {
        if (OperatingSystem.IsWindows())
        {
            var buffer = new char[LocaleNameMaxLength];
            if (GetUserDefaultLocaleName(buffer, buffer.Length) > 0) yield return new string(buffer).TrimEnd('\0');
        }
        if (OperatingSystem.IsMacOS())
        {
            foreach (string name in ReadMacOSAppleLanguages()) yield return name;
        }
        foreach (string variable in new[] { "LC_ALL", "LC_MESSAGES", "LANG" })
            yield return Environment.GetEnvironmentVariable(variable) ?? "";
    }

    private static IReadOnlyList<string> ReadMacOSAppleLanguages()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/defaults",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("read");
            startInfo.ArgumentList.Add("-g");
            startInfo.ArgumentList.Add("AppleLanguages");
            using Process? process = Process.Start(startInfo);
            if (process is null || !process.WaitForExit(1_000)) return [];
            return process.StandardOutput.ReadToEnd().Split('\n').Select(static line => line.Trim().Trim(',', '"')).Where(static name => name.Length > 0 && name is not "(" and not ")").ToArray();
        }
        catch { return []; }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultLocaleName", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetUserDefaultLocaleName([Out] char[] localeName, int cchLocaleName);
}
