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

Run it with a bounded formula cost:

```sh
dotnet run -c Release --project tools/StreamSboxSynthesis -- 4
```

Increasing the bound grows the expression catalog quickly. Cost 4 is intended
for fast discovery of useful cuts; a later exact DAG/SAT search can consume the
reported truth tables as its input.
