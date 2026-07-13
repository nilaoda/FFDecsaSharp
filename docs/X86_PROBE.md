# X64 Probe

`FFDecsaSharp.PerfHarness --probe` is a short diagnostic workload for a rented or borrowed x64 machine. It runs seven samples of the full decrypt path, bitsliced stream kernel, and column-major block cipher, then prints one JSON line. On the current Apple Silicon development host it completes in a few seconds; it is designed to stay far below two minutes on a slower x64 host.

## Run

For the Windows x64 package, extract the ZIP and double-click `Run-X64-Probe.cmd`. It launches five self-contained managed probes and keeps the command window open for copying:

1. the default packed S-box/permutation output layout with hardware-selected state width;
2. the old scalar-lookup, separate-byte-column control;
3. the normalized scalar lookup with the old separate-byte-column layout;
4. an AVX-512 VBMI lookup with the separate-byte-column layout; and
5. the default packed layout with forced `Vector256` state updates.

On AVX2 systems the default keeps each packed 16-bit table result until the vector update, instead of writing separate S-box and permutation byte columns. The default path uses `Vector512` automatically on an AVX-512-capable system, so runs 1 and 5 compare state width while retaining this layout. Runs 2 and 3 are the prior layout's scalar and normalized-input controls. Run 4 uses the native 64-byte AVX-512 VBMI table permutation when `avx512_vbmi=true`; it otherwise remains on the normalized separate-column control.

To run the complete one-click matrix, including strictly comparable managed C# and native C reference rows, double-click `Run-All-Benchmarks.cmd`. It runs the five short managed probes, then the standard managed `ffdecsa-compare-v1` protocol, then the native C protocol. The initial C run may take 10-20 minutes while Microsoft Build Tools installs; the five managed probes themselves are designed to finish within two minutes.

To run only the strict managed side, double-click `Run-CSharp-Reference.cmd`. Compare its `ffdecsa-compare-v1` JSON only with the C JSON: both use 5,000 warmup batches and 30,000 timed batches. The component values in the `--probe` JSON are diagnostic measurements, not the formal C#-versus-C ratio.

To run the packaged executable manually instead:

```text
FFDecsaSharp.PerfHarness.exe --probe
```

Copy the complete JSON line back without reformatting it. The executable is non-AOT: it uses the normal .NET JIT so ISA-specialized paths can be selected on the target processor.

## JIT assembly diagnostic

When the managed matrix identifies a plausible x64 candidate, double-click `Run-Jit-Disasm.cmd` from the same package. It emits `jit-disasm-block-core.txt` beside the executable, containing the target machine's JIT assembly for `CsaBlockCipher` methods reached by one verified probe. Send that text file as an attachment if possible; it is more reliable than pasting a potentially large assembly listing into the command window.

The launcher disables ReadyToRun, tiering, and tiered PGO only for this diagnostic process, so the emitted code is stable enough to inspect. It is not a benchmark and its timing output must not be compared with the normal probe. Send the complete text file together with the JSON line. The inspection focuses on scalar lookup lowering, vector-width selection, register spills, bounds checks, and calls that survived expected inlining.

## Required fields

For a typical 12th-generation Intel result, expect `architecture` to be `X64`, `avx2` and `vector256` to be `true`, and normally `vector512` to be `false`.

`block_state_update_backend` verifies the selected state-update width:

- `vector256`: AVX2-width update path; this is the first x64 optimization under test.
- `vector512`: a 512-bit update path is available.
- `vector128`: no wide state-update path is active.

`block_lookup_backend` identifies the transform-table implementation. Runs 1 and 5 report `x64-packed-ushort-lookup`; run 2 should report `scalar-ushort-table`; run 3 should report `x64-normalized-input-pointer`; run 4 should report `x64-avx512-vbmi-lookup-experimental` only when `avx512_vbmi=true`, otherwise `x64-normalized-input-pointer`. `arm64-tbl-tbx` is Arm-only.

`block_transform_output_layout` is `x64-interleaved-ushort` for runs 1 and 5 on AVX2 hosts, confirming the 128 packed lookup results are held in one temporary layout rather than two byte buffers. Runs 2–4 report `separate-byte-columns-control`.

`block_state_update_backend` distinguishes `vector128`, `vector256`, and `vector512`, so the same result set can guide both Intel and AMD decisions without assuming either vendor's ISA availability or frequency behavior.

The current package verifies every selected backend against the fixed FFdecsa checksum `76DC3CFC07B7D0F2` before timing. Use only results from this package for state-width comparisons; earlier pre-checksum forced-`Vector128` readings are superseded.

`block_core_backend` reports the retained specialized 128-lane core. The generic column-major candidate was removed after a fixed-checksum AVX2 measurement showed a large block-core regression.

Do not compare the component values with the normalized C reference harness. Compare the end-to-end median only with another probe on the same machine, and use the normal `ffdecsa-compare-v1` commands for a C-versus-C# ratio.

## Windows C reference

The Windows package also includes `Run-C-Reference.cmd`. Double-click it after the managed probe:

- The preferred offline-friendly option is a portable Zig archive: extract it so `zig.exe` is at `zig\zig.exe` beside this launcher. The launcher detects it first; Zig bundles the C runtime and linker, so it does not depend on MSVC headers or a Windows SDK.
- Otherwise it uses `clang.exe` when already installed. Otherwise it downloads Microsoft Visual C++ Build Tools from Microsoft's official CDN, installs the C++ workload and Windows 11 SDK, and configures its x64 environment. If the existing Build Tools instance lacks its CRT or Windows SDK headers, the launcher automatically runs one repair/modify pass before compiling. This one-time setup can take 10-20 minutes and may require Windows elevation.
- The Clang path compiles the bundled upstream FFdecsa source with `PARALLEL_MODE=1283`, `-O3`, and `-march=native`; the MSVC fallback uses `/O2 /GL`, link-time code generation, and a bundled compatibility header for FFdecsa's GCC-only inline attribute.
- It prints one `ffdecsa-compare-v1` JSON line, using the same packet input, batch size, timing scope, and expected checksum as the managed standard harness.

Copy the C JSON along with the managed probe JSON. The Clang `-march=native` path makes this a best-effort native C calibration for the rented CPU; it is not an identical compiler-flag comparison with the managed implementation.

### Portable Zig setup

1. Download the current Windows x86_64 Zig archive from [ziglang.org/download](https://ziglang.org/download/). Do not download the minimal executable by itself: keep the complete extracted archive because it includes Zig's C toolchain files.
2. In the extracted FFDecsaSharp benchmark folder, create `zig` and copy the *contents* of the extracted Zig directory into it. The final path must be `zig\zig.exe` next to `Run-C-Reference.cmd`.
3. Double-click `Run-C-Reference.cmd`. Its first compile line should say `portable Zig C compiler`.

The launcher uses `zig cc -O3 -march=native` for the native C calibration.
