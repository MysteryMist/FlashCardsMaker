namespace FlashCardsMaker.Models;

public sealed class DeckTransferPackage
{
    public const int CurrentFormatVersion = 1;

    public string Format { get; set; } = "draconia-deck";
    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public DeckTransferData Deck { get; set; } = new();
}

public sealed class DeckTransferData
{
    public string Name { get; set; } = string.Empty;
    public string JlptLevel { get; set; } = "N5";
    public CardType CardType { get; set; }
    public int LastAssignedNumber { get; set; }
    public bool IsDraft { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DeckTransferCard> Cards { get; set; } = [];
}

public sealed class DeckTransferCard
{
    public int Number { get; set; }
    public string FrontText { get; set; } = string.Empty;
    public string Hiragana { get; set; } = string.Empty;
    public VerbKind? VerbKind { get; set; }
    public VerbTransitivity? Transitivity { get; set; }
    public AdverbClassification? AdverbClassification { get; set; }
    public NounCategory? NounCategory { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DeckTransferMeaning> Meanings { get; set; } = [];
}

public sealed class DeckTransferMeaning
{
    public string Text { get; set; } = string.Empty;
    public bool IsPrimaryMeaning { get; set; }
    public MeaningStyle Style { get; set; }
    public int SortOrder { get; set; }
}
