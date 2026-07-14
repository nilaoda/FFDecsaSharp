using System.Globalization;
using FFDecsaSharp.TransportStream;

namespace FFDecsaSharp.Cli;

internal abstract record CliCommand;
internal sealed record HelpCommand : CliCommand;
internal sealed record VersionCommand : CliCommand;
internal sealed record DecryptCommand(IReadOnlyList<string> InputPaths, string OutputPath, byte[] EvenControlWord, byte[] OddControlWord, int WorkerCount, long PacketOffset, long PacketLimit, bool Overwrite, bool Json, bool ShowProgress) : CliCommand;
internal sealed record BenchmarkCommand(int WorkerCount, int MeasurementBatches, bool Json) : CliCommand;
internal sealed record CliInvocation(CliLocalizer Localizer, CliCommand Command);

internal sealed class CliUsageException(string message) : Exception(message);

internal static class CliParser
{
    public static CliInvocation Parse(IReadOnlyList<string> arguments)
    {
        (CliLocalizer localizer, string[] remaining) = ExtractLanguage(arguments);
        if (remaining.Length == 0) return new CliInvocation(localizer, new HelpCommand());
        if (remaining.Length == 1 && (remaining[0] is "--help" or "-h")) return new CliInvocation(localizer, new HelpCommand());
        if (remaining.Length == 1 && remaining[0] == "--version") return new CliInvocation(localizer, new VersionCommand());
        if (remaining.Skip(1).Any(static argument => argument is "--help" or "-h")) return new CliInvocation(localizer, new HelpCommand());

        CliCommand command = remaining[0] switch
        {
            "decrypt" => ParseDecrypt(remaining[1..], localizer),
            "benchmark" => ParseBenchmark(remaining[1..], localizer),
            _ => throw new CliUsageException(localizer.UnknownCommand(remaining[0])),
        };
        return new CliInvocation(localizer, command);
    }

