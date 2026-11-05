using CommunityToolkit.Mvvm.ComponentModel;
using FlashCardsMaker.Models;
using Microsoft.Maui.Controls;

namespace FlashCardsMaker.ViewModels;

public partial class MeaningEntryViewModel : ObservableObject
{
    private readonly Func<MeaningEntryViewModel, Task> _save;
    private CancellationTokenSource? _saveDelay;
    private bool _isLoading;

    public MeaningEntryViewModel(CardMeaning meaning, Func<MeaningEntryViewModel, Task> save)
    {
        Model = meaning;
        _save = save;
        _isLoading = true;
        Text = meaning.Text;
        IsPrimaryMeaning = meaning.IsPrimaryMeaning;
        Style = meaning.Style;
        _isLoading = false;
    }

    public CardMeaning Model { get; }
    public string PrimaryButtonLabel => IsPrimaryMeaning ? "Primary" : "Set primary";
    public FontAttributes PreviewFontAttributes => Style is MeaningStyle.Bold or MeaningStyle.BoldUnderline
        ? FontAttributes.Bold
        : FontAttributes.None;
    public TextDecorations PreviewTextDecorations => Style is MeaningStyle.Underline or MeaningStyle.BoldUnderline
        ? TextDecorations.Underline
        : TextDecorations.None;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial bool IsPrimaryMeaning { get; set; }

    public string PreviewBorderColor => IsPrimaryMeaning ? "#1F6F50" : "#303630";

    partial void OnIsPrimaryMeaningChanged(bool value)
    {
        OnPropertyChanged(nameof(PrimaryButtonLabel));
        OnPropertyChanged(nameof(PreviewBorderColor));
    }

    [ObservableProperty]
    public partial MeaningStyle Style { get; set; }

    partial void OnTextChanged(string value) => QueueSave();
    partial void OnStyleChanged(MeaningStyle value)
    {
        QueueSave();
        OnPropertyChanged(nameof(PreviewFontAttributes));
        OnPropertyChanged(nameof(PreviewTextDecorations));
    }

    public void SetPrimaryWithoutSaving(bool value) => IsPrimaryMeaning = value;

    private void QueueSave()
    {
        if (_isLoading)
            return;

        _saveDelay?.Cancel();
        _saveDelay?.Dispose();
        _saveDelay = new CancellationTokenSource();
        _ = SaveAfterDelayAsync(_saveDelay.Token);
    }

    private async Task SaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            Model.Text = Text.Trim();
            Model.Style = Style;
            Model.IsPrimaryMeaning = IsPrimaryMeaning;
            await _save(this);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
