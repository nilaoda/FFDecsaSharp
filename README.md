# FFDecsaSharp

FFDecsaSharp is a modern .NET 10 implementation of DVB-CSA packet decryption, rebuilt from the FFdecsa algorithm for 188-byte MPEG transport stream packets.

The library is span-first, NativeAOT-compatible, and keeps packet decryption paths free of managed allocations.

## Source And Implementation Note

The retained reference FFdecsa source under `references/FFdecsa` originates from [gfto/tsdecrypt/FFdecsa](https://github.com/gfto/tsdecrypt/tree/master/FFdecsa). Apart from that reference code, approximately 99% of this project's code was implemented with Codex and GPT-5.6.

## Status

- Decrypts MPEG-TS packets scrambled with even or odd DVB-CSA control words.
- Supports full payloads, adaptation-field payload offsets, and residual payload bytes.
- Provides single-packet and contiguous batch APIs.
- Batches up to 128 full-payload packets per control word, including packets interleaved by key parity.
- Covers every packet shape exercised by the upstream FFdecsa test driver.
- Does not currently provide encryption.

## Use

Reference `src/FFDecsaSharp/FFDecsaSharp.csproj` from a .NET 10 project, then construct a decryptor from the current even and odd control words:

```csharp
using FFDecsaSharp.CSA;

ReadOnlySpan<byte> evenControlWord = [0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00];
ReadOnlySpan<byte> oddControlWord = [0x0F, 0x1E, 0x2D, 0x3C, 0x4B, 0x5A, 0x69, 0x78];

if (!ControlWords.TryCreate(evenControlWord, oddControlWord, out ControlWords controlWords)
    || !Decryptor.TryCreate(controlWords, out Decryptor? decryptor))
{
    throw new InvalidOperationException("Invalid DVB-CSA control words.");
}

PacketDecryptionResult result = decryptor.Decrypt(packet);
```

For a contiguous sequence of 188-byte packets, provide one result slot per packet:

```csharp
Span<PacketDecryptionResult> results = stackalloc PacketDecryptionResult[packetCount];
bool accepted = decryptor.TryDecryptPackets(packetBuffer, results);
```

`TryDecryptPackets` returns `false` only when the input length is not a multiple of 188 bytes or the result span is too short. It reports per-packet validation and decryption outcomes through `results`.

## Measured Performance

The following single-thread measurements use the normalized `ffdecsa-compare-v1`
protocol: 128 full-payload packets per batch, 5,000 warmup batches, 30,000
measured batches, packet copying outside the timed window, and the same
deterministic input and output checksum for C# and C. Throughput is decimal
payload MB/s (`184` payload bytes per packet), not the 188-byte transport rate.

| Host | Implementation | ns/packet | Payload throughput | Relative to C |
|---|---|---:|---:|---:|
| Apple M4 / .NET 10.0.8 / Arm64 | FFDecsaSharp | 512.257 | 359.2 MB/s | 97.0% |
| Apple M4 / Apple Clang FFdecsa `PARALLEL_128_2LONG` | C reference | ≈496.7 | ≈370.4 MB/s | 100% |
| Intel Core i5-10400F / .NET 10.0.8 / AVX2 | FFDecsaSharp | 1030.722 | 178.5 MB/s | 108.3% |
| Intel Core i5-10400F / Zig `cc -O3 -march=native` FFdecsa `PARALLEL_128_2LONG` | C reference | 1116.328 | 164.8 MB/s | 100% |

The Arm64 release probe on the local M4 has also reported approximately
**355 MB/s** single-thread payload throughput in a short multi-sample run.
Small differences from the normalized table are expected from CPU frequency,
run order, and the probe's different sample structure.

Multi-threaded decryption can increase aggregate throughput substantially
because `Decryptor` shares only immutable key schedules and each worker owns a
non-overlapping packet range. The dedicated-worker probe on the same M4
measured **346.8 MB/s** with one worker and **1729.8 MB/s** with ten workers
(about **5.0×**). That probe measures decrypt-only worker critical-path time;
actual file throughput also depends on block size, memory/cache behavior,
synchronization, storage, and CPU thermals. Configure the GUI worker count and
benchmark workload to tune for the target machine.

These C comparisons are against FFdecsa's `PARALLEL_128_2LONG` reference
configuration, not a claim against every possible native FFdecsa backend.

## CLI

`FFDecsaSharp.Cli` is a self-contained, NativeAOT command-line interface for
headless and scripted use. It never reads the GUI settings file: its default is
one worker, and every runtime setting is explicit on the command line.

Decrypt one input with a single control word for both key parities:

```sh
ffdecsasharp decrypt \
  --input encrypted.ts --output decrypted.ts \
  --cw 010203040506 --workers 4
```

Use separate even and odd words, concatenate multiple inputs, or restrict the
packet range when needed:

```sh
ffdecsasharp decrypt \
  --input part-1.ts --input part-2.ts --output output.ts \
  --even-cw 010203040506 --odd-cw A1A2A3A4A5A6 \
  --offset 0 --limit 0 --workers 4 --overwrite
```

Control words accept either six bytes (12 hexadecimal characters; DVB checksum
bytes are derived) or eight complete bytes (16 hexadecimal characters). Final
results can be emitted as JSON with `--json`; progress is written to standard
error, so standard output remains script-friendly. Run `ffdecsasharp --help`
for the full interface. CLI text automatically follows the operating-system
language where it can be detected, or may be selected explicitly with
`--lang en`, `--lang zh-Hans`, or `--lang zh-Hant`; it still never reads the
GUI settings file.

The same deterministic workload as the GUI benchmark is available without a
desktop environment:

```sh
ffdecsasharp benchmark --workers 4 --batches 15000 --json
```

Release artifacts are NativeAOT binaries for Windows, macOS, and Linux. Linux
`x64` and `arm64` artifacts are built in the Alpine musl Docker environments
used by N_m3u8DL-RE and verified as statically linked, avoiding a host libc
dependency. Each release contains both clearly named artifact groups:
`FFDecsaSharp_GUI_<rid>_...` for the desktop app and
`FFDecsaSharp_CLI_<rid>_...` for the command-line binary. During development,
run the CLI with:

```sh
dotnet run -c Release --project src/FFDecsaSharp.Cli/FFDecsaSharp.Cli.csproj -- --help
```

## Build And Test

```sh
dotnet build src/FFDecsaSharp.slnx --no-restore -m:1
dotnet test src/FFDecsaSharp.slnx --no-build
```

Run the short performance suite with:

```sh
dotnet run -c Release --project src/FFDecsaSharp.Benchmarks/FFDecsaSharp.Benchmarks.csproj -- --job short
```

Run the directly comparable managed and FFdecsa reference throughput harnesses with:

```sh
dotnet run -c Release --project src/FFDecsaSharp.PerfHarness/FFDecsaSharp.PerfHarness.csproj
tools/ffdecsa-compare/run-reference.sh
```

Both commands emit the same JSON schema. See `docs/BENCHMARK_PROTOCOL.md` for the timed scope and comparison rules.

To run the short multi-sample ISA and component probe used for x64 validation:

```sh
dotnet run -c Release --project src/FFDecsaSharp.PerfHarness/FFDecsaSharp.PerfHarness.csproj -- --probe
```

The probe reports the selected block backends plus end-to-end, stream, and block medians. It is designed to finish well below two minutes on the target machine. The source launchers and packager live in `tools/x64-probe/`; the Windows package includes a double-click `Run-X64-Probe.cmd` matrix launcher, `Run-C-Reference.cmd` that uses an existing Clang or installs Microsoft Build Tools when needed, and `Run-All-Benchmarks.cmd` for one complete managed-plus-C run. See `docs/X86_PROBE.md` for packaging and result interpretation.

## Layout

- `src/FFDecsaSharp`: library and DVB-CSA implementation.
- `src/FFDecsaSharp.Cli`: NativeAOT command-line decryptor and benchmark entry point.
- `src/FFDecsaSharp.Tests`: unit, differential, and FFdecsa compatibility tests.
- `src/FFDecsaSharp.Benchmarks`: BenchmarkDotNet performance checks.
- `src/FFDecsaSharp.PerfHarness`: dependency-free, machine-readable throughput harness.
- `tools/ffdecsa-compare`: FFdecsa reference build and matching throughput harness.
- `tools/StreamSboxSynthesis`: stream S-box synthesis tool.
- `src/FFDecsaSharp.Gui`: Avalonia GUI application.
- `src/FFDecsaSharp.Gui.Generators`: source generator for the GUI.
- `references/FFdecsa`: upstream source retained for calibration and compatibility study.

## Verification

GitHub Actions restores, builds, and runs the test suite on Ubuntu, macOS, and Windows when the repository is connected to GitHub.
