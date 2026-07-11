# FFdecsa Comparison Protocol

`FFDecsaSharp.PerfHarness` and `tools/ffdecsa-compare/run-reference.sh` both emit one JSON object using the `ffdecsa-compare-v1` format.

The protocol measures 128 complete 188-byte MPEG-TS packets with the same deterministic payload generator and even control word. Each timed batch decrypts one 128-packet buffer in place. Resetting that buffer from the source and verification run outside the timed window.

Both outputs contain `timed_scope`, `batch_packets`, `packets_processed`, `nanoseconds_per_packet`, `packets_per_second`, `megabits_per_second`, and an `output_fnv1a64` checksum. Compare results only when the format, batch size, timed scope, and checksum match.

Run the managed implementation:

```sh
dotnet run -c Release --project src/FFDecsaSharp.PerfHarness/FFDecsaSharp.PerfHarness.csproj
```

Run the FFdecsa 128-lane reference implementation:

```sh
tools/ffdecsa-compare/run-reference.sh
```

The reference script compiles upstream FFdecsa with `PARALLEL_128_2LONG`. Set `CC` to select another native compiler. The generated executable is written under the ignored `artifacts/` directory by default.
