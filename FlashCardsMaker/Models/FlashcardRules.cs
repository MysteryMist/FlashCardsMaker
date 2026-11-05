namespace FlashCardsMaker.Models;

public static class FlashcardRules
{
    public static bool IsFrontComplete(Flashcard card) => !string.IsNullOrWhiteSpace(card.FrontText);

    public static bool HasBackInformation(CardType cardType, Flashcard card, IReadOnlyCollection<CardMeaning> meanings) =>
        !string.IsNullOrWhiteSpace(card.Hiragana) ||
        meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning.Text)) ||
        cardType switch
        {
            CardType.Verb => card.VerbKind.HasValue || card.Transitivity.HasValue,
            CardType.Adverb => card.AdverbClassification.HasValue,
            CardType.Noun => card.NounCategory.HasValue,
            _ => false
        };

    public static bool IsBackComplete(CardType cardType, Flashcard card, IReadOnlyCollection<CardMeaning> meanings)
    {
        if (string.IsNullOrWhiteSpace(card.Hiragana) || !meanings.Any(meaning => !string.IsNullOrWhiteSpace(meaning.Text)))
            return false;

        return cardType switch
        {
            CardType.Verb => card.VerbKind.HasValue && card.Transitivity.HasValue,
            CardType.Adverb => card.AdverbClassification.HasValue,
            CardType.Noun => card.NounCategory.HasValue,
            _ => true
        };
    }

    public static CardStatus GetStatus(CardType cardType, Flashcard card, IReadOnlyCollection<CardMeaning> meanings)
    {
        if (!IsFrontComplete(card))
            return CardStatus.MissingInformation;
        if (IsBackComplete(cardType, card, meanings))
            return CardStatus.Complete;

        return HasBackInformation(cardType, card, meanings)
            ? CardStatus.InProgress
            : CardStatus.MissingInformation;
    }

    public static DeckStatus GetDeckStatus(IReadOnlyCollection<Flashcard> cards, CardType cardType,
        IReadOnlyDictionary<int, IReadOnlyCollection<CardMeaning>> meaningsByCard)
    {
        if (cards.Count == 0)
            return DeckStatus.Draft;

        var statuses = cards.Select(card => GetStatus(
            cardType,
            card,
            meaningsByCard.TryGetValue(card.Id, out var meanings) ? meanings : Array.Empty<CardMeaning>())).ToArray();

        if (statuses.All(status => status == CardStatus.Complete))
            return DeckStatus.Completed;
        if (statuses.Any(status => status == CardStatus.MissingInformation))
            return DeckStatus.MissingInformation;
        if (statuses.Any(status => status == CardStatus.InProgress))
            return DeckStatus.InProgress;

        return DeckStatus.Draft;
    }
}
