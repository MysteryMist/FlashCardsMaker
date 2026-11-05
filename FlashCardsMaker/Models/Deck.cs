using SQLite;

namespace FlashCardsMaker.Models;

[Table("Decks")]
public sealed class Deck
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(120), NotNull]
    public string Name { get; set; } = string.Empty;

    public string JlptLevel { get; set; } = "N5";

    public CardType CardType { get; set; }

    public int LastAssignedNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDraft { get; set; } = true;
}
