using SQLite;

namespace FlashCardsMaker.Models;

[Table("CardMeanings")]
public sealed class CardMeaning
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CardId { get; set; }

    [MaxLength(300), NotNull]
    public string Text { get; set; } = string.Empty;

    public bool IsPrimaryMeaning { get; set; }

    public MeaningStyle Style { get; set; }

    public int SortOrder { get; set; }
}
