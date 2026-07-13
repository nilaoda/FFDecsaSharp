namespace FFDecsaSharp.Gui.Helpers;

internal static class ControlWordParser
{
    public static bool TryParse(string? text, out byte[] controlWord)
    {
        controlWord = [];
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            byte[] input = Convert.FromHexString(text.Trim());
            if (input.Length == 8)
            {
                controlWord = input;
                return true;
            }
            if (input.Length != 6) return false;

            controlWord =
            [
                input[0], input[1], input[2], (byte)(input[0] + input[1] + input[2]),
                input[3], input[4], input[5], (byte)(input[3] + input[4] + input[5]),
            ];
            return true;
        }
        catch (FormatException) { return false; }
    }
}
