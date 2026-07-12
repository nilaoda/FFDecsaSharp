# Migration Progress

This file records durable project progress so implementation state is not only kept in chat history.

## 2026-07-10

### Completed

- Created the .NET 10 solution: `src/FFDecsaSharp.slnx`.
- Added the core library project: `src/FFDecsaSharp`.
- Added the reserved GUI project: `src/FFDecsaSharp.Gui`.
- Added the test project: `src/FFDecsaSharp.Tests`.
- Added the reserved benchmark project: `src/FFDecsaSharp.Benchmarks`.
- Added shared build settings in `src/Directory.Build.props`.
- Added `.gitignore` for .NET build outputs and local IDE files.
- Implemented `FFDecsaSharp.CSA.ControlWord`.
- Implemented transport stream packet primitives:
  - `TransportPacket`
  - `TransportScramblingControl`
  - `AdaptationFieldControl`
- Added focused unit tests for control word parsing/copying/equality.
- Added focused unit tests for TS packet validation, header parsing, scrambling control, adaptation-field control, and payload offset calculation.

### Verification

- `dotnet test FFDecsaSharp.slnx`
  - Passed: 13
  - Failed: 0
- `dotnet build FFDecsaSharp.slnx`
  - Warnings: 0
  - Errors: 0

### Current Phase State

- Project bootstrap: complete.
- Transport Packet foundation: complete for header parsing and payload-boundary detection.
- Control Word foundation: complete for 8-byte immutable value representation.
- CI: not yet added.
- CSA algorithm, key schedule, BitSlice, and decryption core: not started.

### Next Recommended Step

Begin BitSlice foundation work:

1. Study FFdecsa normal-to-slice and slice-to-normal conversion paths.
2. Design a scalar-first C# `BitSliceBlock` representation.
3. Add differential or known-answer tests before implementing CSA core.

## 2026-07-10 Continued

### Structure Correction

- Moved all code-related files under `src/`:
  - `src/FFDecsaSharp.slnx`
  - `src/Directory.Build.props`
  - `src/FFDecsaSharp`
  - `src/FFDecsaSharp.Gui`
  - `src/FFDecsaSharp.Tests`
  - `src/FFDecsaSharp.Benchmarks`
- Root directory now keeps repository docs, reference C source, and repository-level configuration only.

### BitSlice Foundation Started

- Studied FFdecsa stream conversion points:
  - `FFTABLEIN`
  - `FFTABLEOUT`
  - `trasp64_*_88ccw`
  - `trasp64_*_88cw`
- Added internal scalar `BitSliceBlock` infrastructure for 64 bit planes across up to 64 lanes.
- Added unit tests for argument validation, plane clearing, single-bit lane mapping, decode mapping, and 64-lane roundtrip.

### Key Schedule Foundation Started

- Studied FFdecsa `key_schedule_stream` and `key_schedule_block`.
- Added internal `CsaKeySchedule` for:
  - stream nibble schedule (`iA`, `iB`)
  - block schedule (`kk[0]..kk[55]`)
- Added tests for stream nibble splitting, invalid arguments, and a block schedule known-answer case generated from the reference algorithm.
- Added internal `ScheduledControlWord` to hold a validated control word with its stream and block schedules for future Decryptor state.

### Transport Packet Writable Operations

- Studied FFdecsa packet grouping behavior around `pkt[3] & 0xc0` and `pkt[3] &= 0x3f`.
- Added static payload-offset parsing for raw packet spans.
- Added static scrambling-control parsing for raw packet spans.
- Added static scrambling-control clearing for mutable packet spans.

## 2026-07-11

### Repository Tracking

- Initialized the local Git repository on the `main` branch.
- Created the baseline commit `7f7b322` (`chore: bootstrap FFDecsaSharp migration`).
- Confirmed `.gitignore` excludes .NET build output, IDE state, test artifacts, and macOS metadata.
- No remote is configured yet; connect the chosen private GitHub or GitLab repository before the next collaborative handoff.

### Verification

- `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet build src/FFDecsaSharp.slnx --no-restore -m:1 /nr:false`
  - Warnings: 0
  - Errors: 0
- `dotnet test` could compile the test assembly, but test execution is blocked in the current sandbox because VSTest attempts to open a local socket and receives `SocketException (13): Permission denied`.

### Packet Planning Foundation

- Added internal CSA packet planning types:
  - `CsaKeyKind`
  - `CsaPacketPlanningResult`
  - `CsaPacketWorkItem`
  - `CsaPacketPlanner`
- `CsaPacketPlanner.Prepare` now classifies clear, reserved, invalid, no-payload, too-small-payload, and decryptable packets.
- For decryptable or too-small scrambled payloads, planning clears the TS scrambling-control bits to mirror FFdecsa's `pkt[3] &= 0x3f` behavior.
- Build verification after packet planner:
  - `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet build src/FFDecsaSharp.slnx --no-restore -m:1 /nr:false`
  - Warnings: 0
  - Errors: 0

### Scalar Block Cipher Foundation

- Studied FFdecsa `block_decypher_group`.
- Added internal scalar `CsaBlockCipher.TryDecipherBlock` for a single 8-byte block and a 56-byte block schedule.
- Preserved the FFdecsa block S-box and bit permutation behavior.
- Added known-answer tests, including the first `test_1_expected_block` block from the reference test data.
- Build verification after block cipher:
  - `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet build src/FFDecsaSharp.slnx --no-restore -m:1 /nr:false`
  - Warnings: 0
  - Errors: 0
- Added `CsaBlockCipher.TryDecipherBlocks` to process consecutive 8-byte blocks using the scalar single-block implementation.
- Build verification after consecutive block helper:
  - `MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet build src/FFDecsaSharp.slnx --no-restore -m:1 /nr:false`
  - Warnings: 0
  - Errors: 0

### 2026-07-11 Continued

- Restored full test execution after sandbox permissions changed:
  - `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 40
  - Failed: 0
- Investigated scalar stream cipher port:
  - Confirmed reference C first stream outputs for `test_1_key` and first encrypted block:
    - `dc 15 de f1 4a f1 f8 2c`
    - `75 c8 3a 1f bf 67 19 e1`
  - Confirmed reference initialization snapshot for lane 0:
    - `A 0 c a f d 2 f c 2 1`
    - `B f 8 1 a f 4 5 4 1 e`
    - `X 2 Y 0 Z 1 D 1 E 5 F 5 p1 q1 r0`
  - A first scalar C# draft did not match the reference state, so it was not kept in the mainline.
- Added public `ControlWords` value type for even/odd control-word pairs.
- Added unit tests for `ControlWords`.
- Verification after backing out the incomplete stream draft:
  - `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 43
  - Failed: 0
- Added internal `ScheduledControlWords` for paired even/odd scheduled keys.
- Verification after `ScheduledControlWords`:
  - `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 45
  - Failed: 0
  - `dotnet build src/FFDecsaSharp.slnx`
  - Warnings: 0
  - Errors: 0

### 2026-07-11 Stream Calibration Continued

- Re-ran stream cipher calibration against the FFdecsa C reference.
- Confirmed the first scalar draft diverges during initialization by the third internal stream step.
- Recorded durable calibration details in `docs/STREAM_CIPHER_CALIBRATION.md`.
- Kept the incomplete stream implementation out of the mainline so the repository remains buildable and testable.
- Optimized `CsaBlockCipher.TryDecipherBlocks` by splitting out a private no-revalidation block core, so consecutive block processing validates arguments once.
- Verification:
  - `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 45
  - Failed: 0
- `dotnet build src/FFDecsaSharp.slnx`
  - Warnings: 0
  - Errors: 0

### Stream Cipher And Packet Decryptor

- Completed scalar `CsaStreamCipher` with packed 10-nibble A/B registers and the seven reference S-box truth tables.
- Matched the first two FFdecsa stream output groups for `test_1_key` and its initialization block.
- Added public `Decryptor` and `PacketDecryptionResult` APIs.
- Integrated stream generation, block deciphering, and CSA chaining for in-place decryption of one 188-byte TS packet.
- Added end-to-end reference coverage for an odd-key packet prefix, plus clear and reserved scrambling-control behavior.
- The hot decryption path uses spans and stack buffers only; it performs no managed allocations.

### Verification

- `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 51
  - Failed: 0
- `dotnet build src/FFDecsaSharp.slnx`
  - Warnings: 0
  - Errors: 0

### Performance Baseline

- Added a BenchmarkDotNet executable project for the public `Decryptor.Decrypt` hot path.
- Removed redundant per-block argument checks inside the already validated packet decrypt loop and added focused inlining hints for stream-state accessors.
- Short benchmark baseline on Apple M4, .NET 10.0.8, Arm64 RyuJIT:
  - `DecryptPacket`: mean `14.74 us` per 188-byte packet.
  - Managed allocation: `0 B` per operation.
- Benchmark artifacts are excluded from source control. Run `dotnet run -c Release --project src/FFDecsaSharp.Benchmarks/FFDecsaSharp.Benchmarks.csproj -- --job short` to refresh this baseline.

### Full Packet Compatibility Coverage

- Added the complete 184-byte FFdecsa `test_2` reference packet as an end-to-end `Decryptor` regression test.
- The scalar implementation now verifies all 23 chained CSA blocks against the reference plaintext, including TS scrambling-control clearing.
- `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 53
  - Failed: 0

### Residue Compatibility Coverage

- Added the FFdecsa `test_p_1_6` reference vector for one full CSA block plus six residue bytes behind an adaptation field.
- Confirmed residue bytes are decrypted with the next stream output and the packet scrambling-control bits are cleared.

### Batch API Foundation

- Added public `Decryptor.TryDecryptPackets` for contiguous 188-byte packets with caller-provided result storage.
- The method validates the complete batch layout before modifying any packet or result and stays allocation-free.
- Added mixed clear, reserved, and scrambled batch coverage plus invalid-layout coverage.
- Batch benchmark on Apple M4, .NET 10.0.8, Arm64 RyuJIT:
  - Single packet: `14.40 us` per packet, `0 B` allocation.
  - 32-packet batch: `14.58 us` per packet, `0 B` allocation.
- The batch method currently preserves scalar per-packet execution. Its purpose is a stable API and correctness layer for a future cross-packet bit-sliced implementation.
- `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 55
  - Failed: 0

### Bitsliced Stream Acceleration

- Added a 64-lane `ulong` bit-sliced stream cipher with FFdecsa's original S-box boolean networks.
- Verified its one-lane output against FFdecsa and compared 64 independent lanes against the scalar stream implementation.
- Integrated it into `TryDecryptPackets` for contiguous runs of two to 64 full-payload packets using the same control word.
- Full packets with adaptation fields, residue bytes, mixed keys, or isolated packets retain the scalar path.
- The block cipher remains scalar; only the stream phase is bit-sliced in this stage.
- Short benchmark on Apple M4, .NET 10.0.8, Arm64 RyuJIT:
  - Single packet: `15.18 us` per packet, `0 B` allocation.
  - 32 full-packet batch: `8.45 us` per packet, `0 B` allocation.
  - 64-lane stream generation for 23 output blocks: `1.40 us` per lane, `0 B` allocation.
- `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 59
  - Failed: 0

### Block Cipher Lookup Optimization

