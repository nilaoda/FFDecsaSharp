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

