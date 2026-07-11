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

## Scalar Resolution

The first draft was discarded because it attempted to reproduce the bit-sliced boolean network without fully preserving its lane semantics. The completed `CsaStreamCipher` instead implements the mathematically equivalent single-lane state machine:

- A and B are packed 10-nibble shift registers.
- The seven FFdecsa S-box truth tables are evaluated directly for each 5-bit input.
- The transposed initialization mapping is represented directly: the low nibble is `in2` and the high nibble is `in1`.
- Generated output bits use the FFdecsa positions `7..0` directly, so a single lane does not require transpose buffers.

This implementation now matches the recorded first and second FFdecsa output blocks and is covered by `CsaStreamCipherTests`. It also drives the end-to-end `Decryptor` packet test.
