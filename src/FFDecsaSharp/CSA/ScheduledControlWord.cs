namespace FFDecsaSharp.CSA;

internal sealed class ScheduledControlWord
{
    private readonly byte[] _controlWord;
    private readonly byte[] _streamA;
    private readonly byte[] _streamB;
    private readonly byte[] _blockSchedule;

    private ScheduledControlWord(ReadOnlySpan<byte> controlWord)
    {
        _controlWord = new byte[FFDecsaSharp.CSA.ControlWord.Size];
        _streamA = new byte[CsaKeySchedule.StreamNibbleCount];
        _streamB = new byte[CsaKeySchedule.StreamNibbleCount];
        _blockSchedule = new byte[CsaKeySchedule.BlockScheduleLength];

        controlWord.CopyTo(_controlWord);

        bool streamCreated = CsaKeySchedule.TryCreateStreamNibbles(_controlWord, _streamA, _streamB);
        bool blockCreated = CsaKeySchedule.TryCreateBlockSchedule(_controlWord, _blockSchedule);

        if (!streamCreated || !blockCreated)
        {
            throw new InvalidOperationException("Failed to schedule a validated control word.");
        }
    }

    public ReadOnlySpan<byte> ControlWord => _controlWord;

    public ReadOnlySpan<byte> StreamA => _streamA;

    public ReadOnlySpan<byte> StreamB => _streamB;

    public ReadOnlySpan<byte> BlockSchedule => _blockSchedule;

    public static bool TryCreate(ReadOnlySpan<byte> controlWord, out ScheduledControlWord? scheduledControlWord)
    {
        if (controlWord.Length != FFDecsaSharp.CSA.ControlWord.Size)
        {
            scheduledControlWord = null;
            return false;
        }

        scheduledControlWord = new ScheduledControlWord(controlWord);
        return true;
    }
}