- Added a static 256-entry transform table that combines the block S-box output with its bit permutation.
- The block decipher round now uses one transform lookup instead of a separate S-box lookup and six bit-mask shifts.
- Short benchmark on Apple M4, .NET 10.0.8, Arm64 RyuJIT:
  - Single block decipher: `268 ns` to `187 ns` per block.
  - 64-block scalar loop: `273 ns` to `186 ns` per block.
  - Single packet decrypt: `12.51 us` per packet, `0 B` allocation.
  - 32 full-packet bit-sliced batch: `6.44 us` per packet, `0 B` allocation.
- The transform table is constructed once during type initialization and is not part of the hot path.

### Reference C Performance Calibration

- Compiled the reference FFdecsa test driver with Apple Clang `-O3 -mcpu=apple-m4` on the same Apple M4 host.
- Each reference build passed its five built-in correctness vectors before timing 30,000 complete packets.
- FFdecsa throughput by portable bit-slice width:
  - 32 lane (`PARALLEL_32_INT`): `816.9 Mbit/s`, `554,939 packets/s`.
  - 64 lane (`PARALLEL_64_LONG`): `1,153.3 Mbit/s`, `783,515 packets/s`.
  - 128 lane (`PARALLEL_128_2LONG`): `2,919.7 Mbit/s`, `1,983,471 packets/s`.
- Updated the C# batch benchmark to 64 packets, matching the current `ulong` bit-slice width:
  - `6.112 us` per packet, approximately `163,612 packets/s` and `240.9 Mbit/s`, with `0 B` allocation.
- This C# benchmark includes copying each source packet into its mutable work buffer, while the C test times decryption after its source buffer is prepared; therefore the C# number is conservative but not exactly identical work.
- The remaining roughly 4.8x gap against the C 64-lane backend is now primarily the scalar per-packet block cipher and packet packing work, not the bit-sliced stream core.

### Interleaved Batch Block Cipher

- Added a scalar, cross-lane block cipher core that processes the same CSA round across every lane before advancing to the next round.
- The implementation follows FFdecsa's batch state layout while deliberately avoiding hardware SIMD intrinsics.
- Added a 64-block differential test against independent scalar deciphering and retained full packet-batch differential coverage.
- On the 64-packet benchmark, the interleaved block core reduced throughput cost from `6.112 us` to `3.485 us` per packet, with `0 B` allocation.
- This is approximately `286,944 packets/s` and `422.4 Mbit/s`; the gap to the C 64-lane reference is now roughly 2.7x.
- `dotnet test src/FFDecsaSharp.slnx`
  - Passed: 60
  - Failed: 0

### JIT Hot-Loop Tuning

- Marked the stable batch block-round loop and bit-sliced stream generator with `AggressiveOptimization`.
- The 64-packet benchmark improved from `3.485 us` to `3.395 us` per packet, approximately a 2.6% reduction, with `0 B` allocation before the deterministic-initialization correction below.
- The optimization preserves the existing scalar and bit-sliced algorithms; it only guides Tier-1 JIT optimization of their hot loops.

### Deterministic Bitsliced State Initialization

- Explicitly cleared all bit-sliced stream registers before loading the key schedule, removing reliance on stack initialization behavior for A/B tail registers and auxiliary state.
- The corrected 64-packet benchmark is `3.423 us` per packet with `0 B` allocation, a negligible difference from the pre-correction short-run sample.
- The platform-neutral optimization phase has reached diminishing returns: combined lookup, cross-lane block rounds, work-buffer reuse, and JIT tuning reduced the 64-packet path from `6.112 us` to `3.423 us` per packet.
- The next material performance opportunity is architecture-specific SIMD for the remaining cross-lane block S-box and state-update work.

### Arm64 SIMD Table-Lookup Evaluation

- Evaluated a NEON `TBL` implementation for the cross-lane block S-box transform on Apple M4.
- The 256-entry table required four 64-byte `TBL4` groups plus high-index mask selection; the prototype passed the block and packet differential tests and the full 60-test suite.
- It regressed the 64-packet benchmark to `4.514 us` per packet, versus the stable scalar interleaved baseline of `3.423 us` per packet.
- The required state gather/scatter and dual table-selection work outweighed the table-lookup benefit, so this prototype was deliberately not retained.
- Further SIMD work should target a representation that keeps block state resident in vector registers rather than vectorizing only the lookup stage.

### Complete FFdecsa Reference Vector Coverage

- Added end-to-end coverage for the remaining FFdecsa reference packet shapes:
  - the full 184-byte all-`0xFF` payload decrypted with an all-`0xFF` even control word;
  - an 80-byte, ten-block payload behind a 104-byte adaptation field.
- The test suite now covers all five packet vectors exercised by the upstream FFdecsa test driver: full odd-key payload, full even-key payload, all-`0xFF` even-key payload, ten-block adaptation-field payload, and residual-byte payload.
- `dotnet build src/FFDecsaSharp.slnx --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test src/FFDecsaSharp.slnx --no-build` passed 62 tests.

### Cross-Platform Continuous Integration

- Added a GitHub Actions workflow that restores, builds in Release configuration, and executes the full test suite on current Ubuntu, macOS, and Windows runners.
- The workflow operates from `src/`, preserving the project-file structure while keeping repository automation in GitHub's conventional `.github/workflows` location.
- It will run automatically when this local repository is connected to a GitHub remote and receives a push or pull request.

### Cross-Packet Key Grouping

- Changed `TryDecryptPackets` to collect up to 64 full-payload packets per control-word parity across the complete input span, rather than requiring a contiguous same-key run.
- The bit-sliced packet core now consumes validated packet indexes, preserving packet order in the caller buffer while treating each selected packet as an independent lane.
- A group containing one packet retains the scalar path; malformed packets, adaptation-field payloads, and residual-byte payloads retain their existing scalar handling.
- Added scalar differential coverage for alternating even/odd full-payload packets and an alternating-key batch benchmark.
- Apple M4 short-run results, all with 0 B managed allocation:
  - 64 same-key full-payload packets: `3.469 us` per packet.
  - 64 alternating-key full-payload packets: `3.955 us` per packet.
- `dotnet build src/FFDecsaSharp.slnx --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test src/FFDecsaSharp.slnx --no-build` passed 63 tests.

### Project Entry Documentation

- Added `README.md` with current implementation scope, minimal single-packet and batch API examples, local build/test commands, benchmark invocation, and repository layout.
- Documented that the library currently supports decryption only and that the GUI assembly remains reserved for future work.

### Transposed Batch Block Core

- Revisited the batch block cipher using the same state organization as FFdecsa: state is stored by byte position and lane rather than by lane and state position.
- Applied 64-bit SWAR updates to groups of eight adjacent lanes after each scalar block S-box lookup; this preserves the required table semantics while reducing state-update traffic.
- Added a 64-lane differential test and isolated benchmarks for both batch block-core layouts.
- Apple M4 short-run results, 0 B managed allocation:
  - previous lane-major interleaved block core: `75.17 ns` per block;
  - transposed column-major SWAR block core: `34.04 ns` per block.
- Integrated the faster core only into full-payload bit-sliced batches. Full test coverage remains intact:
  - same-key 64-packet batch: `2.290 us` per packet, reduced from `3.430 us`;
  - alternating-key 64-packet batch: `2.806 us` per packet, reduced from `3.955 us`.
- The remaining primary cost is the bit-sliced stream kernel at `1.359 us` per packet for 23 generated stream blocks; NativeAOT alone is not expected to close this gap.
- `dotnet build src/FFDecsaSharp.slnx --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test src/FFDecsaSharp.slnx --no-build` passed 64 tests.

### SWAR Bit-Plane Decode

- Replaced the scalar lane-by-lane bit-plane decode with an eight-lane `ulong` SWAR transpose, preserving the existing bit order and partial-lane behavior.
- Added roundtrip coverage for partial lane groups of 1, 7, 8, 9, and 63 lanes.
- Apple M4 short-run results, 0 B managed allocation:
  - bit-sliced stream generation for 23 blocks: `1.359 us` to `0.968 us` per packet;
  - same-key 64-packet batch: `2.290 us` to `1.842 us` per packet;
  - alternating-key 64-packet batch: `2.307 us` per packet.
- The same-key batch now reaches approximately 69% of the calibrated FFdecsa 64-lane C throughput. The remaining hotspot is the stream-state step itself.
- `dotnet build src/FFDecsaSharp.slnx --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test src/FFDecsaSharp.slnx --no-build` passed 69 tests.

### NativeAOT Throughput Harness

- Added `FFDecsaSharp.PerfHarness`, a dependency-free throughput harness suitable for both JIT and NativeAOT execution; it now exercises the 128-packet batch path.
- BenchmarkDotNet itself cannot be published with NativeAOT because its reflection and diagnostics dependencies are trim/AOT-incompatible; the harness avoids those dependencies.
- Apple M4 measurements, including packet-buffer copying:
  - Release JIT: `1761.1 ns` per packet;
  - default NativeAOT: `1985.3 ns` per packet;
  - NativeAOT with `OptimizationPreference=Speed`: `1833.4 ns` per packet.
- Speed-preference AOT is now the harness default. It is close to the JIT result but does not yet outperform it, so further progress remains dependent on algorithm and data-layout optimization.

### 128-Lane Vector Baseline

- Replaced the 64-bit bit planes with `Vector128<ulong>` planes, allowing one stream-cipher boolean-network invocation to process 128 packets on Arm64 AdvSIMD and x86 SIMD-capable runtimes.
- Extended the batch API, block-core temporary storage, throughput harness, isolated benchmarks, bit-plane round trips, stream differential test, and end-to-end differential test to 128 lanes. The end-to-end test compares 128 independent full-payload packets against the scalar decryptor.
- Replaced the bit-sliced stream register copies with a ten-nibble circular register head, following FFdecsa's virtual shift-register principle. This eliminates the two 36-plane state copies previously performed on every stream step.
- Replaced the circular register lookup with FFdecsa's 32-step contiguous virtual-register window. The stream hot loop now uses direct addresses and advances its ten live nibbles only between 8-byte stream blocks.
- Updated the column-major block-core state update to use 16-byte `Vector128<byte>` updates. The block S-box remains scalar by design, matching the unavoidable lookup stage in FFdecsa.
- Apple M4, .NET 10.0.8, Arm64 RyuJIT, short BenchmarkDotNet runs, all with `0 B` managed allocation:
  - 128-lane stream generation for 23 blocks: `649.6 ns` per packet;
  - 128-lane column-major block decipher: `40.67 ns` per block;
  - 128-packet copy-and-decrypt BenchmarkDotNet path: `1.583 us` per packet;
  - 128-packet fixed-iteration harness, including source-buffer copying: `1695.6 ns` per packet.
- The comparable FFdecsa `PARALLEL_128_2LONG` calibration remains `1,983,471 packets/s`, approximately `504 ns` per packet. The current end-to-end C# result is therefore about 32% of that C throughput. The benchmarks do not yet normalize packet-copying and API validation, but the gap is too large to attribute to that difference alone.
- Profiling by isolated benchmark now identifies the stream kernel and scalar block S-box lookup as the dominant remaining costs. The next optimization should retain stream-state planes in local/vector registers or generate a fully specialized 128-lane step; generic `Span<Vector128<ulong>>` accesses still impose substantial register-pressure and addressing overhead.

### Interleaved 128-Lane Packet Pipeline

