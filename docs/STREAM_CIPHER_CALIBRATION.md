# Stream Cipher Calibration

This document records stream-cipher reference observations from FFdecsa so the scalar C# port can continue from a concrete checkpoint.

## Reference Case

- Control word: `07 e0 1b 02 c9 e0 45 ee`
- Initialization block: `de cf 0a 0d b2 d7 c4 40`

## Confirmed FFdecsa Outputs

After `stream_cypher_group_init`, two consecutive `stream_cypher_group_normal` calls produce:

- `dc 15 de f1 4a f1 f8 2c`
- `75 c8 3a 1f bf 67 19 e1`

## Confirmed Initialization Snapshot

Lane 0 state after `stream_cypher_group_init`:

- `A 0 c a f d 2 f c 2 1`
- `B f 8 1 a f 4 5 4 1 e`
- `X 2 Y 0 Z 1 D 1 E 5 F 5 p1 q1 r0`

## Divergence Found

The first C# scalar draft matched the first two internal steps but diverged by the third internal step of initialization.

Reference FFdecsa state around the divergence:

- Step 0 X/Y/Z/p/q:
  - `X f f f f`
  - `Y 0 0 f f`
  - `Z f f 0 0`
  - `p 0`
  - `q f`
- Step 1 X/Y/Z/p/q:
  - `X 80 00 80 00` at group level
  - `Y 7f ff ff 00` at group level
  - `Z 00 ff 7f ff` at group level
  - `p 00`
  - `q ff`
- Step 2 expected X/Y/Z/p/q begins:
  - `X 80 00 ff ff`
  - `Y ff ff 00 00`
  - `Z ff 00 00 00`
  - `p 7f`
  - `q 7f`

## Next Calibration Step

Compare the full group values written to `A[30]` and `B[30]` after internal step 1. The lane-0 bit can match while full group values differ, which then changes subsequent bit-sliced boolean expressions.
