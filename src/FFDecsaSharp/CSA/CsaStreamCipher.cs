namespace FFDecsaSharp.CSA;

internal struct CsaStreamCipher
{
    public const int BlockSize = 8;

    private const ulong RegisterMask = 0x000000FFFFFFFFFFUL;

    private ulong _a;
    private ulong _b;
    private byte _x;
    private byte _y;
    private byte _z;
    private byte _d;
    private byte _e;
    private byte _f;
    private byte _p;
    private byte _q;
    private byte _r;

    public static bool TryCreate(
        ReadOnlySpan<byte> streamA,
        ReadOnlySpan<byte> streamB,
        ReadOnlySpan<byte> initializationBlock,
        out CsaStreamCipher cipher)
    {
        cipher = default;

        if (streamA.Length < CsaKeySchedule.StreamNibbleCount
            || streamB.Length < CsaKeySchedule.StreamNibbleCount
            || initializationBlock.Length < BlockSize)
        {
            return false;
        }

        for (int i = 0; i < CsaKeySchedule.StreamNibbleCount; i++)
        {
            cipher._a |= (ulong)(streamA[i] & 0x0F) << (i * 4);
            cipher._b |= (ulong)(streamB[i] & 0x0F) << (i * 4);
        }

        for (int byteIndex = 0; byteIndex < BlockSize; byteIndex++)
        {
            byte input = initializationBlock[byteIndex];
            byte in2 = (byte)(input & 0x0F);
            byte in1 = (byte)(input >> 4);

            for (int step = 0; step < 4; step++)
            {
                bool useFirstInput = (step & 1) == 0;
                cipher.Step(
                    useFirstInput ? in1 : in2,
                    useFirstInput ? in2 : in1,
                    includeInput: true);
            }
        }

        return true;
    }

    public bool TryGenerate(Span<byte> output)
    {
        if (output.Length < BlockSize)
        {
            return false;
        }

        for (int byteIndex = 0; byteIndex < BlockSize; byteIndex++)
        {
            byte value = 0;

            for (int step = 0; step < 4; step++)
            {
                Step(0, 0, includeInput: false);

                int bitOffset = 6 - (step * 2);
                value |= (byte)(((GetBit(_d, 2) ^ GetBit(_d, 3)) << (bitOffset + 1))
                    | ((GetBit(_d, 0) ^ GetBit(_d, 1)) << bitOffset));
            }

            output[byteIndex] = value;
        }

        return true;
    }

