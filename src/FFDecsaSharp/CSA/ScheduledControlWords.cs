namespace FFDecsaSharp.CSA;

internal sealed class ScheduledControlWords
{
    private ScheduledControlWords(ScheduledControlWord even, ScheduledControlWord odd)
    {
        Even = even;
        Odd = odd;
    }

    public ScheduledControlWord Even { get; }

    public ScheduledControlWord Odd { get; }

    public ScheduledControlWord Get(CsaKeyKind keyKind)
    {
        return keyKind == CsaKeyKind.Even ? Even : Odd;
    }

    public static bool TryCreate(ControlWords controlWords, out ScheduledControlWords? scheduledControlWords)
    {
        Span<byte> even = stackalloc byte[ControlWord.Size];
        Span<byte> odd = stackalloc byte[ControlWord.Size];
        controlWords.Even.CopyTo(even);
        controlWords.Odd.CopyTo(odd);

        if (!ScheduledControlWord.TryCreate(even, out ScheduledControlWord? scheduledEven)
            || !ScheduledControlWord.TryCreate(odd, out ScheduledControlWord? scheduledOdd))
        {
            scheduledControlWords = null;
            return false;
        }

        scheduledControlWords = new ScheduledControlWords(scheduledEven!, scheduledOdd!);
        return true;
    }
}
