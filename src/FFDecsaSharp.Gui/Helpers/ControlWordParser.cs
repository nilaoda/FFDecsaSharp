namespace FFDecsaSharp.Gui.Helpers;

internal static class ControlWordParser
{
    public static string NormalizeInput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        int nonWhitespaceCount = 0;
        foreach (char character in text)
        {
            if (!char.IsWhiteSpace(character)) nonWhitespaceCount++;
        }

        if (nonWhitespaceCount == text.Length) return text;

        return string.Create(nonWhitespaceCount, text, static (buffer, source) =>
        {
            int index = 0;
            foreach (char character in source)
            {
                if (!char.IsWhiteSpace(character)) buffer[index++] = character;
            }
        });
    }

    public static bool TryParse(string? text, out byte[] controlWord)
    {
        controlWord = [];
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            byte[] input = Convert.FromHexString(NormalizeInput(text));
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