- Reworked the full-payload 128-lane path to generate one stream block and immediately consume it in the corresponding block-cipher/chaining iteration, matching FFdecsa's stream/block interleaving.
- Removed the full `23 * laneCount * 8` stream-output work buffer from the hot packet path. The pipeline now retains only one decoded 8-byte stream block per active lane while preserving the public API and all scalar differential checks.
- Apple M4, .NET 10.0.8, Arm64 RyuJIT, 128-packet MediumRun comparison, all with `0 B` managed allocation:
  - prior 128-lane batch path: `1.605 us` per packet;
  - interleaved pipeline: `1.594 us` per packet.
- The fixed-iteration harness measured `1609.6 ns` per packet including source-buffer copying. The modest BenchmarkDotNet improvement is retained because it also reduces stack working-set and removes a full intermediate-buffer pass.

### Bounds-Check-Free Batch Block S-Box

- Reworked the column-major block S-box lookup to use by-reference access to the scheduled key, transposed state, transform table, and temporary output columns. This removes repeated `Span` bounds checks and address reconstruction from the 128 independent table lookups performed in every block-cipher round while retaining the existing `Vector128<byte>` state updates.
- Apple M4, .NET 10.0.8, Arm64 RyuJIT, 128-lane MediumRun, all with `0 B` managed allocation:
  - isolated column-major block decipher: `40.67 ns` to `28.59 ns` per block;
  - 128-packet copy-and-decrypt path: `1.594 us` to `1.320 us` per packet.
- This is a 29.7% reduction for the block core and a 17.2% end-to-end improvement. The remaining next target is a width-specialized `Vector256`/`Vector512` backend for AVX2/AVX-512 hosts; Apple Silicon continues to use the 128-bit AdvSIMD backend.

### Comparable C And C# Throughput Protocol

- Added `ffdecsa-compare-v1`: a shared JSON output schema for the dependency-free C# throughput harness and an independently compiled upstream FFdecsa reference harness.
- Both harnesses use the same deterministic 128-packet full-payload input, even control word, batch boundary, 5,000 warmup batches, and 30,000 measured batches. Source-buffer reset and FFdecsa's mutable cluster-list preparation remain outside the `decrypt_only` timing window.
- The output includes batch and timing metadata, throughput, allocation count, and an FNV-1a checksum of the final decrypted batch. Results should only be compared when the protocol fields and checksum match.
- Apple M4 serialized comparison after the bounds-check-free block optimization:
  - C#: `1361.559 ns` per packet, `734,452.087` packets per second;
  - FFdecsa C `PARALLEL_128_2LONG`: `502.333 ns` per packet, `1,990,713.116` packets per second;
  - both output checksums: `76DC3CFC07B7D0F2`.
- Under this normalized protocol, the managed implementation reaches about 37% of the reference C throughput. This supersedes the older comparison whose C# side included source copying and had a different measurement boundary.

### Fused Stream S-Boxes And Fixed Register Windows

- Fused the seven stream S-box boolean networks directly into the 128-lane `Step` method, removing the 14-vector `out` parameter ABI from each stream step.
- Materialized the A and B virtual-register windows once per step, so all S-box, feedback, and next-state reads use fixed offsets from that window rather than recomputing `registerOffset * 4` for every access.
- Arm64 RyuJIT disassembly confirms that dynamic address-extension instructions in `Step` decreased from 57 to 8.
- Apple M4, .NET 10.0.8, 128-lane MediumRun, all with `0 B` managed allocation:
  - isolated stream generation: `649.8 ns` to `609.8 ns` per packet;
  - 128-packet copy-and-decrypt path: `1.320 us` to `1.280 us` per packet.
- The normalized serial `ffdecsa-compare-v1` run measured C# at `1300.991 ns` per packet (`768,644.609` packets per second), versus FFdecsa C at `499.956 ns` per packet (`2,000,175.015` packets per second), with matching checksum `76DC3CFC07B7D0F2`. The managed implementation now reaches 38.4% of the reference throughput.

### Unrolled 128-Lane Block S-Box Schedule

- Added a fixed-width block S-box schedule for exactly 128 lanes. It expands the 128 independent transform-table lookups in each block round, following FFdecsa's strategy of exposing independent lookups to the compiler; other batch widths retain the compact loop.
- Apple M4, .NET 10.0.8, 128-lane MediumRun, all with `0 B` managed allocation:
  - isolated column-major block decipher: `28.59 ns` to `25.75 ns` per block;
  - 128-packet copy-and-decrypt path: `1.271 us` to `1.216 us` per packet.
- The normalized serial `ffdecsa-compare-v1` run measured C# at `1241.728 ns` per packet (`805,329.232` packets per second), versus FFdecsa C at `497.894 ns` per packet (`2,008,458.539` packets per second), with matching checksum `76DC3CFC07B7D0F2`. The managed implementation now reaches 40.1% of the reference throughput.

### Cross-Width Block State Updates

- Extended the column-major block-state XOR update with runtime-selected `Vector512<byte>`, `Vector256<byte>`, and `Vector128<byte>` paths. AVX-512 and AVX2 hosts can now update 64 or 32 packet columns per vector operation while Arm64 AdvSIMD continues to use the 16-byte path.
- Apple M4 verification confirms the unavailable wider paths are eliminated without a measurable regression: 128-lane column-major block decipher measured `25.777 ns` per block versus the prior `25.755 ns` baseline.
- The wider paths require the same `ffdecsa-compare-v1` measurement on AVX2 and AVX-512 hardware before reporting cross-platform throughput gains.

### 128-Lane Managed Hot-Path Refinement

- Added a 128-lane-only bit-plane decode path that caches the eight source planes for each byte, reads each 64-lane vector half once, and writes through validated by-reference addresses. The existing generic decoder remains responsible for partial batches.
- Tightened the stream-step ABI to use fixed by-reference state and input planes. The normal stream step now carries no span lengths or input indexing; it consumes dummy references that are eliminated by its static input-mode specialization.
- Reused column-major block S-box temporary columns across all 23 packet-pipeline iterations, and replaced repeated payload span slicing with direct validated by-reference eight-byte reads and writes.
- Removed redundant full-payload re-planning after batch classification. The grouped path now clears the scrambling-control bits immediately before bitsliced decryption, preserving the scalar fallback behavior for one-packet tails.
- A 64-bit dual-half circular-register prototype was verified against the scalar reference but measured `734 ns` per packet for 23 stream blocks, versus `514 ns` for the current `Vector128<ulong>` kernel. It was discarded; splitting the boolean network in two costs more than it saves in register pressure on Arm64.
- Apple M4, .NET 10.0.8, Arm64 RyuJIT, serialized `ffdecsa-compare-v1` runs, all with `0 B` managed allocation:
  - C#: median `1137 ns` per packet, `879,546` packets per second, `1294.7 Mbit/s` effective payload throughput;
  - FFdecsa C `PARALLEL_128_2LONG`: `505.887 ns` per packet, `1,976,724` packets per second, `2909.7 Mbit/s`;
  - both output checksums: `76DC3CFC07B7D0F2`.
- The current managed path reaches approximately 44.5% of the calibrated reference C throughput. The remaining dominant cost is the 128-lane stream boolean network plus the scalar block S-box lookup, not allocation or packet-buffer copying.
- `dotnet test src/FFDecsaSharp.slnx --no-restore -c Release -m:1` passed 73 tests.

## 2026-07-12

### Phase 1 — Stream-Loop Hygiene

- Removed the per-block `outputPlanes.Clear()` in the interleaved packet path. Every output plane is fully overwritten by the 32 stream steps of the next block, so the zero fill was pure cost.
- Cached per-lane payload bases once before the stream/block loop (`packetIndex * 188 + 4`) and replaced repeated `Slice`/`GetReference` address recomputation with fixed `Unsafe.Add` offsets for chaining, block, and stream words.
- Exposed `BitSliceBlock.Decode128` as `internal` and, for full 128-lane batches, call it directly instead of going through `TryDecode` validation on every block.
- Correctness: `dotnet test src/FFDecsaSharp.slnx --no-restore -c Release -m:1` passed 73 tests.
- Apple M4, .NET 10.0.8, Arm64 RyuJIT, serialized `ffdecsa-compare-v1` runs, all with `0 B` managed allocation and matching checksum `76DC3CFC07B7D0F2`:
  - C#: `1130.895 ns` per packet, `884,255` packets per second, `1301.6 Mbit/s` (previous median baseline `1137 ns`);
  - FFdecsa C `PARALLEL_128_2LONG`: `502.008 ns` per packet, `1,992,001` packets per second, `2932.2 Mbit/s`.
- Gain is small as expected (~0.5% e2e). The change primarily cleans addressing for Phase 2–3. Managed share of C throughput is about 44.4%.

### Phase 2 — Local-only `Step` (discarded)

- Replaced the six auxiliary `MemoryMarshal.CreateSpan` views over x/y/z/d/e/f with 24 `Vector128<ulong>` locals and inlined `UpdateFAndE`.
- Correctness: 73 tests passed, checksum matched, 0 managed allocation.
- Protocol throughput regressed: C# `1148.467 ns` per packet vs Phase 1 `1130.895 ns` (~1.6% slower). FFdecsa C was `495.570 ns` in that same pair of runs.
- Discarded. On Arm64 RyuJIT the Span form was already free enough that the extra live locals and end-of-step store-back cost more than they saved. Do not revive without a measured ABI win.

### Phase 3 — Bulk stream decode / lighter extract (discarded)

Attempted structural replacements for the per-block `Decode128` SWAR path used by the full 128-lane interleaved packet pipeline.

1. **Bulk 64×64 bit-matrix transpose** (`Transpose64x64` + `ReverseWithinBytes` per 64-lane half).
   - Correctness: 73 tests passed; `output_fnv1a64=76DC3CFC07B7D0F2`; 0 managed allocation.
   - Protocol throughput **regressed** versus the Phase 1 SWAR `Decode128` path (≈`1256 ns`/packet in the first clean pair of runs vs Phase 1 baseline ≈`1131–1240 ns` under the same noisy host window). Discarded.
2. **Fused `Decode128XorChain`** that assembled stream words and applied chaining/payload XOR without a separate `streamOutput` buffer.
   - Correctness: 73 tests, matching checksum, 0 alloc.
   - Protocol throughput also regressed (≈`1247 ns`/packet). Discarded.
3. **`Decode128Words`** writing contiguous `ulong` lane words instead of strided per-byte stores.
   - Correctness OK; no measured e2e win over SWAR `Decode128`. Discarded.

Notes:

- FFdecsa's `trasp64_128_88cw` operates on a different lane/bit packing than this managed plane layout (lane0 is MSB of the high halfword here; FFdecsa's group-major `FFTABLEIN` packing is not bit-identical). A direct port of the C transpose is not a drop-in for our planes without a layout conversion that itself costs work.
- On the current host, decode is not the remaining large structural win: the SWAR path already beats the bulk transpose prototype, so Phase 3's "largest remaining structural cut" hypothesis did not hold for this representation.
- Do not revive bulk transpose / fused decode-chain without a measured isolated decode microbenchmark win **and** protocol e2e gain.

