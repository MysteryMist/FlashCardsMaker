using CommunityToolkit.Mvvm.ComponentModel;
using FlashCardsMaker.Models;

namespace FlashCardsMaker.ViewModels;

public partial class CardListItemViewModel : ObservableObject
{
    public CardListItemViewModel(Flashcard card, CardStatus status)
    {
        Card = card;
        FrontText = card.FrontText;
        UpdateStatus(status);
    }

    public Flashcard Card { get; }
    public int Number => Card.Number;

    [ObservableProperty]
    public partial string FrontText { get; set; }

    [ObservableProperty]
    public partial CardStatus Status { get; set; }

    [ObservableProperty]
    public partial string StatusColor { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string SelectionStroke => IsSelected ? "#F28C28" : "#536452";
    public double SelectionStrokeThickness => IsSelected ? 3 : 1.5;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(SelectionStroke));
        OnPropertyChanged(nameof(SelectionStrokeThickness));
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
}
