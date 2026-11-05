using CommunityToolkit.Mvvm.ComponentModel;
using FlashCardsMaker.Models;
using Microsoft.Maui.Controls;

namespace FlashCardsMaker.ViewModels;

public partial class MeaningEntryViewModel : ObservableObject
{
    private readonly Func<MeaningEntryViewModel, Task> _save;
    private CancellationTokenSource? _saveDelay;
    private Task? _saveTask;
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
    public float PreviewFontSize { get; private set; } = 13;
    public IReadOnlyList<string> StyleOptions { get; } = Enum.GetNames<MeaningStyle>();
    public string StyleName
    {
        get => Style.ToString();
        set
        {
            if (Enum.TryParse<MeaningStyle>(value, out var style) && Enum.IsDefined(style))
                Style = style;
        }
    }
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

    [ObservableProperty]
    public partial bool IsDeleting { get; set; }

    public bool CanEdit => !IsDeleting;

    public string PreviewBorderColor => IsPrimaryMeaning ? "#1F6F50" : "#303630";

    partial void OnIsPrimaryMeaningChanged(bool value)
    {
        OnPropertyChanged(nameof(PrimaryButtonLabel));
        OnPropertyChanged(nameof(PreviewBorderColor));
    }

    partial void OnIsDeletingChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    [ObservableProperty]
    public partial MeaningStyle Style { get; set; }

    partial void OnTextChanged(string value) => QueueSave();
    partial void OnStyleChanged(MeaningStyle value)
    {
        QueueSave();
        OnPropertyChanged(nameof(StyleName));
        OnPropertyChanged(nameof(PreviewFontAttributes));
        OnPropertyChanged(nameof(PreviewTextDecorations));
    }

    public void SetPrimaryWithoutSaving(bool value) => IsPrimaryMeaning = value;

    public void SetPreviewFontSize(float value)
    {
        if (Math.Abs(PreviewFontSize - value) < 0.1f)
            return;

        PreviewFontSize = value;
        OnPropertyChanged(nameof(PreviewFontSize));
    }

    private void QueueSave()
    {
        if (_isLoading || IsDeleting)
            return;

        _saveDelay?.Cancel();
        _saveDelay?.Dispose();
        _saveDelay = new CancellationTokenSource();
        _saveTask = SaveAfterDelayAsync(_saveDelay.Token);
    }

    public async Task PrepareForDeletionAsync()
    {
        IsDeleting = true;
        _saveDelay?.Cancel();
        if (_saveTask is not null)
            await _saveTask;
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