Host noise observation during this phase: consecutive `ffdecsa-compare-v1` runs drifted (managed ≈`1230–1330 ns`, C ≈`530–557 ns`) versus earlier quieter samples (managed ≈`1131 ns`, C ≈`502 ns`). Relative prototype comparisons used paired runs; absolute ns should not be over-interpreted while the machine is thermally/noisy.

### Stream kernel specialization (`Step` / register window) — discarded

Attempted focused rewrites of the 128-lane stream boolean network after Phase 1–3.

1. **`StepFull` (full-lane NormalStep specialization)**
   - Dropped the init-input branch and replaced every `^ activeLanes` with `~` / `~x` forms for the full-128 decrypt path only.
   - Correctness: 73 tests, checksum `76DC3CFC07B7D0F2`, 0 managed allocation.
   - Paired protocol samples in a noisy host window (HEAD ≈`1240–1465 ns`, StepFull ≈`1235–1263 ns`) showed **no reliable e2e win**; earlier single-run samples were also within noise. Discarded to avoid code duplication without a measured gain.

2. **`GenerateStreamBlockFull` (32-step stream-block helper)**
   - Hoisted the 8×4 step loop into one helper that writes all 64 output planes via `Unsafe.Add` on plane refs.
   - Correctness OK; protocol sample ≈`1278 ns` with no improvement over HEAD in the same window. Discarded.

Interpretation:

- The remaining gap to FFdecsa C is still dominated by the boolean-network arithmetic itself and block S-box work, not by the small ABI/loop packaging around `Step`.
- Host thermal/noise currently swings managed protocol results by ~200 ns; only changes that beat HEAD by a clear margin across paired runs should be kept.
- Do not revive `StepFull` / stream-block helper packaging without quieter paired measurements and an isolated stream BDN win.

### Phase 4 — Column-major 128-lane specialization (kept)

Added a dedicated `DecipherBlocksColumnMajor128` fast path used when `blockCount == 128`:

- Fixed 128-lane column stride for load/store (`Unsafe` lane-major ↔ column-major packing without `blockCount` multiplies in the inner address math).
- Unrolled 8-byte column load/store per lane.
- Round-state updates use a pure Arm64-friendly `Vector128` loop over all 128 lanes (no per-round `IsHardwareAccelerated` branching for the full-batch path).
- Generic `blockCount != 128` path unchanged.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx -c Release -m:1` — 73 passed.
- Protocol checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`.

Measurement (Apple M4, .NET 10.0.8):

- Isolated BDN `DecipherBlocksColumnMajor` short job:
  - Phase 4: **22.47 ns**/block
  - HEAD baseline: **27.79 ns**/block
  - ≈ **19%** isolated block-core improvement.
- Full protocol e2e remains noise-dominated on this host (managed ~`1119–1175 ns`, C ~`534 ns` in the same window). Keep decision is based on the isolated block win plus unchanged correctness gates, not on a single noisy e2e sample.

### Bulk Encode128 init path (discarded)

Ported the inverse of the bulk 64×64 transpose as `Encode128` for full-lane `TryEncode` (once per batch init). Correctness passed (73 tests, matching protocol checksum), but:

- Encode runs only once per 128-packet batch, so even a large isolated win cannot move e2e much.
- The same bulk-transpose family already regressed as a decode replacement in Phase 3.
- Alternating protocol samples were noise-dominated and did not show a reliable keep signal.

Discarded. Prefer keeping the simple scalar init encode.

### AdvanceRegisterWindow `CopyBlock` (kept)

Replaced the two `Span.CopyTo` live-register copies in `AdvanceRegisterWindow` with `Unsafe.CopyBlockUnaligned` of the fixed 640-byte (40 × `Vector128`) live A/B banks.

Correctness: 73 tests, protocol checksum `76DC3CFC07B7D0F2`, 0 managed allocation.

Isolated BDN `GenerateBitslicedStream` short job on Apple M4:

- CopyBlock path: **568–577 ns**/packet
- Previous `Span.CopyTo` path: **633 ns**/packet (reconfirm noisy up to higher)
- Clear isolated stream-kernel packaging win; retained.

### Stream/block loop packaging (discarded)

Flattened the 8×4 step nested loop into a 32-step plane writer with fixed `Unsafe.Add` stores and a full-128 chaining loop branch. Correctness passed, but alternating protocol pairs were noise-dominated and did not show a reliable e2e win over the CopyBlock baseline. Discarded.

### NativeAOT PerfHarness check

Published `FFDecsaSharp.PerfHarness` with `PublishAot=true` / `OptimizationPreference=Speed` for `osx-arm64`. Protocol samples (~`1123–1140 ns`) did not beat the current RyuJIT path in the same window (~`1052–1175 ns`). No AOT-only algorithm change was required; keep measuring the RyuJIT build for the scoreboard.

### Pre-expanded stream schedule planes (discarded)

Precomputed full-lane all-ones/zero `Vector128` stream nibble planes on `ScheduledControlWord` and bulk-copied them into the A/B register window for 128-lane batches. Correctness passed, but the work is only once per batch and alternating protocol samples showed no reliable e2e gain. Discarded.

### Optimized scalar TryEncode (discarded)

Unrolled per-byte bit tests and precomputed lane masks for `TryEncode`. Correctness passed; protocol pairs were noise-dominated with no keep-grade gain (encode is once per batch). Discarded.

### Stream kernel body rewrite (Step boolean network / register window) — discarded

Revisited the 128-lane `Step` boolean network and virtual-register window after Phase 1/4 and the earlier packaging discards. Goal: structural stream-kernel work, not ABI packaging.

Prototypes measured against HEAD with isolated BDN `GenerateBitslicedStream` (short job) and/or paired A/B runs on Apple M4:

1. **Full-local auxiliary state + FFdecsa S-box-first order + explicit `StepNormal`/`StepInit`**
   - Hoisted x/y/z/d/e/f into 24 outer `Vector128` locals, removed CreateSpan, evaluated S-boxes into temps before feedback, committed X/Y/Z/p/q at step end (FFdecsa order).
   - Correctness: 73 tests green.
   - Isolated stream BDN: **~670 ns**/packet vs HEAD CopyBlock baseline **~568–577 ns**. Clear regression. Discarded.

2. **S-box-first reorder only (kept Span x/y/z/d/e/f ABI)**
   - Same FFdecsa evaluation order with temps; sliding plane cursor; inlined F/E adder.
   - Correctness OK.
   - Isolated stream BDN: **~602 ns**. Still slower than HEAD. Discarded.

3. **Sliding plane cursor + inlined F/E + original S-box write order**
   - Replaced `VectorWindow`/`Get` with `Unsafe.Add` plane bases; wrote next A/B via `Unsafe.Subtract(window, 4)`.
   - Correctness OK.
   - Isolated stream BDN: **~632 ns**. Discarded.

4. **Minimal indirection removal (final candidate)**
   - Only replaced `VectorWindow`/`Get` with direct `Unsafe.Add`, inlined `UpdateFAndE`, wrote next A/B via plane refs. **Same evaluation order as HEAD.**
   - Correctness: 73 tests green.
   - Paired short-job BDN (3 pairs, HEAD then candidate):
     - HEAD: 560.7 / 570.2 / 571.9 ns (mean ≈ **567.6 ns**)
     - Candidate: 570.4 / 566.2 / 571.4 ns (mean ≈ **569.3 ns**)
   - No reliable isolated stream win (within noise / slightly worse). Discarded.

Interpretation:

- RyuJIT already lowers the current Span + `VectorWindow` form well on Arm64 AdvSIMD. Extra live locals, S-box reordering into more temps, or more aggressive plane-cursor packaging either regresses or stays inside noise.
- The remaining gap to FFdecsa C is still the raw boolean-network op count (7 S-boxes × 32 steps × 23 blocks) plus block S-box work, not the small C# addressing wrappers around `Step`.
- Do not revive S-box-first / full-local Step rewrites without a quieter multi-pair isolated stream BDN win **and** protocol e2e gain. Prefer new structural ideas (layout/algorithm), not more Step packaging.

### Full-run virtual register history (no AdvanceRegisterWindow) — discarded

Sized the A/B virtual-register banks for the entire init+stream run (`historyLength = 32 * (1 + blockCount)`, full-payload ≈ 736 nibbles × 4 planes) so `registerOffset` could walk backward continuously and the per-block 640 B × 2 live-register copy (`AdvanceRegisterWindow` / `CopyBlock`) could be removed.

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` short-job paired sample (pair 1, quiet enough for a keep/reject signal):

- HEAD (CopyBlock every 32 steps): **565.1 ns**
- Full-history candidate: **649.1 ns** (~15% slower)

Discarded after the first pair because the regression is larger than host noise. Larger stack-resident A/B banks (tens of KB per bank) hurt cache locality more than the eliminated mid-run copies save. Keep the 32-step window + `CopyBlock` live-register advance.

### Compact loop for PopulateTransformOutputs128 — discarded

Replaced the fully unrolled 128-lane block S-box/transform populate with a tight `for (lane = 0; lane < 128; lane++)` loop, hypothesizing I-cache pressure from the giant unroll.

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` short-job pair 1:

- HEAD (fully unrolled populate): **22.87 ns**/block
- Compact loop: **38.31 ns**/block (~68% slower)

Clear regression. Keep the unrolled populate. On this path the JIT-friendly unroll still wins over a compact loop.

### Word-wise column-major load/store for 128-lane block path — discarded

