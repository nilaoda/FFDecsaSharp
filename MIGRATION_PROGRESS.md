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