    private void Step(byte inputA, byte inputB, bool includeInput)
    {
        byte a0 = GetRegisterNibble(_a, 0);
        byte a1 = GetRegisterNibble(_a, 1);
        byte a2 = GetRegisterNibble(_a, 2);
        byte a3 = GetRegisterNibble(_a, 3);
        byte a4 = GetRegisterNibble(_a, 4);
        byte a5 = GetRegisterNibble(_a, 5);
        byte a6 = GetRegisterNibble(_a, 6);
        byte a7 = GetRegisterNibble(_a, 7);
        byte a8 = GetRegisterNibble(_a, 8);
        byte a9 = GetRegisterNibble(_a, 9);

        byte b2 = GetRegisterNibble(_b, 2);
        byte b3 = GetRegisterNibble(_b, 3);
        byte b4 = GetRegisterNibble(_b, 4);
        byte b5 = GetRegisterNibble(_b, 5);
        byte b6 = GetRegisterNibble(_b, 6);
        byte b7 = GetRegisterNibble(_b, 7);
        byte b8 = GetRegisterNibble(_b, 8);
        byte b9 = GetRegisterNibble(_b, 9);

        byte s1 = GetSBoxValue(0, (GetBit(a3, 0) << 4) | (GetBit(a0, 2) << 3) | (GetBit(a5, 1) << 2) | (GetBit(a6, 3) << 1) | GetBit(a8, 0));
        byte s2 = GetSBoxValue(1, (GetBit(a1, 1) << 4) | (GetBit(a2, 2) << 3) | (GetBit(a5, 3) << 2) | (GetBit(a6, 0) << 1) | GetBit(a8, 1));
        byte s3 = GetSBoxValue(2, (GetBit(a0, 3) << 4) | (GetBit(a1, 0) << 3) | (GetBit(a4, 1) << 2) | (GetBit(a4, 3) << 1) | GetBit(a5, 2));
        byte s4 = GetSBoxValue(3, (GetBit(a2, 3) << 4) | (GetBit(a0, 1) << 3) | (GetBit(a1, 3) << 2) | (GetBit(a3, 2) << 1) | GetBit(a7, 0));
        byte s5 = GetSBoxValue(4, (GetBit(a4, 2) << 4) | (GetBit(a3, 3) << 3) | (GetBit(a5, 0) << 2) | (GetBit(a7, 1) << 1) | GetBit(a8, 2));
        byte s6 = GetSBoxValue(5, (GetBit(a2, 1) << 4) | (GetBit(a3, 1) << 3) | (GetBit(a4, 0) << 2) | (GetBit(a6, 2) << 1) | GetBit(a8, 3));
        byte s7 = GetSBoxValue(6, (GetBit(a1, 2) << 4) | (GetBit(a2, 0) << 3) | (GetBit(a6, 1) << 2) | (GetBit(a7, 2) << 1) | GetBit(a7, 3));

        byte extraB = (byte)(
            (GetBit(b8, 2) ^ GetBit(b5, 3) ^ GetBit(b2, 1) ^ GetBit(b7, 0))
            | ((GetBit(b4, 3) ^ GetBit(b7, 2) ^ GetBit(b3, 0) ^ GetBit(b4, 1)) << 1)
            | ((GetBit(b5, 0) ^ GetBit(b7, 1) ^ GetBit(b2, 3) ^ GetBit(b3, 2)) << 2)
            | ((GetBit(b2, 0) ^ GetBit(b5, 1) ^ GetBit(b6, 2) ^ GetBit(b8, 3)) << 3));

        byte nextA = (byte)(a9 ^ _x);
        byte nextB = (byte)(b6 ^ b9 ^ _y);
        if (includeInput)
        {
            nextA ^= (byte)(_d ^ inputA);
            nextB ^= inputB;
        }

        if (_p != 0)
        {
            nextB = RotateLeft(nextB);
        }

        _d = (byte)(_e ^ _z ^ extraB);
        byte previousF = _f;
        if (_q != 0)
        {
            int sum = _z + _e + _r;
            _f = (byte)(sum & 0x0F);
            _r = (byte)(sum >> 4);
        }
        else
        {
            _f = _e;
        }

        _e = previousF;
        _a = ((_a << 4) & RegisterMask) | nextA;
        _b = ((_b << 4) & RegisterMask) | nextB;
        _x = (byte)(((s1 >> 1) & 1) | (((s2 >> 1) & 1) << 1) | ((s3 & 1) << 2) | ((s4 & 1) << 3));
        _y = (byte)(((s3 >> 1) & 1) | (((s4 >> 1) & 1) << 1) | ((s5 & 1) << 2) | ((s6 & 1) << 3));
        _z = (byte)(((s5 >> 1) & 1) | (((s6 >> 1) & 1) << 1) | ((s1 & 1) << 2) | ((s2 & 1) << 3));
        _p = (byte)(s7 >> 1);
        _q = (byte)(s7 & 1);
    }

    private static byte GetRegisterNibble(ulong register, int index)
    {
        return (byte)((register >> (index * 4)) & 0x0F);
    }

    private static int GetBit(byte value, int bit)
    {
        return (value >> bit) & 1;
    }

    private static byte GetSBoxValue(int sBox, int input)
    {
        return StreamSBoxes[(sBox * 32) + input];
    }

    private static byte RotateLeft(byte value)
    {
        return (byte)(((value << 1) & 0x0F) | (value >> 3));
    }

    private static ReadOnlySpan<byte> StreamSBoxes =>
    [
        2, 0, 1, 1, 2, 3, 3, 0, 3, 2, 2, 0, 1, 1, 0, 3, 0, 3, 3, 0, 2, 2, 1, 1, 2, 2, 0, 3, 1, 1, 3, 0,
        3, 1, 0, 2, 2, 3, 3, 0, 1, 3, 2, 1, 0, 0, 1, 2, 3, 1, 0, 3, 3, 2, 0, 2, 0, 0, 1, 2, 2, 1, 3, 1,
        2, 0, 1, 2, 2, 3, 3, 1, 1, 1, 0, 3, 3, 0, 2, 0, 1, 3, 0, 1, 3, 0, 2, 2, 2, 0, 1, 2, 0, 3, 3, 1,
        3, 1, 2, 3, 0, 2, 1, 2, 1, 2, 0, 1, 3, 0, 0, 3, 1, 0, 3, 1, 2, 3, 0, 3, 0, 3, 2, 0, 1, 2, 2, 1,
        2, 0, 0, 1, 3, 2, 3, 2, 0, 1, 3, 3, 1, 0, 2, 1, 2, 3, 2, 0, 0, 3, 1, 1, 1, 0, 3, 2, 3, 1, 0, 2,
        0, 1, 2, 3, 1, 2, 2, 0, 0, 1, 3, 0, 2, 3, 1, 3, 2, 3, 0, 2, 3, 0, 1, 1, 2, 1, 1, 2, 0, 3, 3, 0,
        0, 3, 2, 2, 3, 0, 0, 1, 3, 0, 1, 3, 1, 2, 2, 1, 1, 0, 3, 3, 0, 1, 1, 2, 2, 3, 1, 0, 2, 3, 0, 2,
    ];
}
