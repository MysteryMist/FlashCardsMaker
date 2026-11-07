using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using FlashCardsMaker.Models;

namespace FlashCardsMaker.ViewModels;

public partial class CardListItemViewModel : ObservableObject
{
    private string _hiragana = string.Empty;
    private string _backPreviewMeta = string.Empty;
    private string _backPreviewMeanings = "No meanings yet";

    public CardListItemViewModel(Flashcard card, CardStatus status)
    {
        Card = card;
        FrontText = card.FrontText;
        Hiragana = card.Hiragana;
        UpdateStatus(status);
    }

    public Flashcard Card { get; }
    public int Number => Card.Number;

    [ObservableProperty]
    public partial string FrontText { get; set; }

    public string Hiragana
    {
        get => _hiragana;
        set => SetProperty(ref _hiragana, value);
    }

    public string BackPreviewMeta
    {
        get => _backPreviewMeta;
        set => SetProperty(ref _backPreviewMeta, value);
    }

    public string BackPreviewMeanings
    {
        get => _backPreviewMeanings;
        set => SetProperty(ref _backPreviewMeanings, value);
    }

    public ObservableCollection<CardMeaning> BackMeanings { get; } = [];

    [ObservableProperty]
    public partial CardStatus Status { get; set; }

    [ObservableProperty]
    public partial string StatusColor { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string SelectionStroke => IsSelected ? "#FF8C00" : "#7C8980";
    public double SelectionStrokeThickness => IsSelected ? 4 : 2.5;
    public string SelectionBackgroundColor => IsSelected ? "#49351F" : "#242824";
    public string SelectionTextColor => IsSelected ? "#FFFFFF" : "#D7DED9";

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionStroke));
        OnPropertyChanged(nameof(SelectionStrokeThickness));
        OnPropertyChanged(nameof(SelectionBackgroundColor));
        OnPropertyChanged(nameof(SelectionTextColor));
    }

    public void UpdateStatus(CardStatus value)
    {
        Status = value;
        StatusColor = value switch
        {
            CardStatus.Complete => "#1F6F50",
            CardStatus.InProgress => "#D6A43B",
            _ => "#C64E4E"
        };
    }

    public void UpdateFrontText(string value)
    {
        Card.FrontText = value;
        FrontText = value;
    }

    public void UpdateBackPreview(CardType cardType, string hiragana, VerbKind? verbKind,
        VerbTransitivity? transitivity, AdverbClassification? adverbClassification,
        NounCategory? nounCategory, IEnumerable<CardMeaning> meanings)
    {
        Card.Hiragana = hiragana;
        Card.VerbKind = verbKind;
        Card.Transitivity = transitivity;
        Card.AdverbClassification = adverbClassification;
        Card.NounCategory = nounCategory;
        Hiragana = hiragana;
        BackPreviewMeta = GetBackPreviewMeta(Card, cardType);
        var orderedMeanings = meanings.OrderBy(item => item.SortOrder).ToArray();
        BackMeanings.Clear();
        foreach (var meaning in orderedMeanings)
            BackMeanings.Add(meaning);
        BackPreviewMeanings = BackMeanings.Count == 0
            ? "No meanings yet"
            : string.Join(Environment.NewLine, BackMeanings.Select(meaning => $"•  {meaning.Text}"));
    }

    private static string GetBackPreviewMeta(Flashcard card, CardType cardType) => cardType switch
    {
        CardType.Verb => $"{card.VerbKind?.ToString().ToLowerInvariant() ?? "verb"} verb, {card.Transitivity?.ToString().ToLowerInvariant() ?? ""} verb".Trim(' ', ','),
        CardType.Adverb => $"{card.AdverbClassification?.ToString().ToLowerInvariant() ?? ""} adverb".Trim(),
        CardType.Noun => $"{card.NounCategory?.ToString().ToLowerInvariant() ?? ""} noun".Trim(),
        CardType.IAdjective => "i-adjective",
        CardType.NaAdjective => "na-adjective",
        _ => cardType.ToString().ToLowerInvariant()
    };
}
