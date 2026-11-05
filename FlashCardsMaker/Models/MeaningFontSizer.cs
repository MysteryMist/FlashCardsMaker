namespace FlashCardsMaker.Models;

public static class MeaningFontSizer
{
    public static float CalculateSize(IEnumerable<string> meanings, float availableWidth, float availableHeight,
        float maximumSize, float minimumSize, float itemPadding, float itemSpacing, float lineHeightMultiplier = 1.25f)
    {
        var texts = meanings.Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        if (texts.Length == 0)
            return maximumSize;

        for (var size = maximumSize; size >= minimumSize; size -= 0.5f)
        {
            var charactersPerLine = Math.Max(1, availableWidth / (size * 0.62f));
            var requiredHeight = texts.Sum(text =>
                Math.Ceiling((text.Length + 3) / charactersPerLine) * size * lineHeightMultiplier + itemPadding)
                + Math.Max(0, texts.Length - 1) * itemSpacing;
            if (requiredHeight <= availableHeight)
                return size;
        }

        return minimumSize;
    }
}
