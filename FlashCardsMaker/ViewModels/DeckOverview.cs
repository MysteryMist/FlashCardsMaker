using CommunityToolkit.Mvvm.ComponentModel;
using FlashCardsMaker.Models;

namespace FlashCardsMaker.ViewModels;

public partial class DeckOverview : ObservableObject
{
    public DeckOverview(Deck deck)
    {
        Deck = deck;
        Name = deck.Name;
        JlptLevel = deck.JlptLevel;
        CardType = deck.CardType;
    }

    public Deck Deck { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string JlptLevel { get; set; }

    [ObservableProperty]
    public partial CardType CardType { get; set; }

    [ObservableProperty]
    public partial int CardCount { get; set; }

    [ObservableProperty]
    public partial int FrontCompleteCount { get; set; }

    [ObservableProperty]
    public partial int BackCompleteCount { get; set; }

    [ObservableProperty]
    public partial int ProgressPercent { get; set; }

    [ObservableProperty]
    public partial DeckStatus Status { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public string EditButtonText => IsExpanded ? "Close" : "Edit";

    public string SelectionStroke => IsExpanded ? "#F28C28" : "#536452";
    public double SelectionStrokeThickness => IsExpanded ? 3 : 1.5;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(EditButtonText));
        OnPropertyChanged(nameof(SelectionStroke));
        OnPropertyChanged(nameof(SelectionStrokeThickness));
    }

    public string StatusLabel => Status switch
    {
        DeckStatus.Completed => "Completed",
        DeckStatus.MissingInformation => "Missing information",
        DeckStatus.InProgress => "In progress",
        _ => "Draft"
    };

    public string Subtitle => $"{JlptLevel}  •  {CardType}";
    public double ProgressFraction => ProgressPercent / 100d;
    public string FrontProgressLabel => $"Front {FrontCompleteCount}/{CardCount}";
    public string BackProgressLabel => $"Back {BackCompleteCount}/{CardCount}";

    public string StatusColor => Status switch
    {
        DeckStatus.Completed => "#1F6F50",
        DeckStatus.InProgress => "#D6A43B",
        DeckStatus.MissingInformation => "#C64E4E",
        _ => "#777777"
    };

    public void Update(Deck deck, int count, int frontComplete, int backComplete, int progress, DeckStatus status)
    {
        Deck = deck;
        Name = deck.Name;
        JlptLevel = deck.JlptLevel;
        CardType = deck.CardType;
        CardCount = count;
        FrontCompleteCount = frontComplete;
        BackCompleteCount = backComplete;
        ProgressPercent = progress;
        Status = status;
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(FrontProgressLabel));
        OnPropertyChanged(nameof(BackProgressLabel));
    }
}