    private static (CliLocalizer Localizer, string[] Remaining) ExtractLanguage(IReadOnlyList<string> arguments)
    {
        CliLanguage requestedLanguage = CliLanguage.Auto;
        bool languageSpecified = false;
        var remaining = new List<string>(arguments.Count);
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (argument is not "--lang" and not "--language")
            {
                remaining.Add(argument);
                continue;
            }

            CliLocalizer fallback = CliLocalizer.Create(CliLanguage.Auto);
            if (languageSpecified) throw new CliUsageException(fallback.DuplicateLanguageOption);
            if (++index >= arguments.Count || arguments[index].StartsWith("-", StringComparison.Ordinal))
                throw new CliUsageException(fallback.OptionRequiresValue(argument));
            if (!CliLocalizer.TryParseLanguage(arguments[index], out requestedLanguage))
                throw new CliUsageException(fallback.InvalidLanguage(arguments[index]));
            languageSpecified = true;
        }
        return (CliLocalizer.Create(requestedLanguage), remaining.ToArray());
    }

    private static DecryptCommand ParseDecrypt(IReadOnlyList<string> arguments, CliLocalizer localizer)
    {
        var inputs = new List<string>();
        string? output = null;
        string? commonControlWord = null;
        string? evenControlWord = null;
        string? oddControlWord = null;
        int workers = 1;
        long offset = 0;
        long limit = 0;
        bool overwrite = false;
        bool json = false;
        bool showProgress = true;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "-i" or "--input": inputs.Add(ReadValue(arguments, ref index, argument, localizer)); break;
                case "-o" or "--output": output = ReadValue(arguments, ref index, argument, localizer); break;
                case "--cw": commonControlWord = ReadValue(arguments, ref index, argument, localizer); break;
                case "--even-cw": evenControlWord = ReadValue(arguments, ref index, argument, localizer); break;
                case "--odd-cw": oddControlWord = ReadValue(arguments, ref index, argument, localizer); break;
                case "-w" or "--workers": workers = ParsePositiveInt(ReadValue(arguments, ref index, argument, localizer), argument, localizer); break;
                case "--offset": offset = ParseNonNegativeLong(ReadValue(arguments, ref index, argument, localizer), argument, localizer); break;
                case "--limit": limit = ParseNonNegativeLong(ReadValue(arguments, ref index, argument, localizer), argument, localizer); break;
                case "--overwrite": overwrite = true; break;
                case "--json": json = true; break;
                case "--no-progress": showProgress = false; break;
                default: throw new CliUsageException(localizer.UnknownOption(argument));
            }
        }

        if (inputs.Count == 0) throw new CliUsageException(localizer.NoInput);
        if (string.IsNullOrWhiteSpace(output)) throw new CliUsageException(localizer.OutputRequired);
        if (commonControlWord is not null && (evenControlWord is not null || oddControlWord is not null))
            throw new CliUsageException(localizer.MixedControlWords);
        if (commonControlWord is null && (evenControlWord is null || oddControlWord is null))
            throw new CliUsageException(localizer.MissingControlWords);
        if (!TryParseControlWord(commonControlWord ?? evenControlWord!, out byte[] even))
            throw new CliUsageException(localizer.InvalidEvenControlWord);
        if (!TryParseControlWord(commonControlWord ?? oddControlWord!, out byte[] odd))
            throw new CliUsageException(localizer.InvalidOddControlWord);

        return new DecryptCommand(inputs, output, even, odd, ValidateWorkerCount(workers, localizer), offset, limit, overwrite, json, showProgress);
    }

    private static BenchmarkCommand ParseBenchmark(IReadOnlyList<string> arguments, CliLocalizer localizer)
    {
        int workers = 1;
        int batches = 15_000;
        bool json = false;
        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            switch (argument)
            {
                case "-w" or "--workers": workers = ParsePositiveInt(ReadValue(arguments, ref index, argument, localizer), argument, localizer); break;
                case "--batches": batches = ParsePositiveInt(ReadValue(arguments, ref index, argument, localizer), argument, localizer); break;
                case "--json": json = true; break;
                default: throw new CliUsageException(localizer.UnknownOption(argument));
            }
        }
        return new BenchmarkCommand(ValidateWorkerCount(workers, localizer), batches, json);
    }

    private static string ReadValue(IReadOnlyList<string> arguments, ref int index, string option, CliLocalizer localizer)
    {
        if (++index >= arguments.Count || arguments[index].StartsWith("-", StringComparison.Ordinal))
            throw new CliUsageException(localizer.OptionRequiresValue(option));
        return arguments[index];
    }

    private static int ParsePositiveInt(string text, string option, CliLocalizer localizer)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
            throw new CliUsageException(localizer.PositiveInteger(option));
        return value;
    }

    private static long ParseNonNegativeLong(string text, string option, CliLocalizer localizer)
    {
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value) || value < 0)
            throw new CliUsageException(localizer.NonNegativeInteger(option));
        return value;
    }

    private static int ValidateWorkerCount(int workerCount, CliLocalizer localizer)
    {
        int maximum = Math.Max(1, Environment.ProcessorCount);
        if (workerCount > maximum) throw new CliUsageException(localizer.WorkerLimit(maximum));
        return TransportStreamDecryptionService.CoerceWorkerCount(workerCount);
    }

    private static bool TryParseControlWord(string text, out byte[] controlWord)
    {
        controlWord = [];
        try
        {
            byte[] input = Convert.FromHexString(text);
            if (input.Length == 8)
            {
                controlWord = input;
                return true;
            }
            if (input.Length != 6) return false;
            controlWord = [input[0], input[1], input[2], (byte)(input[0] + input[1] + input[2]), input[3], input[4], input[5], (byte)(input[3] + input[4] + input[5])];
            return true;
        }
        catch (FormatException) { return false; }
    }
}