Replaced the eight scalar byte loads/stores per lane in `DecipherBlocksColumnMajor128` with one `ulong` word read/write plus shift packing/unpacking.

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` short-job, 3 paired runs:

- HEAD: 22.42 / 22.27 / 22.53 ns (mean ≈ **22.41 ns**)
- Word pack/unpack: 23.05 / 22.56 / 22.39 ns (mean ≈ **22.67 ns**)

No reliable win (slightly slower on average). Keep the direct per-byte scatter/gather form.

### Decode ReverseBits table (kept)

Replaced the SWAR 3-step `ReverseBits(byte)` helper used by `Decode128` / `TryDecode` with a 256-entry `ReadOnlySpan<byte>` lookup table.

Rationale: each stream block decode performs many bit-reversals while unpacking 128-lane bitplanes to lane-major bytes. Table lookup removes the repeated mask/shift sequence on that hot path.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx -c Release -m:1` — 73 passed.
- Protocol checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`, `verified=true`.

Measurement (Apple M4, .NET 10.0.8):

- Isolated BDN `GenerateBitslicedStream` short job, 3 paired HEAD/candidate runs:
  - HEAD SWAR ReverseBits: 570.3 / 593.3 / 576.6 ns (mean ≈ **580.1 ns**)
  - ReverseBits table: 257.7 / 258.4 / 256.5 ns (mean ≈ **257.5 ns**)
  - ≈ **55%** isolated stream-path improvement (includes Decode128 over 23 blocks).
- Protocol `ffdecsa-compare-v1` after the change (3 C# samples + 1 C):
  - C#: **840.2 / 835.2 / 886.0 ns** per packet (best ≈ **835 ns**)
  - FFdecsa C `PARALLEL_128_2LONG`: **533.5 ns**
  - Managed share of C throughput ≈ **60–64%** in this window (previous best quiet samples were roughly mid-40%s / ~1050–1130 ns).

Keep. Decode bit-reversal is a real residual cost; the table is 256 B and allocation-free.

### Decode128 packaging after ReverseBits table (kept)

Tightened the 128-lane decode path on top of the ReverseBits table:

- Cache a `ref byte` into `ReverseBitsTable` and index with `Unsafe.Add` instead of re-entering the Span indexer helper.
- Split each 128-lane plane into low/high 64-lane halves once per output byte.
- Factor the 8-group unpack into `Decode128Half` with denser lane-store addressing (`baseOffset + n * BytesPerLane`).

Correctness: 73 tests, protocol checksum `76DC3CFC07B7D0F2`, 0 managed allocation.

Measurement (Apple M4, .NET 10.0.8):

- Isolated BDN `GenerateBitslicedStream` short job, 3 paired runs vs ReverseBits-table HEAD:
  - HEAD: 261.2 / 256.9 / 265.2 ns (mean ≈ **261.1 ns**)
  - Decode128 packaging: 240.3 / 241.0 / 240.7 ns (mean ≈ **240.7 ns**)
  - ≈ **7.8%** isolated stream-path improvement on top of the table win.
- Protocol `ffdecsa-compare-v1` (3 C# + 1 C):
  - C#: **825.2 / 835.7 / 824.8 ns** per packet
  - FFdecsa C: **534.9 ns**
  - Managed share of C ≈ **64–65%** in this window.

Keep as a small structural packaging win on the remaining decode residual.

### Decode128 SWAR reverse-within-bytes instead of table lookups — discarded

After the ReverseBits table + Decode128 packaging wins, tried replacing the 16 table lookups per 8-lane group with:

`ReverseBitsWithinBytes(Transpose8By8(ReverseBitsWithinBytes(packed)))`

Correctness: 73 tests green.

Isolated BDN pair 1 vs packaging HEAD:

- HEAD table path: **241.2 ns**
- SWAR reverse-within-bytes: **302.5 ns** (~25% slower)

Discarded. On this host the 256 B reverse table stays hotter than two full-width SWAR reverse stages around the transpose.

### Decode128 word accumulation then bulk store — discarded

Rebuilt `Decode128` to accumulate each lane's 8-byte word in a `stackalloc ulong[128]` buffer (OR-in reversed bytes by shift) and finish with 128 contiguous `WriteUnaligned` stores, avoiding strided single-byte stores into lane-major output.

Correctness: 73 tests green.

Isolated BDN pair 1 vs packaging HEAD:

- HEAD strided byte stores: **240.7 ns**
- Word accumulation: **307.4 ns** (~28% slower)

Discarded. The extra 1 KB stack buffer, clear, and RMW ORs outweigh the strided-store cost on this path. Keep the direct per-byte lane stores.

### Pre-expanded block transform schedule (roundKey^state) — discarded

Candidate left dirty from prior session: build `ushort[56*256]` `ExpandedBlockTransforms` at schedule time and replace hot-path `roundKey ^ state` lookups with preexpanded tables for 128-lane `DecipherBlocksColumnMajor` / full-payload path.

Correctness was not re-run in this session before discard; isolated block BDN short-job pairs from the prior handoff were noise-level (~0.7%: HEAD ≈ 22.62 ns vs candidate ≈ 22.45 ns). No keep-grade e2e evidence. Restored HEAD (`b3774e3`) without committing.

Interpretation: schedule-time expansion removes a cheap XOR before a table load but does not move the dominant scalar S-box / column-major residual. Keep the live `roundKey ^ state` transform populate.

### 128-lane StepFull rewrite (activeLanes → NOT + full-local ABI) — discarded

Revisited the stream kernel body after decode packaging wins. Goal: structural Step/register-window work on the 128-lane hot path.

Prototypes (both correctness-green: 73 tests):

1. **`StepFull` with `activeLanes` all-ones rewritten to `~`**, same evaluation order as HEAD, still Span/VectorWindow auxiliary state.
2. **`StepFull` full-local x/y/z/d/e/f ABI + direct `Unsafe.Add` A/B window + inlined F/E adder + `~` for all-ones**, same evaluation order (not S-box-first).

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 paired HEAD/candidate runs (Apple M4, .NET 10.0.8):

| Variant | pair1 | pair2 | pair3 | mean |
|--------|------:|------:|------:|-----:|
| HEAD | 243.8 / 245.1 | 239.8 / 258.2 | 243.7 / 243.5 | ≈ **243–249 ns** |
| NOT-only StepFull | 239.3 | 244.6 | 240.4 | ≈ **241.4 ns** (~2–5% isolated) |
| Full-local StepFull | 236.2 | 234.3 | 236.0 | ≈ **235.5 ns** (~3% isolated vs paired HEAD ≈ 242.4 ns) |

Paired protocol `ffdecsa-compare-v1` for full-local StepFull (3 pairs HEAD then candidate + 1 C):

- HEAD: 820.8 / 833.6 / 819.3 ns (mean ≈ **824.6 ns**)
- Candidate: 841.2 / 823.1 / 844.1 ns (mean ≈ **836.1 ns**)
- FFdecsa C: **536.9 ns**
- Checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`, `verified=true` on all samples.

No reliable e2e win (candidate slightly slower on average; pair deltas within host noise and not consistently negative). Isolated stream wins are small and do not survive the full interleaved pipeline.

Discarded and restored HEAD. Do not revive more Step packaging / full-local / activeLanes-NOT specializations without a multi-pair protocol gain that exceeds host noise. Prefer genuinely new structural avenues (algorithm/layout), not another Step ABI reshape.

### Double register history (64-step window / half AdvanceRegisterWindow) — discarded

Increased `RegisterHistoryLength` from 32 to 64 steps (`StepsPerBlock * 2`) and only called `AdvanceRegisterWindow` when `registerOffset == 0`, so live A/B walk two stream blocks before the 640 B × 2 `CopyBlock` refresh. Stack banks grow from ~2.7 KB to ~4.7 KB per A/B side.

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 paired runs (Apple M4, .NET 10.0.8):

- pair1: HEAD **239.9 ns** / CAND **254.6 ns** (+6.1%)
- pair2: HEAD **250.4 ns** / CAND **241.2 ns** (-3.7%)
- pair3: HEAD **244.2 ns** / CAND **243.6 ns** (-0.2%)
- means: HEAD ≈ **244.8 ns**, CAND ≈ **246.5 ns**

No reliable multi-pair win (pair1 regresses; overall mean slightly worse). Discarded after isolation. Same locality lesson as full-run history: larger virtual-register banks hurt more than fewer mid-run copies save. Keep the 32-step window + per-block `CopyBlock`.

### Monomorphic 128-lane full-payload path (kept)

Specialized the protocol hot path with `TryDecryptFullPayloads128`:

- `TryDecryptFullPayloads` routes `packetCount == 128` into the monomorphic method.
- `const int packetCount = MaxLaneCount` for fixed stack sizes and loop bounds.
- `activeLanes = Vector128.Create(ulong.MaxValue, ulong.MaxValue)` (no mask helper).
- Always call `BitSliceBlock.Decode128` (no partial-lane decode branch).
- Partial-width batches keep the original flexible path.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx -c Release -m:1` — 73 passed.
- Protocol checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`, `verified=true`.

Measurement (Apple M4, .NET 10.0.8), paired protocol `ffdecsa-compare-v1` (HEAD then candidate × 3 + 1 C):

- pair1: HEAD **824.2 ns** / CAND **811.1 ns** (−1.6%)
- pair2: HEAD **827.6 ns** / CAND **813.1 ns** (−1.8%)
- pair3: HEAD **838.2 ns** / CAND **815.7 ns** (−2.7%)
- means: HEAD ≈ **830.0 ns**, CAND ≈ **813.3 ns** (~2.0% e2e)
- FFdecsa C: **538.9 ns**
- Managed share of C ≈ **66%** in this window (best candidate sample ≈ **811 ns**).

Keep. This is a structural monomorphization win on the full interleaved path rather than another Step boolean-network packaging rewrite. Remaining gap is still dominated by raw 128-lane stream boolean ops and scalar block S-box work.

### Direct DecipherBlocksColumnMajor128 call from monomorphic path — discarded

Exposed `DecipherBlocksColumnMajor128` as `internal` and called it directly from `TryDecryptFullPayloads128` instead of going through the `DecipherBlocksColumnMajor` width dispatcher.

Correctness: 73 tests green.

Paired protocol (3 pairs HEAD then candidate + 1 C):

- pair1: HEAD **816.5 ns** / CAND **898.3 ns** (+10.0%)
- pair2: HEAD **868.6 ns** / CAND **830.6 ns** (-4.4%)
- pair3: HEAD **823.3 ns** / CAND **832.0 ns** (+1.1%)
- means: HEAD ≈ **836.1 ns**, CAND ≈ **853.6 ns**
- FFdecsa C: **532.2 ns**

No reliable win (host-noise dominated; pair1 regresses hard). The public dispatcher already branches to the 128-lane body for MaxLaneCount, so removing that branch is not a keep-grade structural cut. Restored HEAD.

### Monomorphic-path StepFull128 (activeLanes → NOT only) — discarded

Added `StepFull128` used only by `TryDecryptFullPayloads128`, keeping HEAD evaluation order and Span/`VectorWindow` ABI while rewriting all-ones `activeLanes` XORs to bitwise `~`.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Paired protocol (3 pairs HEAD then candidate + 1 C):

- pair1: HEAD **809.5 ns** / CAND **807.0 ns** (-0.31%)
- pair2: HEAD **814.9 ns** / CAND **805.4 ns** (-1.16%)
- pair3: HEAD **819.2 ns** / CAND **819.9 ns** (+0.09%)
- means: HEAD ≈ **814.5 ns**, CAND ≈ **810.8 ns**
- FFdecsa C: **533.5 ns**

No keep-grade e2e win (sub-1% mean, pair3 slightly slower). Confirms earlier Step packaging discards: all-ones mask folding alone is not enough once the monomorphic full-payload path is already in place. Restored HEAD.

### FFdecsa trasp_N_8 / trasp_8_N block load/store — discarded

