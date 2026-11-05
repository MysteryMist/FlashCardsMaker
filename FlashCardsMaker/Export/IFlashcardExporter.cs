using FlashCardsMaker.Models;

namespace FlashCardsMaker.Export;

public sealed record ExportArtifact(string FileName, byte[] Content);

public interface IFlashcardExporter
{
    Task<ExportArtifact> GenerateAsync(Deck deck, IReadOnlyList<Flashcard> cards,
        IReadOnlyDictionary<int, IReadOnlyList<CardMeaning>> meaningsByCard,
        PaperSize paperSize, PaperOrientation orientation, GridLayout gridLayout, DuplexFlipEdge flipEdge,
        ExportFormat format, ExportLayout exportLayout,
        CancellationToken cancellationToken = default);
}
