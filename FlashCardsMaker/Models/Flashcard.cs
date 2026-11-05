using SQLite;

namespace FlashCardsMaker.Models;

[Table("Cards")]
public sealed class Flashcard
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int DeckId { get; set; }

    public int Number { get; set; }

    [MaxLength(200)]
    public string FrontText { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Hiragana { get; set; } = string.Empty;

    public VerbKind? VerbKind { get; set; }

    public VerbTransitivity? Transitivity { get; set; }

    public AdverbClassification? AdverbClassification { get; set; }

    public NounCategory? NounCategory { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