Replaced the scalar per-lane column-major load/store in `DecipherBlocksColumnMajor128` with FFdecsa-style bulk integer transpose (`trasp_N_8` / `trasp_8_N` for GROUP_PARALLELISM=128).

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` ShortRun paired samples:

- pair1: HEAD **22.29 ns** / CAND **23.63 ns** (+6.0%)
- pair2: HEAD **22.28 ns** / CAND **23.45 ns** (+5.3%)

No keep-grade win; available pairs are at best noise and pair1 already regresses versus HEAD scalar scatter/gather. On this host the existing fixed-stride per-lane load/store remains hotter than the bulk int transpose stages. Restored HEAD.

### Fully unrolled 128-lane Vector128 block state updates (kept)

Unrolled the Arm64 `for (updateIndex = 0; updateIndex < 128; updateIndex += 16)` state-update loop in `DecipherBlocksColumnMajor128` into eight constant-offset `Vector128<byte>` update blocks. The 128-lane S-box populate path was already fully unrolled; this removes the residual 8-iteration loop around the per-round column XORs.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx -c Release -m:1` — 73 passed.
- Protocol checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`, `verified=true`.

Measurement (Apple M4, .NET 10.0.8):

Isolated BDN `DecipherBlocksColumnMajor` ShortRun, 3 paired runs:

- pair1: HEAD **23.01 ns** / CAND **22.28 ns** (-3.17%)
- pair2: HEAD **23.21 ns** / CAND **22.09 ns** (-4.83%)
- pair3: HEAD **23.11 ns** / CAND **22.09 ns** (-4.41%)
- means: HEAD ≈ **23.11 ns**, CAND ≈ **22.15 ns** (~-4.1% isolated block)

Paired protocol `ffdecsa-compare-v1` (HEAD then candidate × 3 + 1 C; host noisy this window):

- pair1: HEAD **978.1 ns** / CAND **846.8 ns** (-13.4%) — HEAD sample is a thermal/noise outlier
- pair2: HEAD **819.4 ns** / CAND **808.9 ns** (-1.3%)
- pair3: HEAD **866.0 ns** / CAND **815.6 ns** (-5.8%)
- FFdecsa C: **550.0 ns**
- Candidate-only confirm samples after keep: **834.6 / 926.3 / 824.6 ns** (best ≈ **824.6 ns**)

Keep. Isolated block signal is clean and multi-pair; e2e direction matches though absolute protocol numbers remain host-noise sensitive. Remaining gap to C is still dominated by the 128-lane stream boolean network and scalar block S-box table lookups.

### Sliding live-state base for block rounds (liveslide) — discarded

Uncommitted candidate on `DecipherBlocksColumnMajor128`: walk a `liveState` ref backward each round (FFdecsa-style `roff`) instead of recomputing `offset * ColumnStride` for S-box populate and unrolled Vector128 state updates.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Isolated BDN `DecipherBlocksColumnMajor` ShortRun (3 pairs HEAD then candidate):

- pair1: HEAD **22.52 ns** / CAND **21.81 ns** (-3.1%)
- pair2: HEAD **22.28 ns** / CAND **21.90 ns** (-1.7%)
- pair3: HEAD **22.13 ns** / CAND **23.90 ns** (+8.0%)
- means: HEAD ≈ **22.31 ns**, CAND ≈ **22.54 ns**

No reliable multi-pair block win (pair3 regresses hard; mean slightly worse). Protocol samples under the candidate were noise-level versus recent HEAD (~802–814 ns). Restored HEAD (`9eed197`). Keep the absolute-offset unrolled Vector128 updates without the sliding live base.

### 4-step stream kernel fusion (StepQuad locals) — discarded

Specialized `StepQuadInit` / `StepQuadNormal` running four boolean-network steps per call with x/y/z/d/e/f kept as outer `Vector128` locals across the quad (no per-step Span store-back). Wired into `TryGenerateBlocks` and `TryDecryptFullPayloads128` only.

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` ShortRun pair 1:

- HEAD **243.4 ns** / CAND **266.7 ns** (~+9.5%)

Clear regression. Restored HEAD. Crossing the step boundary with a giant fused method hurts more than it saves on Arm64 RyuJIT.

### 4-step call unroll (same Step ABI) — discarded

Unrolled the `for (step = 0; step < 4; step++)` loops in `TryGenerateBlocks` / `TryDecryptFullPayloads128` into four straight-line `Step<>` calls without changing the Step body.

Correctness: 73 tests green.

Isolated BDN pair 1: HEAD **239.4 ns** / CAND **240.8 ns** (noise / slightly worse). Restored HEAD. Loop overhead around Step is not the bottleneck.

### Full-lane StepFull128 with complement/AndNot shapes — discarded

Added `StepFull128` for the 128-lane normal path (`TryDecryptFullPayloads128` + full-lane `TryGenerateBlocks`), folding all-ones `activeLanes` XORs into `~` / complement forms while keeping Span/`VectorWindow` ABI and HEAD evaluation order.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 pairs:

- pair1: HEAD **241.03 ns** / CAND **239.45 ns** (-0.66%)
- pair2: HEAD **247.09 ns** / CAND **245.83 ns** (-0.51%)
- pair3: HEAD **241.31 ns** / CAND **239.63 ns** (-0.70%)
- means: HEAD ≈ **243.15 ns**, CAND ≈ **241.64 ns** (~-0.62% isolated)

Paired protocol `ffdecsa-compare-v1` (3 pairs + 1 C):

- pair1: HEAD **809.6 ns** / CAND **802.5 ns** (-0.88%)
- pair2: HEAD **851.3 ns** / CAND **807.5 ns** (-5.14%) — HEAD sample is a noise outlier
- pair3: HEAD **819.4 ns** / CAND **805.9 ns** (-1.65%)
- means: HEAD ≈ **826.8 ns**, CAND ≈ **805.3 ns**
- FFdecsa C: **536.3 ns**

Directionally slightly faster but isolated gain is sub-1% and e2e is host-noise dominated (same class as prior monomorphic `activeLanes→NOT` discards). Restored HEAD. Do not revive more complement-only Step specializations without a clearer multi-pair isolated stream win.

### Dual-step stream kernel with cached A/B window shift — discarded

Added `StepDualNormal`: load the live 10×4 A/B window into locals once, run two boolean-network steps, and shift the cached locals between the halves instead of reloading from the virtual register window for the second step. Wired into `TryGenerateBlocks` / `TryDecryptFullPayloads128` normal loops (2 duals per byte).

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` ShortRun pair 1:

- HEAD **276.7 ns** (noisy absolute; still the unfused path)
- CAND **441.3 ns** (~+60%)

Clear regression. Restored HEAD. Keeping ~80 live plane locals across a dual-step blows Arm64 register pressure / spill cost far beyond any saved window reloads.

### S-box A-plane preload CSE in Step — discarded

Preloaded every A-window cell touched by the seven stream S-boxes into named locals once per `Step`, then reused those locals in the existing evaluation order (no dual-step, no ABI change).

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` ShortRun pair 1:

- HEAD **240.9 ns** / CAND **251.1 ns** (+4.2%)

Regression on the first pair; stopped further pairs. Restored HEAD. Explicitly naming ~35 live A planes increases spill pressure without reducing the boolean-network work the JIT already schedules from `VectorWindow.Get`.

### Stream-then-block reordering of monomorphic full-payload path — discarded

Restructured `TryDecryptFullPayloads128` so all 22 stream blocks (Step + Decode128 into a 22,528 B buffer) run first, then the block-cipher/chaining loop consumes the precomputed stream words. Stream state is independent of the block cipher, so this is a pure schedule/layout change aimed at better I-cache / locality for each phase.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Paired protocol `ffdecsa-compare-v1` (3 pairs HEAD then candidate):

- pair1: HEAD **814.5 ns** / CAND **893.1 ns** (+9.6%)
- pair2: HEAD **809.2 ns** / CAND **812.1 ns** (+0.4%)
- pair3: HEAD **805.6 ns** / CAND **801.7 ns** (-0.5%)
- means: HEAD ≈ **809.8 ns**, CAND ≈ **835.6 ns**

No keep-grade win (pair1 regresses hard; mean worse). The interleaved stream/block schedule remains hotter than phase-separated buffers on this host. Restored HEAD.

### Skip final AdvanceRegisterWindow after last stream block — discarded

Avoided the last per-block `AdvanceRegisterWindow` (640 B × 2 `CopyBlock`) in `TryGenerateBlocks` / `TryDecryptFullPayloads` / `TryDecryptFullPayloads128` when no further stream steps remain.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Paired protocol (3 pairs):

- pair1: HEAD **803.6 ns** / CAND **827.5 ns** (+3.0%)
- pair2: HEAD **801.9 ns** / CAND **795.0 ns** (-0.9%)
- pair3: HEAD **821.3 ns** / CAND **824.3 ns** (+0.4%)
- means: HEAD ≈ **808.9 ns**, CAND ≈ **815.6 ns** (+0.8%)

No keep-grade win (one dead copy per packet is noise-level vs host variation). Restored HEAD.

### Block-before-stream iteration order (FFdecsa schedule) — discarded

Reordered the monomorphic full-payload loop to match FFdecsa's `decrypt_packets` iteration: `DecipherBlocksColumnMajor` on the current chaining/IB first, then 32 stream `Step`s + `Decode128`, then chaining/payload XOR. Block and stream are independent given the current chaining and stream registers.

Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, 0 alloc.

Paired protocol (3 pairs):

- pair1: HEAD **853.4 ns** / CAND **860.2 ns** (+0.8%)
- pair2: HEAD **811.3 ns** / CAND **832.4 ns** (+2.6%)
- pair3: HEAD **811.2 ns** / CAND **796.4 ns** (-1.8%)
- means: HEAD ≈ **825.3 ns**, CAND ≈ **829.7 ns** (+0.5%)

No keep-grade win. Restored HEAD. The existing stream-then-block interleave remains competitive with C's block-then-stream schedule on this managed path.

### Extract stream S-boxes into EvaluateStreamSboxes helper — discarded

Moved the seven fused stream S-box boolean networks out of `Step` into an `AggressiveInlining` `EvaluateStreamSboxes` helper (same evaluation order / `VectorWindow` loads) to test whether a tighter post-feedback live-range boundary helps Arm64 RyuJIT.

Correctness: 73 tests green.

Isolated BDN `GenerateBitslicedStream` ShortRun pairs:

- pair1: HEAD **244.3 ns** / CAND **243.0 ns** (-0.55%)
- pair2: HEAD **244.4 ns** / CAND **257.6 ns** (+5.4%)

No reliable multi-pair win (pair2 regresses). Restored HEAD. Packaging the S-box block as a separate method does not beat the fully fused `Step` body.

## Stream kernel rework session summary (2026-07-12)

Attempted structural rewrites focused on the 128-lane stream boolean network / register window after HEAD `9eed197`:

Discarded (no keep-grade multi-pair win):

1. Block liveslide virtual base (`liveState` roff) on unrolled Vector128 updates
2. 4-step fused `StepQuad` with outer x/y/z/d/e/f locals
3. 4-step straight-line call unroll
4. Full-lane `StepFull128` complement/`~` shapes
5. Dual-step cached A/B window shift (`StepDualNormal`)
6. S-box A-plane preload CSE
7. Stream-then-block full-payload reordering (all stream blocks first)
8. Skip final `AdvanceRegisterWindow`
9. Block-before-stream FFdecsa iteration order
10. `EvaluateStreamSboxes` helper extraction

Interpretation:

- RyuJIT already lowers the current fused `Step` + 32-step virtual register window well.
- Remaining C gap (~1.5×; managed often ~800–830 ns vs C ~530–550 ns on this host) is still dominated by raw 128-lane boolean-network arithmetic (7 S-boxes × 32 steps × 23 blocks) and scalar block S-box table lookups, not Step ABI / loop packaging / register-window bookkeeping.
- Further packaging-only stream rewrites are exhausted for this phase. Next real avenues need either:
  - a lower-op boolean network (new synthesis, not just `activeLanes→NOT`),
  - a different plane/register representation that cuts memory traffic without exploding live ranges,
  - or ISA/backend help beyond pure managed AdvSIMD `Vector128<ulong>` (out of current pure-managed scoreboard scope unless measured free).

Current HEAD remains `9eed197`. Working tree code restored; this section records the discarded experiments.

### Split BlockTransform ushort into separate S-box/permutation byte tables — discarded

