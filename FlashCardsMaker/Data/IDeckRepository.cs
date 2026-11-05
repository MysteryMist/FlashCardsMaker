using FlashCardsMaker.Models;

namespace FlashCardsMaker.Data;

public interface IDeckRepository
{
    Task InitializeAsync();
    Task<IReadOnlyList<Deck>> GetDecksAsync();
    Task<Deck?> GetDeckAsync(int deckId);
    Task<IReadOnlyList<Flashcard>> GetCardsAsync(int deckId);
    Task<IReadOnlyList<CardMeaning>> GetMeaningsAsync(int cardId);
    Task SaveDeckAsync(Deck deck);
    Task DeleteDeckAsync(int deckId);
    Task<Deck> ImportDeckAsync(DeckTransferData transferData, int? replaceDeckId);
    Task<Deck> DuplicateDeckAsync(int deckId);
    Task<Flashcard> CreateCardAsync(int deckId, string frontText);
    Task SaveCardAsync(Flashcard card);
    Task DeleteCardAsync(int cardId);
    Task SaveMeaningAsync(CardMeaning meaning);
    Task DeleteMeaningAsync(int meaningId);
}
