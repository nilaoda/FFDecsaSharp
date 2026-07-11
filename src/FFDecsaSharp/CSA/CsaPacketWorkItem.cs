namespace FFDecsaSharp.CSA;

internal readonly struct CsaPacketWorkItem
{
    public CsaPacketWorkItem(CsaKeyKind keyKind, int payloadOffset, int payloadLength)
    {
        KeyKind = keyKind;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public CsaKeyKind KeyKind { get; }

    public int PayloadOffset { get; }

    public int PayloadLength { get; }

    public int BlockCount => PayloadLength / 8;

    public int ResidueByteCount => PayloadLength % 8;
}