Replaced the packed `ushort` transform table (`sbox<<8 | perm`) with direct `BlockSBox` + a separate 256-entry permutation table, removing the per-lane `>> 8` in `PopulateTransformOutputs128` and scalar block paths.

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` ShortRun pair 1:

- HEAD **22.75 ns** / CAND **25.02 ns** (+10.0%)

Clear regression on the first pair; stopped further pairs. Restored HEAD. A single ushort load that yields both outputs remains hotter than two independent byte-table loads per lane on this path.

### 16-lane chunked populate+update fusion — discarded

Fused each block round into eight 16-lane chunks: `PopulateTransformOutputs16` immediately followed by the matching `Vector128` state-update block, hypothesizing better L1 locality on the 256 B sbox/perm temps.

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` ShortRun pair 1:

- HEAD **23.27 ns** / CAND **25.83 ns** (+11.0%)

Clear regression; stopped further pairs. Restored HEAD. Full-width populate then full-width vector updates remains hotter than interleaved 16-lane chunks (temps already L1-resident; extra call/loop structure hurts).

### 16-column ring state for 128-lane block path — discarded

Replaced the 64-column virtual block history (`packetCount*64` bytes) in `DecipherBlocksColumnMajor128` with a 16-slot modular column ring (2 KB) that only keeps the live 9-wide window plus spare slots. Absolute offsets became `(base+k) & 15`.

Correctness: 73 tests green.

Isolated BDN `DecipherBlocksColumnMajor` ShortRun pair 1:

- HEAD **22.18 ns** / CAND **25.22 ns** (+13.7%)

Clear regression; stopped further pairs. Restored HEAD. Contiguous linear virtual history with simple `offset--` addressing stays hotter than modular ring indexing on Arm64 (masking/non-contiguous column bases hurt the unrolled Vector128 updates more than the smaller working set helps).


### Lower-op stream S-box boolean resynthesis (AndNot-portable) — discarded

Re-synthesized the 4-input stream S-box temporary functions with a logic.c-style multi-level search that adds portable `AndNot` (`x & ~y`) as a 1-cost primitive (maps to Arm64 `BIC` and x86 `ANDN`/`andn` via `Vector128.AndNot`). Free `OrNot` was measured separately but rejected as the primary cost model because it is not single-op on x86.

AndNot-only improvements found vs FFdecsa/current levels (sum of 26 non-trivial tmp nets: old 143 → new 138, Δ−5):

- s1 tmp3: 5→4 — `(fa & ~((fb & ~fd) ^ fc)) ^ fd`
- s4 tmp1: 6→5 — `(fb & ~fa) ^ (((fa | fc) & fd) ^ fc)`
- s4 tmp2: 7→6 — `(fb | (fc & ~fd)) ^ (fa | ((fb ^ fd) & ~fc))`
- s6 tmp0: 6→5 — `(fb & (fa | fd)) ^ (fc & ~(fa & fd))`

Patched only those four expressions into the fused 128-lane `Step` boolean network (same evaluation order / register window / Span ABI). Correctness: 73 tests green; protocol checksum `76DC3CFC07B7D0F2`, `managed_allocated_bytes=0`.

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 alternating HEAD/CAND pairs:

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 288.8 ns | 260.5 ns |
| 2 | 267.2 ns | 282.3 ns |
| 3 | 289.9 ns | 274.5 ns |
| mean | ≈ **282.0 ns** | ≈ **272.4 ns** (~3% directionally)

Protocol `ffdecsa-compare-v1` 3 alternating pairs (same host window; C ref **644.7 ns**):

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 999.9 ns | 1001.2 ns |
| 2 | 1036.9 ns | 978.1 ns |
| 3 | 978.5 ns | 996.8 ns |
| mean | ≈ **1005.1 ns** | ≈ **992.0 ns** |

No keep-grade multi-pair win: isolated stream is mixed (pair 2 regresses) and e2e stays inside host noise (~±30–50 ns). Restored HEAD. A ~3% theoretical gate cut on a subset of S-box tmps is real but too small / too scheduling-sensitive to show up cleanly on this managed path.

Interpretation:

- Portable lower-op rewrites of individual FFdecsa tmp nets are near saturation; free-`OrNot` synthesis can claim ~Δ−13 levels but is not portable to x86 as single ops.
- Do not keep partial AndNot rewrites without a clear multi-pair isolated **and** e2e win.
- Next stream-kernel avenues still need either joint multi-output (shared-DAG across a whole 5→2 S-box) synthesis with a larger measured cut, or a different plane/register representation — not more packaging.

### Current HEAD reassessment and next research target

