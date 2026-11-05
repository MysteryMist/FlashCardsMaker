namespace FlashCardsMaker.Models;

public static class FrontTextFontSizer
{
    public static float CalculateSize(string text, float availableWidth, float availableHeight, float maximumFontSize)
    {
        if (string.IsNullOrEmpty(text) || availableWidth <= 0 || availableHeight <= 0)
            return maximumFontSize;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var widestLine = lines.Max(CalculateWidthInEm);
        if (widestLine <= 0)
            return maximumFontSize;

        var widthFit = availableWidth * 0.88f / widestLine;
        var heightFit = availableHeight / (Math.Max(1, lines.Length) * 1.2f);
        return Math.Min(maximumFontSize, Math.Min(widthFit, heightFit));
    }

    private static float CalculateWidthInEm(string text)
    {
        var width = 0f;
        foreach (var character in text)
        {
            width += character switch
            {
                >= '\u2E80' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF' or >= '\u3040' and <= '\u31FF' => 1f,
                _ when char.IsWhiteSpace(character) => 0.32f,
                _ when char.IsUpper(character) => 0.68f,
                _ when char.IsPunctuation(character) => 0.42f,
                _ => 0.56f
            };
        }

        return width;
    }
}
