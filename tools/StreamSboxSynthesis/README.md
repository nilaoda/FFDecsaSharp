# Stream S-Box Synthesis Explorer

This is an offline research tool for the seven 5-to-2 DVB-CSA stream S-boxes.
It derives the four `fe` cofactor truth tables from the current C# boolean
networks, then enumerates bounded four-input expressions using `AND`, `OR`,
`XOR`, and portable `AND NOT`.

For each S-box it reports candidate intermediate functions that can be reused
by at least two cofactors. The scorer is deliberately conservative: it only
uses an exact one-gate composition of a shared node and an independently
enumerated residual. It is a candidate generator, not a proof of global
multi-output optimality. Any proposed network must be independently checked
against all 32 S-box inputs and then measured in the stream benchmark.

The explorer computes `complete_total` by adding the final `fe` merge cost to
the reported `cofactor_total`, and only prints candidates below the maintained
source network's portable Boolean-operation count. This is still a structural
screen rather than an ISA cycle model: it intentionally does not credit Arm64
`BSL` lowering or JIT-only common-subexpression elimination.

Run it with a bounded formula cost:

```sh
dotnet run -c Release --project tools/StreamSboxSynthesis -- 4
```

The `xor2` mode explores a bounded two-independent-shared-node XOR basis:

```sh
dotnet run -c Release --project tools/StreamSboxSynthesis -- xor2 6
```

Increasing the bound grows the expression catalog quickly. Cost 4 is intended
for fast discovery of useful cuts; a later exact DAG/SAT search can consume the
reported truth tables as its input.

The `beam` mode runs a multi-output beam search over shared intermediate DAG nodes
for one or all S-boxes. It reports complete gate counts including final `fe` merges:

```sh
dotnet run -c Release --project tools/StreamSboxSynthesis -- beam 0 18 64
```

`beam 0` means all seven S-boxes. This is a heuristic screen, not an exact optimum.
