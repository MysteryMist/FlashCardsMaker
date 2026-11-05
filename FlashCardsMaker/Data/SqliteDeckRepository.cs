using FlashCardsMaker.Models;
using Microsoft.Maui.Storage;
using SQLite;

namespace FlashCardsMaker.Data;

public sealed class SqliteDeckRepository : IDeckRepository
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _initialized;

    public SqliteDeckRepository()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "draconia-cards.db3");
        _database = new SQLiteAsyncConnection(databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _database.CreateTableAsync<Deck>();
        await _database.CreateTableAsync<Flashcard>();
        await _database.CreateTableAsync<CardMeaning>();
        var hasSequenceColumn = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('Decks') WHERE name = 'LastAssignedNumber'");
        if (hasSequenceColumn == 0)
            await _database.ExecuteAsync("ALTER TABLE Decks ADD COLUMN LastAssignedNumber INTEGER NOT NULL DEFAULT 0");
        _initialized = true;
    }

    public async Task<IReadOnlyList<Deck>> GetDecksAsync()
    {
        await InitializeAsync();
        return await _database.Table<Deck>().OrderByDescending(deck => deck.UpdatedAt).ToListAsync();
    }

    public async Task<Deck?> GetDeckAsync(int deckId)
    {
        await InitializeAsync();
        return await _database.Table<Deck>().FirstOrDefaultAsync(deck => deck.Id == deckId);
    }

    public async Task<IReadOnlyList<Flashcard>> GetCardsAsync(int deckId)
    {
        await InitializeAsync();
        return await _database.Table<Flashcard>().Where(card => card.DeckId == deckId)
            .OrderBy(card => card.Number).ToListAsync();
    }

    public async Task<IReadOnlyList<CardMeaning>> GetMeaningsAsync(int cardId)
    {
        await InitializeAsync();
        return await _database.Table<CardMeaning>().Where(meaning => meaning.CardId == cardId)
            .OrderBy(meaning => meaning.SortOrder).ThenBy(meaning => meaning.Id).ToListAsync();
    }

    public async Task SaveDeckAsync(Deck deck)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            deck.Name = deck.Name.Trim();
            deck.UpdatedAt = DateTime.UtcNow;
            if (deck.Id == 0)
            {
                deck.CreatedAt = deck.UpdatedAt;
                await _database.InsertAsync(deck);
            }
            else
            {
                await _database.UpdateAsync(deck);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteDeckAsync(int deckId)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var cards = await _database.Table<Flashcard>().Where(card => card.DeckId == deckId).ToListAsync();
            foreach (var card in cards)
            {
                await _database.Table<CardMeaning>().Where(meaning => meaning.CardId == card.Id).DeleteAsync();
            }

            await _database.Table<Flashcard>().Where(card => card.DeckId == deckId).DeleteAsync();
            await _database.Table<Deck>().Where(deck => deck.Id == deckId).DeleteAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Deck> ImportDeckAsync(DeckTransferData transferData, int? replaceDeckId)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            Deck? importedDeck = null;
            await _database.RunInTransactionAsync(connection =>
            {
                var nameConflict = connection.Table<Deck>().ToList()
                    .FirstOrDefault(deck => string.Equals(deck.Name, transferData.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (nameConflict is not null && nameConflict.Id != replaceDeckId)
                    throw new InvalidOperationException("A deck with this name already exists.");

                if (replaceDeckId is int existingId)
                {
                    var existingDeck = connection.Find<Deck>(existingId)
                        ?? throw new InvalidOperationException("The deck selected for replacement no longer exists.");
                    var existingCards = connection.Table<Flashcard>()
                        .Where(card => card.DeckId == existingId).ToList();
                    foreach (var card in existingCards)
                        connection.Table<CardMeaning>().Where(meaning => meaning.CardId == card.Id).Delete();
                    connection.Table<Flashcard>().Where(card => card.DeckId == existingId).Delete();
                    connection.Delete(existingDeck);
                }

                var now = DateTime.UtcNow;
                importedDeck = new Deck
                {
                    Name = transferData.Name.Trim(),
                    JlptLevel = transferData.JlptLevel,
                    CardType = transferData.CardType,
                    LastAssignedNumber = Math.Max(transferData.LastAssignedNumber,
                        transferData.Cards.Select(card => card.Number).DefaultIfEmpty().Max()),
                    IsDraft = transferData.IsDraft,
                    CreatedAt = transferData.CreatedAt == default ? now : transferData.CreatedAt,
                    UpdatedAt = now
                };
                connection.Insert(importedDeck);

                foreach (var sourceCard in transferData.Cards.OrderBy(card => card.Number))
                {
                    var importedCard = new Flashcard
                    {
                        DeckId = importedDeck.Id,
                        Number = sourceCard.Number,
                        FrontText = sourceCard.FrontText,
                        Hiragana = sourceCard.Hiragana,
                        VerbKind = sourceCard.VerbKind,
                        Transitivity = sourceCard.Transitivity,
                        AdverbClassification = sourceCard.AdverbClassification,
                        NounCategory = sourceCard.NounCategory,
                        CreatedAt = sourceCard.CreatedAt == default ? now : sourceCard.CreatedAt,
                        UpdatedAt = sourceCard.UpdatedAt == default ? now : sourceCard.UpdatedAt
                    };
                    connection.Insert(importedCard);

                    foreach (var sourceMeaning in sourceCard.Meanings.OrderBy(meaning => meaning.SortOrder))
                    {
                        connection.Insert(new CardMeaning
                        {
                            CardId = importedCard.Id,
                            Text = sourceMeaning.Text,
                            IsPrimaryMeaning = sourceMeaning.IsPrimaryMeaning,
                            Style = sourceMeaning.Style,
                            SortOrder = sourceMeaning.SortOrder
                        });
                    }
                }
            });

            return importedDeck!;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Deck> DuplicateDeckAsync(int deckId)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var source = await _database.Table<Deck>().FirstAsync(deck => deck.Id == deckId);
            var sourceCards = await _database.Table<Flashcard>().Where(card => card.DeckId == deckId)
                .OrderBy(card => card.Number).ToListAsync();
            var copy = new Deck
            {
                Name = $"{source.Name} Copy",
                JlptLevel = source.JlptLevel,
                CardType = source.CardType,
                LastAssignedNumber = source.LastAssignedNumber,
                IsDraft = true
            };
            await _database.InsertAsync(copy);

            foreach (var sourceCard in sourceCards)
            {
                var cardCopy = new Flashcard
                {
                    DeckId = copy.Id,
                    Number = sourceCard.Number,
                    FrontText = sourceCard.FrontText,
                    Hiragana = sourceCard.Hiragana,
                    VerbKind = sourceCard.VerbKind,
                    Transitivity = sourceCard.Transitivity,
                    AdverbClassification = sourceCard.AdverbClassification,
                    NounCategory = sourceCard.NounCategory
                };
                await _database.InsertAsync(cardCopy);

                var sourceMeanings = await _database.Table<CardMeaning>()
                    .Where(meaning => meaning.CardId == sourceCard.Id).OrderBy(meaning => meaning.SortOrder).ToListAsync();
                foreach (var meaning in sourceMeanings)
                {
                    await _database.InsertAsync(new CardMeaning
                    {
                        CardId = cardCopy.Id,
                        Text = meaning.Text,
                        IsPrimaryMeaning = meaning.IsPrimaryMeaning,
                        Style = meaning.Style,
                        SortOrder = meaning.SortOrder
                    });
                }
            }

            return copy;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Flashcard> CreateCardAsync(int deckId, string frontText)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var deck = await _database.Table<Deck>().FirstAsync(item => item.Id == deckId);
            var maxNumber = await _database.ExecuteScalarAsync<int>(
                "SELECT COALESCE(MAX(Number), 0) FROM Cards WHERE DeckId = ?", deckId);
            var number = Math.Max(deck.LastAssignedNumber, maxNumber) + 1;
            var card = new Flashcard
            {
                DeckId = deckId,
                Number = number,
                FrontText = frontText.Trim()
            };
            await _database.InsertAsync(card);
            deck.LastAssignedNumber = number;
            deck.UpdatedAt = DateTime.UtcNow;
            await _database.UpdateAsync(deck);
            await TouchDeckAsync(deckId);
            return card;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveCardAsync(Flashcard card)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var storedCard = await _database.Table<Flashcard>().FirstOrDefaultAsync(item => item.Id == card.Id);
            if (storedCard is null)
                return;

            card.Number = storedCard.Number;
            card.DeckId = storedCard.DeckId;
            card.UpdatedAt = DateTime.UtcNow;
            await _database.UpdateAsync(card);
            await TouchDeckAsync(card.DeckId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteCardAsync(int cardId)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var card = await _database.Table<Flashcard>().FirstOrDefaultAsync(item => item.Id == cardId);
            if (card is null)
                return;

            await _database.Table<CardMeaning>().Where(meaning => meaning.CardId == cardId).DeleteAsync();
            await _database.DeleteAsync(card);
            await TouchDeckAsync(card.DeckId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SaveMeaningAsync(CardMeaning meaning)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            await _database.RunInTransactionAsync(connection =>
            {
                if (meaning.IsPrimaryMeaning)
                {
                    connection.Table<CardMeaning>().Where(item => item.CardId == meaning.CardId)
                        .ToList().ForEach(item =>
                        {
                            item.IsPrimaryMeaning = false;
                            connection.Update(item);
                        });
                }

                if (meaning.Id == 0)
                    connection.Insert(meaning);
                else
                    connection.Update(meaning);
            });
            var card = await _database.Table<Flashcard>().FirstOrDefaultAsync(item => item.Id == meaning.CardId);
            if (card is not null)
                await TouchDeckAsync(card.DeckId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteMeaningAsync(int meaningId)
    {
        await InitializeAsync();
        await _writeLock.WaitAsync();
        try
        {
            var meaning = await _database.Table<CardMeaning>().FirstOrDefaultAsync(item => item.Id == meaningId);
            if (meaning is null)
                return;

            await _database.DeleteAsync(meaning);
            var card = await _database.Table<Flashcard>().FirstOrDefaultAsync(item => item.Id == meaning.CardId);
            if (card is not null)
                await TouchDeckAsync(card.DeckId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task TouchDeckAsync(int deckId)
    {
        var deck = await _database.Table<Deck>().FirstOrDefaultAsync(item => item.Id == deckId);
        if (deck is null)
            return;

        deck.UpdatedAt = DateTime.UtcNow;
        await _database.UpdateAsync(deck);
    }
}
