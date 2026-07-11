# FFDecsaSharp

FFDecsaSharp is a modern .NET 10 implementation of DVB-CSA packet decryption, rebuilt from the FFdecsa algorithm for 188-byte MPEG transport stream packets.

The library is span-first, NativeAOT-compatible, and keeps packet decryption paths free of managed allocations.

## Status

- Decrypts MPEG-TS packets scrambled with even or odd DVB-CSA control words.
- Supports full payloads, adaptation-field payload offsets, and residual payload bytes.
- Provides single-packet and contiguous batch APIs.
- Batches up to 64 full-payload packets per control word, including packets interleaved by key parity.
- Covers every packet shape exercised by the upstream FFdecsa test driver.
- Does not currently provide encryption or a GUI application.

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

## Build And Test

```sh
dotnet build src/FFDecsaSharp.slnx --no-restore -m:1
dotnet test src/FFDecsaSharp.slnx --no-build
```

Run the short performance suite with:

```sh
dotnet run -c Release --project src/FFDecsaSharp.Benchmarks/FFDecsaSharp.Benchmarks.csproj -- --job short
```

## Layout

- `src/FFDecsaSharp`: library and DVB-CSA implementation.
- `src/FFDecsaSharp.Tests`: unit, differential, and FFdecsa compatibility tests.
- `src/FFDecsaSharp.Benchmarks`: BenchmarkDotNet performance checks.
- `src/FFDecsaSharp.Gui`: reserved assembly for a future Avalonia front end.
- `references/FFdecsa`: upstream source retained for calibration and compatibility study.

## Verification

GitHub Actions restores, builds, and runs the test suite on Ubuntu, macOS, and Windows when the repository is connected to GitHub.