Re-read the current implementation state after the retained/discarded 2026-07-12 experiments and refreshed the local measurements on Apple M4 / .NET 10.0.8 / Arm64 RyuJIT.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx --no-restore -c Release -m:1` — 73 passed.

Protocol throughput (`ffdecsa-compare-v1`, checksum `76DC3CFC07B7D0F2`, decrypt-only, 128 packets, 0 managed allocation):

- C#: `805.851 ns` per packet, `1,240,924 packets/s`, `1,826.640 Mbit/s`.
- FFdecsa C `PARALLEL_128_2LONG`: `543.804 ns` per packet, `1,838,897 packets/s`, `2,706.857 Mbit/s`.
- Managed throughput is about `67.5%` of the calibrated C reference in this host window.

Isolated short BenchmarkDotNet readings:

- `GenerateBitslicedStream`: `226.0 ns` per packet.
- `DecipherBlocksColumnMajor`: `20.73 ns` per block.

Research notes:

- A naive exact BFS over complete 5-input S-box output bits with `AND`/`OR`/`XOR`/`NOT` becomes impractical quickly: by cost 7 it had generated about 7.37 million functions while 13 of the 14 stream S-box output bits were still not found. This supports FFdecsa's existing decomposition strategy: synthesize 4-input temporary functions and combine them through the `fe` selector instead of treating each 5-input output independently.
- Packaging-only Step/register-window rewrites are exhausted for this phase. Multiple variants were correctness-green but failed to produce keep-grade e2e gains.
- The next plausible research target is a constrained, shared-DAG synthesis for each complete 5→2 S-box using the existing `fe`-mux structure: search for common 4-input subexpressions shared by both output bits, not independent one-output formulas. A keep candidate should show a clear isolated `GenerateBitslicedStream` win across paired runs before protocol measurement.
- Secondary target: cross-platform measurement of the existing `Vector256`/`Vector512` block-state update paths on AVX2/AVX-512 hardware. Apple Silicon cannot validate those wider paths.

### FFdecsa-style full 64x128 output transpose — deferred

Investigated replacing `BitSliceBlock.Decode128`'s 128 scalar 8x8 transposes and byte-reversal lookups with FFdecsa's six-stage, in-place `trasp64_128_88cw` matrix transpose. This looks attractive because the stream output is decoded once for every 8-byte payload block.

Implemented the six-stage `Vector128<ulong>` transform as an isolated prototype, including reversal of the per-byte plane order and per-`ulong` bit order. The 128-lane encode/decode round-trip still failed: its output does not differ from the current layout by a simple fixed byte reordering. FFdecsa's `PARALLEL_128_2LONG` plane/lane convention is coupled to its inverse input transpose, while the managed implementation's `TryEncode` and stream output use a different high-bit-first lane layout.

The prototype was removed before any hot-path measurement; Release tests are back to 73/73. Making this viable requires an end-to-end representation change across encode, active-lane masks, stream output ordering, and decode, then paired protocol validation. That is a high-risk data-layout rewrite, not a local decode substitution, so it is deferred behind shared-DAG stream S-box synthesis and wider-ISA validation.

### Explicit cross-output S-box CSE — discarded

Tested explicit reuse of six exact four-input subexpressions already present across the two output branches of the stream S-boxes: `fa | fb` (S1), `fa & fd` (S2), `fa ^ fc` (S5), and `fc ^ fd`, `fa & fc`, `fb | fd` (S7). This is a semantics-preserving shared-DAG subset, reducing the source-level boolean-operation count by six per `Step`.

Correctness: 73 Release tests passed; protocol checksum remained `76DC3CFC07B7D0F2`, with 0 managed allocation.

Isolated `GenerateBitslicedStream` short BenchmarkDotNet samples were mixed and overlapped:

- candidate: `223.29 ns` (99.9% CI `219.25–227.32 ns`)
- HEAD: `226.94 ns` (99.9% CI `221.55–232.33 ns`)

The end-to-end `C → HEAD → C` protocol sequence did not reproduce a stable gain:

- candidate: `747.19 ns/packet`
- HEAD: `756.41 ns/packet`
- candidate: `764.45 ns/packet`

The candidate average is effectively equal to HEAD once the thermal/order drift is included. Restored HEAD. This confirms that manual extraction of small existing common nodes is below the keep threshold, whether because RyuJIT already eliminates much of it or because the additional live vector values offset the reduced gates. Future shared-DAG work must discover a materially different network, not merely expose obvious repeated terms.

### Arm64 backend instruction-selection gap — confirmed

Captured optimized Apple M4 assembly for both implementations.

- .NET 10.0.8 RyuJIT emits `Step<NormalStep>` as a 1,656-byte method using AdvSIMD `eor` / `and` / `orr` instructions. It has no vector stack spills, so simple live-range changes cannot recover a spill penalty.
- Apple Clang's optimized FFdecsa `PARALLEL_128_2LONG` binary uses AdvSIMD plus ARM SHA-3 boolean instructions, including `eor3.16b`, `bcax.16b`, `bic.16b`, `orn.16b`, and `bsl.16b`. The binary contains 82 static `eor3` / `bcax` instructions in the optimized decrypt path.
- The current .NET 10 reference intrinsics expose `Xor3` only under SVE. Apple M4 does not provide SVE, and there is no public non-SVE SHA-3 `eor3` / `bcax` intrinsic available to this pure-managed implementation.

The reference measurement in this inspection window was `502.34 ns/packet`; the managed baseline remains around `750–805 ns/packet` depending on host state. This is a concrete backend advantage for C that source-level `Vector128` expression reshaping cannot reproduce today. A native Arm64 helper could expose these instructions, but that changes the pure-managed scope and must be treated as a separate, opt-in backend rather than a migration optimization.

### Arm64 AdvSIMD bit-select lowering — kept

Stayed within the pure-managed implementation and used `Vector128.ConditionalSelect` only for exact mux identities already present in the stream kernel:

- four B-register rotation updates, `a ^ (p & (a ^ b))`;
- the `r` update, `r ^ (q & (carry ^ r))`;
- the S4 output mux, `tmp0 ^ (fe & (tmp1 ^ tmp0))`;
- the F update keeps the equivalent shared helper form and falls back to the original expression where the JIT does not retain a select instruction.

The helper uses `ConditionalSelect` on AdvSIMD and the original boolean identity elsewhere, so x86 paths retain their existing two-operation lowering. On Apple M4, RyuJIT lowers the direct sites to `bsl` (and chains same-mask rotations with `bit` / `bif`): `Step<NormalStep>` shrank from 1,656 B to 1,584 B, with no vector stack spills in either version.

Correctness: 73 Release tests passed; protocol checksum `76DC3CFC07B7D0F2`; 0 managed allocation.

Measurements on Apple M4 / .NET 10.0.8:

- isolated `GenerateBitslicedStream`: candidate `217.52 ns` (99.9% CI `214.91–220.13 ns`) vs immediately adjacent HEAD `226.94 ns` (CI `221.55–232.33 ns`), around **4% faster**;
- protocol samples: candidate `741.57 / 750.31 / 751.49 ns/packet`, HEAD `759.05 / 749.28 ns/packet`. The average end-to-end direction is a modest ~**0.8%** improvement amid host drift, while the isolated stream signal is clear.

Kept because the codegen change is directly verified, semantics are identical, the isolated hot path has a clear multi-nanosecond gain, and fallback code preserves non-Arm behavior. A follow-up that explicitly expanded the four F updates did produce additional select instructions but grew the Step body and regressed the isolated mean (`219.03 ns`); it was discarded.

### Multi-output S-box synthesis tooling — started

Added `tools/StreamSboxSynthesis`, an offline .NET tool that derives each stream S-box's four `fe` cofactor truth tables from the maintained C# boolean networks. It enumerates bounded four-input expressions over `AND`, `OR`, `XOR`, and portable `AND NOT`, then reports common nodes usable by at least two cofactors.

At formula cost 4, the initial catalog contains 8,312 unique truth functions. The current scorer is deliberately a bounded candidate generator, not a global-optimality proof: it only recognizes a shared node plus a one-gate composition with an independently enumerated residual. Candidates remain research artifacts until they are checked against all 32 S-box inputs and demonstrate a paired stream benchmark improvement.

### Two-shared-node S-box exploration — tooling extended, no kernel change

Added `xor2` mode to the synthesis explorer. It searches a bounded XOR basis of two independent shared four-input nodes, rejects residual expressions that recompute either shared node, and labels every S-box with its concrete stream outputs (`x[0],z[2]` through `p,q`). This exposes candidates missed by the earlier one-shared-node pass.

Important scoring correction: `cofactor_total` only counts the four `fe=0/1` formulas plus explicit shared definitions. It does not include the final `fe` muxes or the cross-output sharing already present in the maintained hand-tuned DAG. A candidate whose cofactor total is lower is therefore not automatically a faster or even lower-gate stream network.

Validated this limitation with the lowest-looking third-S-box candidate: after matching it to `y[0],x[2]` (rather than the adjacent fourth S-box), its complete implementation has the same 18 vector Boolean operations as the current source structure. It was not patched into the kernel. A temporary wrong-S-box mapping was caught immediately by the scalar-lane and packet regression tests, then reverted; the working implementation remains 73/73 green.

### Arm64 vectorized block-transform lookup — kept

The 128-lane block path previously performed 128 independent random `ushort` lookups per block-cipher round. Added an Arm64-only AdvSIMD path for that exact 128-lane shape:

- Split the packed transform result into two one-time 256-byte lookup tables: S-box output and permuted output.
- Process 16 indexes at a time with one `TBL` plus three `TBX` instructions per table. The index is reduced by 64 between table groups; out-of-range lanes retain the preceding result, yielding a complete 256-byte lookup without scalar gathers.
- Retain the previous packed-`ushort` scalar implementation as the fallback for x64 and other targets, so non-Arm code generation and semantics are unchanged.

Correctness:

- `dotnet test src/FFDecsaSharp.slnx --no-restore -c Release -m:1` — 73 passed.
- Protocol checksum remains `76DC3CFC07B7D0F2`; managed allocation remains 0 B.
- Captured optimized Arm64 RyuJIT output contains the expected `tbl` and `tbx` instructions in `PopulateTransformOutputs128Arm64`.

Apple M4 / .NET 10.0.8 / Arm64 RyuJIT measurements with matching short BenchmarkDotNet jobs:

- `DecipherBlocksColumnMajor`: baseline **20.789 ns/block**; candidate **15.795 ns/block**; approximately **24.0% faster**.
- `ffdecsa-compare-v1`, 128 decrypt-only packets: immediately adjacent baseline **747.898 ns/packet**; candidate **626.361 ns/packet**; approximately **16.2% faster**.

This is a local data-layout realization of the existing block transform, not a native backend. It is the first retained improvement that materially reduces the scalar block-table contribution; stream S-box synthesis remains a separate research track.

### Stream S-box synthesis scoring: complete_total filter — tooling kept

Updated `tools/StreamSboxSynthesis` so reported candidates include the final `fe`
merge cost (`complete_total = cofactor_total + mux_cost`) and are filtered to
those strictly below the maintained portable source-gate counts for each S-box
(S1..S7: 23/19/17/20/21/18/19). This avoids over-ranking cofactor-only totals
that ignore muxes and existing hand-tuned sharing.

At formula cost 6 the only under-threshold candidate remains S1
(`complete_total=22`, shared `(((fa|fd)&~fb)^(fc&~(fa&fd)))`). No other S-box
produced a complete cut under the current one-shared-node residual model.

### Shared four-output S1 DAG rewrite — discarded

Patched S1 (`x[0], z[2]`) to the cost-5/6 explorer candidate: one shared node
used by all four `fe` cofactors, then two `Select` merges. Exact match over all
32 inputs; Release tests 73/73; protocol checksum `76DC3CFC07B7D0F2`.

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 alternating HEAD/CAND pairs
(Apple M4, .NET 10.0.8):

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 240.8 ns | 222.2 ns |
| 2 | 222.1 ns | 221.6 ns |
| 3 | 234.6 ns | 221.2 ns |
| mean | ≈ **232.5 ns** | ≈ **221.7 ns** |

Protocol `ffdecsa-compare-v1` pairs in the same window:

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 639.4 ns | 638.8 ns |
| 2 | 633.7 ns | 632.0 ns |
| 3 | 644.1 ns | 638.6 ns |
| mean | ≈ **639.1 ns** | ≈ **636.5 ns** |

Interpretation: the theoretical portable gate cut is only Δ−1 on S1, and the
isolated stream means are polluted by high-variance HEAD samples (pairs 1 and 3
have large StdDev). End-to-end deltas stay inside host noise (~1–5 ns). Restored
HEAD. Do not keep single-S-box rewrites at the Δ−1 level without a quiet multi-
pair isolated win that also shows up in protocol.

### Expand remaining `tmp ^ (fe & delta)` merges to `Select` — discarded

Rewrote the remaining FFdecsa-style XOR-and merges in `Step` to the equivalent
`Select(fe, tmp ^ delta, tmp)` form (and the inverted S3 merge to
`Select(fe, tmp0, tmp0 ^ tmp1)`), aiming to lower more sites to Arm64 `BSL`
alongside the already-kept F-update selects. Correctness: 73/73 Release tests.

Isolated stream + protocol ShortRun / harness, 3 alternating pairs:

| Pair | stream HEAD | stream CAND | protocol HEAD | protocol CAND |
|-----:|------------:|------------:|--------------:|--------------:|
| 1 | 217.6 ns | 221.2 ns | 628.4 ns | 630.9 ns |
| 2 | 232.4 ns | 216.8 ns | 682.7 ns | 634.4 ns |
| 3 | 217.9 ns | 245.2 ns | 630.7 ns | 658.2 ns |
| mean | ≈ **222.6 ns** | ≈ **227.7 ns** | ≈ **647.3 ns** | ≈ **641.2 ns** |

No keep-grade signal: stream often regresses or swings with host noise, and the
protocol mean is dominated by a single noisy HEAD sample rather than a
reproducible candidate win. Restored HEAD. Expanding every fe-merge to
`Select` without a verified reduction in Step size / spills is not free; the
previous kept BSL change already covers the profitable F-update sites.

### Next research targets after these discards

1. Deeper multi-output synthesis: residuals beyond one catalog gate, or an
   exact multi-output DAG/SAT search seeded by the PLA export — the current
   explorer is saturated for one-shared-node cuts except the discarded S1 Δ−1.
2. Remaining e2e bulk after the kept Arm64 block `TBL`/`TBX` path is now
   stream-dominated again; packaging-only stream rewrites remain exhausted.
3. Deferred full-matrix stream transpose / native Arm64 helper remain out of
   pure-managed local scope unless explicitly reopened.

### Multi-output beam-search mode — tooling kept

Added `beam` mode to `tools/StreamSboxSynthesis`. It grows a shared four-input
DAG with `AND`/`OR`/`XOR`/`AND NOT`, ranks partial states by Hamming distance
to the four `fe` cofactors, and only reports exact covers (all cofactors present
as DAG nodes) including the final mux cost.

Example:

```sh
dotnet run -c Release --project tools/StreamSboxSynthesis -- beam 1 20 120
```

Deep scan (`max_gates=20`, `beam=120`) exact-cover results vs portable source
gate counts:

| S-box | source | best exact complete | delta |
|------:|-------:|--------------------:|------:|
| 1 | 23 | 20 | −3 |
| 2 | 19 | 23 | +4 |
| 3 | 17 | 17 | 0 |
| 4 | 20 | no cover ≤20 | — |
| 5 | 21 | no cover ≤20 | — |
| 6 | 18 | 22 | +4 |
| 7 | 19 | 24 | +5 |

Only S1 is a structural under-threshold candidate under this heuristic. The
score counts every intermediate DAG node once; it still does not model Arm64
`BSL`, register pressure, or RyuJIT scheduling.

### Beam-search S1 shared DAG (complete 20) — discarded

Implemented the S1 exact cover found by beam search (16 intermediate gates + 2
`Select` merges; theoretical portable complete cost 20 vs HEAD 23). Verified
over all 32 inputs; Release tests 73/73; protocol checksum `76DC3CFC07B7D0F2`.

Isolated BDN `GenerateBitslicedStream` ShortRun, 3 alternating pairs:

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 219.5 ns | 220.0 ns |
| 2 | 220.2 ns | 220.5 ns |
| 3 | 219.3 ns | 221.0 ns |
| mean | ≈ **219.7 ns** | ≈ **220.5 ns** |

Protocol pairs:

| Pair | HEAD | CAND |
|-----:|-----:|-----:|
| 1 | 627.3 ns | 628.1 ns |
| 2 | 636.7 ns | 631.3 ns |
| 3 | 636.7 ns | 631.4 ns |
| mean | ≈ **633.5 ns** | ≈ **630.3 ns** |

Isolated stream is a small but consistent regression (~0.8 ns). Protocol is
mixed and within host noise. Restored HEAD. A theoretical Δ−3 portable gate cut
on a single S-box is still below the keep threshold on this managed Arm64 path
when it does not improve the isolated stream kernel.

Current HEAD baseline in this quiet window (after kept Arm64 block `TBL`/`TBX`):

- `GenerateBitslicedStream` ≈ **220–235 ns**/packet (host-dependent)
- `DecipherBlocksColumnMajor` ≈ **15.83 ns**/block
- `ffdecsa-compare-v1` ≈ **627–651 ns**/packet

### Research status after this session

Kept:

- `complete_total` filtering in the shared-node explorer
- Hamming-guided multi-output `beam` mode (candidate generator only)

Discarded after measurement:

- single-shared-node S1 rewrite (`complete_total` 22)
- expanding remaining `fe` XOR-and merges to `Select`
- beam-search S1 DAG (`complete_total` 20)

Still open for pure-managed work:

1. Stronger multi-output synthesis (exact SAT/DAG, or residual CSE that reduces
   live vector pressure rather than only gate count) — only S1 currently shows a
   structural cut, and it did not win on the hot path.
2. Stream packaging / register-window rewrites remain exhausted.
3. Full-matrix stream transpose and native Arm64 helpers remain deferred /
   out-of-scope for the pure-managed scoreboard unless reopened explicitly.
