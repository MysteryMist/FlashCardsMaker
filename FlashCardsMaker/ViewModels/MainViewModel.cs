using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlashCardsMaker.Data;
using FlashCardsMaker.Export;
using FlashCardsMaker.Models;
using Microsoft.Maui.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlashCardsMaker.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string LastSelectedCardPreferencePrefix = "LastSelectedCard.";

    private static string GetLastSelectedCardPreferenceKey(int deckId) =>
        $"{LastSelectedCardPreferencePrefix}{deckId}";

    private static readonly JsonSerializerOptions DeckTransferJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IDeckRepository _repository;
    private readonly IFlashcardExporter _exporter;
    private CancellationTokenSource? _cardSaveDelay;
    private CancellationTokenSource? _deckSaveDelay;
    private bool _isLoading;

    public event EventHandler? MeaningAdding;
    public event EventHandler? MeaningAdded;

    public MainViewModel(IDeckRepository repository, IFlashcardExporter exporter)
    {
        _repository = repository;
        _exporter = exporter;
        NewDeckName = string.Empty;
        NewDeckJlptLevel = "N5";
        NewDeckCardType = CardType.Verb;
        DeckName = string.Empty;
        DeckJlptLevel = "N5";
        NewCardFrontText = string.Empty;
        FrontText = string.Empty;
        Hiragana = string.Empty;
        SaveStatus = "All changes saved locally";
        CardTypes = Enum.GetValues<CardType>();
        VerbKinds = Enum.GetValues<VerbKind>();
        Transitivities = Enum.GetValues<VerbTransitivity>();
        AdverbClassifications = Enum.GetValues<AdverbClassification>();
        NounCategories = Enum.GetValues<NounCategory>();
        MeaningStyles = Enum.GetValues<MeaningStyle>();
        JlptLevels = ["N5", "N4", "N3", "N2", "N1"];
        JlptFilterOptions = new(JlptLevels.Select(level => new FilterOptionViewModel(level, RefreshFilteredDecks)));
        CardTypeFilterOptions = new(Enum.GetNames<CardType>().Select(type => new FilterOptionViewModel(type, RefreshFilteredDecks)));
        PaperSizes = Enum.GetValues<PaperSize>();
        PaperOrientations = Enum.GetValues<PaperOrientation>();
        FlipEdges = ["Long edge", "Short edge"];
        GridLayoutLabels = ["3 × 3", "4 × 4", "5 × 5"];
        ExportFormatLabels = ["PDF", "PNG sheets (ZIP)", "JPEG sheets (ZIP)"];
        ExportLayoutLabels = ["Front only", "Back only", "Both in one file", "Both but separate files"];
    }

    public ObservableCollection<DeckOverview> Decks { get; } = [];
    public ObservableCollection<DeckOverview> FilteredDecks { get; } = [];
    public ObservableCollection<CardListItemViewModel> Cards { get; } = [];
    public ObservableCollection<MeaningEntryViewModel> Meanings { get; } = [];

    public IReadOnlyList<CardType> CardTypes { get; }
    public IReadOnlyList<VerbKind> VerbKinds { get; }
    public IReadOnlyList<VerbTransitivity> Transitivities { get; }
    public IReadOnlyList<AdverbClassification> AdverbClassifications { get; }
    public IReadOnlyList<NounCategory> NounCategories { get; }
    public IReadOnlyList<MeaningStyle> MeaningStyles { get; }
    public IReadOnlyList<string> JlptLevels { get; }
    public ObservableCollection<FilterOptionViewModel> JlptFilterOptions { get; }
    public ObservableCollection<FilterOptionViewModel> CardTypeFilterOptions { get; }
    public IReadOnlyList<PaperSize> PaperSizes { get; }
    public IReadOnlyList<PaperOrientation> PaperOrientations { get; }
    public IReadOnlyList<string> FlipEdges { get; }
    public IReadOnlyList<string> GridLayoutLabels { get; }
    public IReadOnlyList<string> ExportFormatLabels { get; }
    public IReadOnlyList<string> ExportLayoutLabels { get; }

    [ObservableProperty]
    public partial DeckOverview? SelectedDeck { get; set; }

    [ObservableProperty]
    public partial Flashcard? SelectedCard { get; set; }

    [ObservableProperty]
    public partial string NewDeckName { get; set; }

    private string _deckSearchText = string.Empty;

    [ObservableProperty]
    public partial string NewDeckJlptLevel { get; set; }

    [ObservableProperty]
    public partial CardType NewDeckCardType { get; set; }

    [ObservableProperty]
    public partial string DeckName { get; set; }

    [ObservableProperty]
    public partial string DeckJlptLevel { get; set; }

    [ObservableProperty]
    public partial CardType DeckCardType { get; set; }

    [ObservableProperty]
    public partial string NewCardFrontText { get; set; }

    [ObservableProperty]
    public partial string FrontText { get; set; }

    [ObservableProperty]
    public partial string Hiragana { get; set; }

    [ObservableProperty]
    public partial VerbKind? VerbKind { get; set; }

    [ObservableProperty]
    public partial VerbTransitivity? Transitivity { get; set; }

    [ObservableProperty]
    public partial AdverbClassification? AdverbClassification { get; set; }

    [ObservableProperty]
    public partial NounCategory? NounCategory { get; set; }

    [ObservableProperty]
    public partial string SaveStatus { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasSelectedDeck { get; set; }

    [ObservableProperty]
    public partial bool HasSelectedCard { get; set; }

    [ObservableProperty]
    public partial int TotalCards { get; set; }

    [ObservableProperty]
    public partial int CompletedDeckCount { get; set; }

    [ObservableProperty]
    public partial int DeckCount { get; set; }

    [ObservableProperty]
    public partial bool HasDecks { get; set; }

    [ObservableProperty]
    public partial bool HasNoSearchResults { get; set; }

    private PaperSize _selectedPaperSize = PaperSize.A4;
    private PaperOrientation _selectedPaperOrientation = PaperOrientation.Landscape;
    private string _selectedGridLayoutLabel = "3 × 3";
    private string _selectedFlipEdge = "Long edge";
    private string _selectedExportFormatLabel = "PDF";
    private string _selectedExportLayoutLabel = "Both in one file";

    public PaperSize SelectedPaperSize
    {
        get => _selectedPaperSize;
        set => SetProperty(ref _selectedPaperSize, value);
    }

    public PaperOrientation SelectedPaperOrientation
    {
        get => _selectedPaperOrientation;
        set => SetProperty(ref _selectedPaperOrientation, value);
    }

    public string DeckSearchText
    {
        get => _deckSearchText;
        set
        {
            if (SetProperty(ref _deckSearchText, value))
                RefreshFilteredDecks();
        }
    }

    public string SelectedGridLayoutLabel
    {
        get => _selectedGridLayoutLabel;
        set => SetProperty(ref _selectedGridLayoutLabel, value);
    }

    public string SelectedFlipEdge
    {
        get => _selectedFlipEdge;
        set => SetProperty(ref _selectedFlipEdge, value);
    }

    public string SelectedExportFormatLabel
    {
        get => _selectedExportFormatLabel;
        set => SetProperty(ref _selectedExportFormatLabel, value);
    }

    public string SelectedExportLayoutLabel
    {
        get => _selectedExportLayoutLabel;
        set => SetProperty(ref _selectedExportLayoutLabel, value);
    }

    public bool IsEmpty => !HasDecks;

    public bool IsVerbDeck => DeckCardType == CardType.Verb;
    public bool IsAdverbDeck => DeckCardType == CardType.Adverb;
    public bool IsNounDeck => DeckCardType == CardType.Noun;

    public string CardDisplayNumber => SelectedDeck is null || SelectedCard is null
        ? "CARD PREVIEW"
        : $"{DeckName.ToUpperInvariant()} {SelectedCard.Number:000}";

    public string BackPreviewNumber => SelectedCard is null
        ? string.Empty
        : $"{SelectedCard.Number}.";

    public string BackPreviewReading => Hiragana;

    private void UpdateMeaningPreviewFontSize()
    {
        var fontSize = MeaningFontSizer.CalculateSize(
            Meanings.Select(meaning => meaning.Text), 240, 100, 13, 6, 4, 2, 1.2f);
        foreach (var meaning in Meanings)
            meaning.SetPreviewFontSize(fontSize);
    }

    private MeaningEntryViewModel CreateMeaningEntryViewModel(CardMeaning meaning)
    {
        var viewModel = new MeaningEntryViewModel(meaning, SaveMeaningEntryAsync);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MeaningEntryViewModel.Text))
                UpdateMeaningPreviewFontSize();
        };
        return viewModel;
    }

    public FormattedString FrontPreviewText
    {
        get
        {
            var formattedText = new FormattedString();
            if (string.IsNullOrEmpty(FrontText))
                return formattedText;

            var start = 0;
            var isJapanese = IsJapaneseCharacter(FrontText[0]);
            for (var index = 1; index <= FrontText.Length; index++)
            {
                var nextIsJapanese = index < FrontText.Length && IsJapaneseCharacter(FrontText[index]);
                if (index < FrontText.Length && nextIsJapanese == isJapanese)
                    continue;

                formattedText.Spans.Add(new Span
                {
                    Text = FrontText[start..index],
                    FontFamily = isJapanese ? "MS Gothic" : "Times New Roman"
                });
                start = index;
                isJapanese = nextIsJapanese;
            }

            return formattedText;
        }
    }

    private static bool IsJapaneseCharacter(char character) =>
        character is >= '\u2E80' and <= '\u9FFF' or >= '\uF900' and <= '\uFAFF';

    public string CardStatusLabel => SelectedCard is null ? "No card selected" : FlashcardRules.GetStatus(
        DeckCardType, SelectedCard, Meanings.Select(meaning => meaning.Model).ToArray()) switch
    {
        CardStatus.Complete => "Complete",
        CardStatus.InProgress => "In progress",
        _ => "Missing information"
    };

    public string CardStatusColor => SelectedCard is null ? "#777777" : FlashcardRules.GetStatus(
        DeckCardType, SelectedCard, Meanings.Select(meaning => meaning.Model).ToArray()) switch
    {
        CardStatus.Complete => "#1F6F50",
        CardStatus.InProgress => "#D6A43B",
        _ => "#C64E4E"
    };

    public string BackPreviewMeta => DeckCardType switch
    {
        CardType.Verb => $"{VerbKind?.ToString().ToLowerInvariant() ?? "verb"} verb, {Transitivity?.ToString().ToLowerInvariant() ?? ""} verb".Trim(' ', ','),
        CardType.Adverb => $"{AdverbClassification?.ToString().ToLowerInvariant() ?? ""} adverb".Trim(),
        CardType.Noun => $"{NounCategory?.ToString().ToLowerInvariant() ?? ""} noun".Trim(),
        CardType.IAdjective => "i-adjective",
        CardType.NaAdjective => "na-adjective",
        _ => DeckCardType.ToString().ToLowerInvariant()
    };

    public string BackPreviewMetaColor => DeckCardType switch
    {
        CardType.Verb => VerbKind switch
        {
            FlashCardsMaker.Models.VerbKind.Godan => "#1565C0",
            FlashCardsMaker.Models.VerbKind.Ichidan => "#2E7D32",
            FlashCardsMaker.Models.VerbKind.Irregular => "#7B1FA2",
            _ => "#C62828"
        },
        CardType.Adverb => AdverbClassification switch
        {
            FlashCardsMaker.Models.AdverbClassification.Positive => "#2E7D32",
            FlashCardsMaker.Models.AdverbClassification.Negative => "#C62828",
            FlashCardsMaker.Models.AdverbClassification.Neutral => "#7B1FA2",
            _ => "#C62828"
        },
        _ => "#C62828"
    };

    partial void OnDeckNameChanged(string value)
    {
        QueueDeckSave();
        OnPropertyChanged(nameof(CardDisplayNumber));
    }

    partial void OnDeckJlptLevelChanged(string value) => QueueDeckSave();

    partial void OnDeckCardTypeChanged(CardType value)
    {
        QueueDeckSave();
        OnPropertyChanged(nameof(IsVerbDeck));
        OnPropertyChanged(nameof(IsAdverbDeck));
        OnPropertyChanged(nameof(IsNounDeck));
        OnPropertyChanged(nameof(BackPreviewMeta));
        OnPropertyChanged(nameof(BackPreviewMetaColor));
        UpdateCardStatus();
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        await RefreshDecksAsync(true);
    }

    [RelayCommand]
    private async Task CreateDeckAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDeckName))
            return;

        IsBusy = true;
        try
        {
            var deck = new Deck
            {
                Name = NewDeckName.Trim(),
                JlptLevel = NewDeckJlptLevel,
                CardType = NewDeckCardType
            };

            await _repository.SaveDeckAsync(deck);
            NewDeckName = string.Empty;
            await RefreshDecksAsync(true);

            var item = Decks.FirstOrDefault(deckItem => deckItem.Deck.Id == deck.Id);
            if (item is null)
            {
                var persistedDeck = await _repository.GetDeckAsync(deck.Id);
                if (persistedDeck is not null)
                    item = new DeckOverview(persistedDeck);
            }

            if (item is not null)
                await OpenDeckAsync(item);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Unable to create deck", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDeckAsync(DeckOverview? deckOverview)
    {
        if (deckOverview is null)
            return;

        try
        {
            await SaveSelectedCardAsync();
            await SaveSelectedDeckAsync();
            CancelScheduledSaves();

            var deck = await _repository.GetDeckAsync(deckOverview.Deck.Id);
            if (deck is null)
                return;

            if (SelectedDeck is not null)
                SelectedDeck.IsExpanded = false;

            _isLoading = true;
            SelectedDeck = deckOverview;
            deckOverview.IsExpanded = true;
            HasSelectedDeck = true;
            DeckName = deck.Name;
            DeckJlptLevel = deck.JlptLevel;
            DeckCardType = deck.CardType;
            Cards.Clear();
            Meanings.Clear();
            SelectedCard = null;
            HasSelectedCard = false;

            foreach (var card in (await _repository.GetCardsAsync(deck.Id)).OrderByDescending(card => card.Number))
            {
                var meanings = await _repository.GetMeaningsAsync(card.Id);
                Cards.Add(new CardListItemViewModel(card, FlashcardRules.GetStatus(deck.CardType, card, meanings)));
            }

            if (Cards.Count > 0)
            {
                var savedCardId = Preferences.Default.Get(GetLastSelectedCardPreferenceKey(deck.Id), 0);
                var cardToRestore = Cards.FirstOrDefault(item => item.Card.Id == savedCardId) ?? Cards[0];
                await LoadCardAsync(cardToRestore.Card);
            }
            else
            {
                Preferences.Default.Remove(GetLastSelectedCardPreferenceKey(deck.Id));
            }

            _isLoading = false;
            OnPropertyChanged(nameof(CardDisplayNumber));
        }
        catch (Exception ex)
        {
            _isLoading = false;
            await Shell.Current.DisplayAlertAsync("Unable to open deck", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task ToggleDeckAsync(DeckOverview? deckOverview)
    {
        if (deckOverview is null)
            return;

        if (SelectedDeck == deckOverview && deckOverview.IsExpanded)
        {
            await SaveSelectedCardAsync();
            await SaveSelectedDeckAsync();
            CancelScheduledSaves();
            deckOverview.IsExpanded = false;
            SelectedDeck = null;
            SelectedCard = null;
            HasSelectedDeck = false;
            HasSelectedCard = false;
            Cards.Clear();
            Meanings.Clear();
            OnPropertyChanged(nameof(CardDisplayNumber));
            await RefreshDecksAsync(true);
            return;
        }

        await OpenDeckAsync(deckOverview);
    }

    [RelayCommand]
    private async Task DuplicateDeckAsync()
    {
        if (SelectedDeck is null)
            return;

        try
        {
            var duplicate = await _repository.DuplicateDeckAsync(SelectedDeck.Deck.Id);
            await RefreshDecksAsync(true);

            var item = Decks.FirstOrDefault(deckItem => deckItem.Deck.Id == duplicate.Id);
            if (item is not null)
                await OpenDeckAsync(item);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Unable to duplicate deck", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedDeckAsync()
    {
        if (SelectedDeck is null)
            return;

        var deckName = SelectedDeck.Name;
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete deck",
            $"Delete '{deckName}' and all of its cards and meanings? This cannot be undone.",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        var deckId = SelectedDeck.Deck.Id;
        Preferences.Default.Remove(GetLastSelectedCardPreferenceKey(deckId));
        CancelScheduledSaves();
        SelectedDeck.IsExpanded = false;
        await _repository.DeleteDeckAsync(deckId);
        SelectedDeck = null;
        SelectedCard = null;
        HasSelectedDeck = false;
        HasSelectedCard = false;
        Cards.Clear();
        Meanings.Clear();
        await RefreshDecksAsync(true);
        OnPropertyChanged(nameof(CardDisplayNumber));
    }

    [RelayCommand]
    private async Task AddCardAsync()
    {
        if (SelectedDeck is null || string.IsNullOrWhiteSpace(NewCardFrontText))
            return;

        await SaveSelectedDeckAsync();
        var card = await _repository.CreateCardAsync(SelectedDeck.Deck.Id, NewCardFrontText);
        NewCardFrontText = string.Empty;
        Cards.Insert(0, new CardListItemViewModel(card, CardStatus.MissingInformation));
        await LoadCardAsync(card);
        await RefreshDecksAsync();
    }

    [RelayCommand]
    private async Task SelectCardAsync(CardListItemViewModel? card)
    {
        if (card is not null)
            await LoadCardAsync(card.Card);
    }

    [RelayCommand]
    private async Task DeleteCardAsync()
    {
        if (SelectedCard is null)
            return;

        var cardItem = Cards.FirstOrDefault(item => item.Card.Id == SelectedCard.Id);
        if (cardItem is null)
            return;

        var index = Cards.IndexOf(cardItem);
        await _repository.DeleteCardAsync(SelectedCard.Id);
        Cards.Remove(cardItem);
        SelectedCard = null;
        Meanings.Clear();
        HasSelectedCard = Cards.Count > 0;

        if (Cards.Count > 0)
            await LoadCardAsync(Cards[Math.Clamp(index, 0, Cards.Count - 1)].Card);
        else
        {
            FrontText = string.Empty;
            Hiragana = string.Empty;
            if (SelectedDeck is not null)
                Preferences.Default.Remove(GetLastSelectedCardPreferenceKey(SelectedDeck.Deck.Id));
        }

        await RefreshDecksAsync();
    }

    [RelayCommand]
    private async Task AddMeaningAsync()
    {
        if (SelectedCard is null)
            return;

        MeaningAdding?.Invoke(this, EventArgs.Empty);

        var meaning = new CardMeaning
        {
            CardId = SelectedCard.Id,
            SortOrder = Meanings.Count,
            Style = MeaningStyle.Normal
        };

        await _repository.SaveMeaningAsync(meaning);
        Meanings.Add(CreateMeaningEntryViewModel(meaning));
        UpdateMeaningPreviewFontSize();
        UpdateCardStatus();
        MeaningAdded?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DeleteMeaningAsync(MeaningEntryViewModel? meaning)
    {
        if (meaning is null || !Meanings.Contains(meaning))
            return;

        try
        {
            await meaning.PrepareForDeletionAsync();
            await _repository.DeleteMeaningAsync(meaning.Model.Id);
        }
        catch
        {
            meaning.IsDeleting = false;
            throw;
        }

        Meanings.Remove(meaning);
        UpdateMeaningPreviewFontSize();
        UpdateCardStatus();
        await RefreshDecksAsync();
    }

    [RelayCommand]
    private async Task SetPrimaryMeaningAsync(MeaningEntryViewModel? selected)
    {
        if (selected is null)
            return;

        foreach (var meaning in Meanings)
            meaning.SetPrimaryWithoutSaving(meaning == selected);

        foreach (var meaning in Meanings.Where(meaning => meaning != selected))
            await SaveMeaningEntryAsync(meaning);

        await SaveMeaningEntryAsync(selected);
    }

    [RelayCommand]
    private async Task ExportDeckAsync()
    {
        if (SelectedDeck is null)
            return;

        await SaveSelectedCardAsync();
        await SaveSelectedDeckAsync();
        var deck = await _repository.GetDeckAsync(SelectedDeck.Deck.Id);
        if (deck is null)
            return;

        var cards = await _repository.GetCardsAsync(deck.Id);
        if (cards.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Nothing to export", "Add cards to this deck before exporting.", "OK");
            return;
        }

        var meaningsByCard = new Dictionary<int, IReadOnlyList<CardMeaning>>();
        foreach (var card in cards)
            meaningsByCard[card.Id] = await _repository.GetMeaningsAsync(card.Id);

        var incomplete = cards.Where(card => !FlashcardRules.IsBackComplete(deck.CardType, card,
            meaningsByCard[card.Id])).ToArray();

        if (incomplete.Length > 0)
        {
            var action = await Shell.Current.DisplayActionSheetAsync(
                $"{incomplete.Length} card(s) have incomplete back information.",
                "Cancel", null, "Review Missing Cards", "Export Anyway");

            if (action == "Review Missing Cards")
            {
                await LoadCardAsync(incomplete[0]);
                return;
            }

            if (action != "Export Anyway")
                return;
        }

        var gridLayout = SelectedGridLayoutLabel switch
        {
            "4 × 4" => GridLayout.FourByFour,
            "5 × 5" => GridLayout.FiveByFive,
            _ => GridLayout.ThreeByThree
        };

        var flipEdge = SelectedFlipEdge == "Short edge" ? DuplexFlipEdge.ShortEdge : DuplexFlipEdge.LongEdge;
        var format = SelectedExportFormatLabel switch
        {
            "PNG sheets (ZIP)" => ExportFormat.PNG,
            "JPEG sheets (ZIP)" => ExportFormat.JPEG,
            _ => ExportFormat.PDF
        };
        var exportLayout = SelectedExportLayoutLabel switch
        {
            "Front only" => ExportLayout.FrontOnly,
            "Back only" => ExportLayout.BackOnly,
            "Both but separate files" => ExportLayout.BothSeparateFiles,
            _ => ExportLayout.BothInOneFile
        };

        IsBusy = true;
        try
        {
            var artifact = await _exporter.GenerateAsync(deck, cards, meaningsByCard, SelectedPaperSize, SelectedPaperOrientation,
                gridLayout, flipEdge, format, exportLayout);
            await using var content = new MemoryStream(artifact.Content);
            var result = await FileSaver.Default.SaveAsync(artifact.FileName, content);
            if (result.IsSuccessful)
                await Shell.Current.DisplayAlertAsync("Export complete", $"Saved {artifact.FileName}.", "Done");
            else if (result.Exception is not null)
                await Shell.Current.DisplayAlertAsync("Export failed", result.Exception.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportDeckDataAsync()
    {
        if (SelectedDeck is null)
            return;

        await SaveSelectedCardAsync();
        await SaveSelectedDeckAsync();
        var deck = await _repository.GetDeckAsync(SelectedDeck.Deck.Id);
        if (deck is null)
            return;

        var package = new DeckTransferPackage
        {
            Deck = new DeckTransferData
            {
                Name = deck.Name,
                JlptLevel = deck.JlptLevel,
                CardType = deck.CardType,
                LastAssignedNumber = deck.LastAssignedNumber,
                IsDraft = deck.IsDraft,
                CreatedAt = deck.CreatedAt,
                UpdatedAt = deck.UpdatedAt
            }
        };

        foreach (var card in await _repository.GetCardsAsync(deck.Id))
        {
            var transferCard = new DeckTransferCard
            {
                Number = card.Number,
                FrontText = card.FrontText,
                Hiragana = card.Hiragana,
                VerbKind = card.VerbKind,
                Transitivity = card.Transitivity,
                AdverbClassification = card.AdverbClassification,
                NounCategory = card.NounCategory,
                CreatedAt = card.CreatedAt,
                UpdatedAt = card.UpdatedAt
            };

            foreach (var meaning in await _repository.GetMeaningsAsync(card.Id))
            {
                transferCard.Meanings.Add(new DeckTransferMeaning
                {
                    Text = meaning.Text,
                    IsPrimaryMeaning = meaning.IsPrimaryMeaning,
                    Style = meaning.Style,
                    SortOrder = meaning.SortOrder
                });
            }

            package.Deck.Cards.Add(transferCard);
        }

        IsBusy = true;
        try
        {
            var safeName = new string(deck.Name.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '-' : character).ToArray()).Trim();
            await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(package, DeckTransferJsonOptions));
            var result = await FileSaver.Default.SaveAsync($"{safeName}.draconiadeck.json", content);
            if (result.IsSuccessful)
                await Shell.Current.DisplayAlertAsync("Deck data exported", $"Saved {safeName}.draconiadeck.json.", "Done");
            else if (result.Exception is not null)
                await Shell.Current.DisplayAlertAsync("Export failed", result.Exception.Message, "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Export failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportDeckDataAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose a Draconia deck data file"
        });
        if (file is null)
            return;

        IsBusy = true;
        try
        {
            DeckTransferPackage? package;
            await using (var stream = await file.OpenReadAsync())
                package = await JsonSerializer.DeserializeAsync<DeckTransferPackage>(stream, DeckTransferJsonOptions);

            ValidateDeckTransferPackage(package);
            var transferDeck = package!.Deck;
            await SaveSelectedCardAsync();
            await SaveSelectedDeckAsync();
            CancelScheduledSaves();

            var existingDecks = await _repository.GetDecksAsync();
            var nameConflict = existingDecks.FirstOrDefault(deck =>
                string.Equals(deck.Name, transferDeck.Name.Trim(), StringComparison.OrdinalIgnoreCase));
            int? replaceDeckId = null;

            if (nameConflict is not null)
            {
                var action = await Shell.Current.DisplayActionSheetAsync(
                    $"A deck named '{nameConflict.Name}' already exists.", "Cancel", null,
                    "Overwrite existing deck", "Rename imported deck");

                if (action == "Overwrite existing deck")
                {
                    var confirmed = await Shell.Current.DisplayAlertAsync(
                        "Overwrite existing deck?",
                        $"Replace '{nameConflict.Name}' and all of its cards and meanings? This cannot be undone.",
                        "Overwrite", "Cancel");
                    if (!confirmed)
                        return;

                    replaceDeckId = nameConflict.Id;
                }
                else if (action == "Rename imported deck")
                {
                    while (true)
                    {
                        var suggestedName = GetAvailableImportedDeckName(transferDeck.Name, existingDecks);
                        var newName = await Shell.Current.DisplayPromptAsync(
                            "Rename imported deck", "Enter a unique name for this deck.",
                            initialValue: suggestedName, maxLength: 120);
                        if (newName is null)
                            return;

                        newName = newName.Trim();
                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            await Shell.Current.DisplayAlertAsync("Name required", "Enter a deck name.", "OK");
                            continue;
                        }

                        if (existingDecks.Any(deck => string.Equals(deck.Name, newName, StringComparison.OrdinalIgnoreCase)))
                        {
                            await Shell.Current.DisplayAlertAsync("Name already exists", "Choose a different deck name.", "OK");
                            continue;
                        }

                        transferDeck.Name = newName;
                        break;
                    }
                }
                else
                {
                    return;
                }
            }

            if (SelectedDeck is { } selectedDeck && replaceDeckId == selectedDeck.Deck.Id)
            {
                selectedDeck.IsExpanded = false;
                SelectedDeck = null;
                SelectedCard = null;
                HasSelectedDeck = false;
                HasSelectedCard = false;
                Cards.Clear();
                Meanings.Clear();
            }

            var importedDeck = await _repository.ImportDeckAsync(transferDeck, replaceDeckId);
            await RefreshDecksAsync(true);

            var importedOverview = Decks.FirstOrDefault(deck => deck.Deck.Id == importedDeck.Id);
            if (importedOverview is not null)
                await OpenDeckAsync(importedOverview);

            await Shell.Current.DisplayAlertAsync("Deck imported", $"'{importedDeck.Name}' is ready to edit.", "Done");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Import failed", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void ValidateDeckTransferPackage(DeckTransferPackage? package)
    {
        if (package is null || package.Format != "draconia-deck"
            || package.FormatVersion != DeckTransferPackage.CurrentFormatVersion
            || package.Deck is null)
            throw new InvalidDataException("This is not a supported Draconia deck data file.");

        var deck = package.Deck;
        if (string.IsNullOrWhiteSpace(deck.Name) || deck.Name.Trim().Length > 120
            || !new[] { "N5", "N4", "N3", "N2", "N1" }.Contains(deck.JlptLevel)
            || !Enum.IsDefined(deck.CardType)
            || deck.LastAssignedNumber < 0
            || deck.Cards is null)
            throw new InvalidDataException("The deck information in this file is invalid.");

        if (deck.Cards.Any(card => card is null || card.Number <= 0
            || card.FrontText is null || card.FrontText.Length > 200
            || card.Hiragana is null || card.Hiragana.Length > 200
            || card.Meanings is null
            || card.Meanings.Any(meaning => meaning is null)
            || card.Meanings.Count(meaning => meaning.IsPrimaryMeaning) > 1
            || (card.VerbKind is not null && !Enum.IsDefined(card.VerbKind.Value))
            || (card.Transitivity is not null && !Enum.IsDefined(card.Transitivity.Value))
            || (card.AdverbClassification is not null && !Enum.IsDefined(card.AdverbClassification.Value))
            || (card.NounCategory is not null && !Enum.IsDefined(card.NounCategory.Value)))
            || deck.Cards.Select(card => card.Number).Distinct().Count() != deck.Cards.Count)
            throw new InvalidDataException("One or more cards in this file are invalid.");

        if (deck.Cards.SelectMany(card => card.Meanings).Any(meaning => meaning is null
            || meaning.Text is null || meaning.Text.Length > 300 || meaning.SortOrder < 0
            || !Enum.IsDefined(meaning.Style)))
            throw new InvalidDataException("One or more card meanings in this file are invalid.");
    }

    private static string GetAvailableImportedDeckName(string originalName, IReadOnlyList<Deck> existingDecks)
    {
        var suffix = 1;
        while (true)
        {
            var ending = suffix == 1 ? " (Imported)" : $" (Imported {suffix})";
            var prefixLength = Math.Max(0, 120 - ending.Length);
            var prefix = originalName[..Math.Min(originalName.Length, prefixLength)];
            var candidate = prefix + ending;
            if (!existingDecks.Any(deck => string.Equals(deck.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;

            suffix++;
        }
    }

    private async Task LoadCardAsync(Flashcard card)
    {
        await SaveSelectedCardAsync();
        _cardSaveDelay?.Cancel();
        _cardSaveDelay?.Dispose();
        _cardSaveDelay = null;

        _isLoading = true;
        foreach (var cardItem in Cards)
            cardItem.IsSelected = cardItem.Card.Id == card.Id;

        SelectedCard = card;
        if (SelectedDeck is not null)
            Preferences.Default.Set(GetLastSelectedCardPreferenceKey(SelectedDeck.Deck.Id), card.Id);
        HasSelectedCard = true;
        FrontText = card.FrontText;
        Hiragana = card.Hiragana;
        VerbKind = card.VerbKind;
        Transitivity = card.Transitivity;
        AdverbClassification = card.AdverbClassification;
        NounCategory = card.NounCategory;
        Meanings.Clear();

        foreach (var meaning in await _repository.GetMeaningsAsync(card.Id))
            Meanings.Add(new MeaningEntryViewModel(meaning, SaveMeaningEntryAsync));

        _isLoading = false;
        UpdateCardStatus();
    }

    private async Task SaveSelectedCardAsync()
    {
        if (SelectedCard is null || SelectedDeck is null)
            return;

        SelectedCard.FrontText = FrontText;
        SelectedCard.Hiragana = Hiragana;
        SelectedCard.VerbKind = VerbKind;
        SelectedCard.Transitivity = Transitivity;
        SelectedCard.AdverbClassification = AdverbClassification;
        SelectedCard.NounCategory = NounCategory;
        await _repository.SaveCardAsync(SelectedCard);
        SaveStatus = "All changes saved locally";
    }

    private async Task SaveSelectedDeckAsync()
    {
        if (SelectedDeck is null)
            return;

        SelectedDeck.Deck.Name = DeckName.Trim();
        SelectedDeck.Deck.JlptLevel = DeckJlptLevel;
        SelectedDeck.Deck.CardType = DeckCardType;
        await _repository.SaveDeckAsync(SelectedDeck.Deck);
    }

    private async Task SaveMeaningEntryAsync(MeaningEntryViewModel meaning)
    {
        meaning.Model.Text = meaning.Text.Trim();
        meaning.Model.Style = meaning.Style;
        meaning.Model.IsPrimaryMeaning = meaning.IsPrimaryMeaning;
        await _repository.SaveMeaningAsync(meaning.Model);
        UpdateCardStatus();
        await RefreshDecksAsync();
    }

    private void QueueCardSave()
    {
        if (_isLoading || SelectedCard is null)
            return;

        _cardSaveDelay?.Cancel();
        _cardSaveDelay?.Dispose();
        _cardSaveDelay = new CancellationTokenSource();
        SaveStatus = "Saving changes…";
        UpdateCardStatus();
        _ = SaveCardAfterDelayAsync(_cardSaveDelay.Token);
    }

    private async Task SaveCardAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await SaveSelectedCardAsync();
            await RefreshDecksAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void QueueDeckSave()
    {
        if (_isLoading || SelectedDeck is null)
            return;

        _deckSaveDelay?.Cancel();
        _deckSaveDelay?.Dispose();
        _deckSaveDelay = new CancellationTokenSource();
        _ = SaveDeckAfterDelayAsync(_deckSaveDelay.Token);
    }

    private async Task SaveDeckAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(350, cancellationToken);
            await SaveSelectedDeckAsync();
            await RefreshDecksAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelScheduledSaves()
    {
        _cardSaveDelay?.Cancel();
        _deckSaveDelay?.Cancel();
    }

    private async Task RefreshDecksAsync(bool reorder = false)
    {
        if (!MainThread.IsMainThread)
        {
            await MainThread.InvokeOnMainThreadAsync(() => RefreshDecksAsync(reorder));
            return;
        }

        var persistedDecks = await _repository.GetDecksAsync();
        var existing = Decks.ToDictionary(item => item.Deck.Id);
        var currentIds = persistedDecks.Select(deck => deck.Id).ToHashSet();

        foreach (var obsolete in Decks.Where(item => !currentIds.Contains(item.Deck.Id)).ToArray())
            Decks.Remove(obsolete);

        foreach (var deck in persistedDecks)
        {
            var cards = await _repository.GetCardsAsync(deck.Id);
            var meaningsByCard = new Dictionary<int, IReadOnlyCollection<CardMeaning>>();
            foreach (var card in cards)
                meaningsByCard[card.Id] = await _repository.GetMeaningsAsync(card.Id);

            var frontComplete = cards.Count(FlashcardRules.IsFrontComplete);
            var backComplete = cards.Count(card => FlashcardRules.IsBackComplete(deck.CardType, card,
                meaningsByCard.GetValueOrDefault(card.Id, Array.Empty<CardMeaning>())));
            var progress = cards.Count == 0 ? 0 : (int)Math.Round(backComplete * 100d / cards.Count);
            var status = FlashcardRules.GetDeckStatus(cards, deck.CardType, meaningsByCard);

            if (existing.TryGetValue(deck.Id, out var item))
                item.Update(deck, cards.Count, frontComplete, backComplete, progress, status);
            else
                Decks.Add(new DeckOverview(deck)
                {
                    CardCount = cards.Count,
                    FrontCompleteCount = frontComplete,
                    BackCompleteCount = backComplete,
                    ProgressPercent = progress,
                    Status = status
                });
        }

        if (reorder)
        {
            for (var index = 0; index < persistedDecks.Count; index++)
            {
                var target = Decks.First(item => item.Deck.Id == persistedDecks[index].Id);
                var currentIndex = Decks.IndexOf(target);
                if (currentIndex != index)
                    Decks.Move(currentIndex, index);
            }
        }

        TotalCards = Decks.Sum(item => item.CardCount);
        CompletedDeckCount = Decks.Count(item => item.Status == DeckStatus.Completed);
        DeckCount = Decks.Count;
        HasDecks = Decks.Count > 0;
        RefreshFilteredDecks();
        OnPropertyChanged(nameof(Decks));
    }

    private void RefreshFilteredDecks()
    {
        var search = DeckSearchText.Trim();
        var selectedJlptLevels = JlptFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedCardTypes = CardTypeFilterOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hasSelectedFilters = selectedJlptLevels.Count > 0 || selectedCardTypes.Count > 0;
        var matches = Decks.Where(deck =>
            (string.IsNullOrEmpty(search)
                || deck.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || deck.JlptLevel.Contains(search, StringComparison.OrdinalIgnoreCase)
                || deck.CardType.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
            && (!hasSelectedFilters
                || selectedJlptLevels.Contains(deck.JlptLevel)
                || selectedCardTypes.Contains(deck.CardType.ToString())))
            .ToArray();
        var matchingDecks = matches.ToHashSet();

        for (var index = FilteredDecks.Count - 1; index >= 0; index--)
        {
            if (!matchingDecks.Contains(FilteredDecks[index]))
                FilteredDecks.RemoveAt(index);
        }

        for (var index = 0; index < matches.Length; index++)
        {
            if (index < FilteredDecks.Count && FilteredDecks[index] == matches[index])
                continue;

            var currentIndex = FilteredDecks.IndexOf(matches[index]);
            if (currentIndex >= 0)
                FilteredDecks.Move(currentIndex, index);
            else
                FilteredDecks.Insert(index, matches[index]);
        }

        while (FilteredDecks.Count > matches.Length)
            FilteredDecks.RemoveAt(FilteredDecks.Count - 1);

        HasNoSearchResults = HasDecks && matches.Length == 0;
    }

    private void UpdateCardStatus()
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(UpdateCardStatus);
            return;
        }

        OnPropertyChanged(nameof(CardDisplayNumber));
        OnPropertyChanged(nameof(CardStatusLabel));
        OnPropertyChanged(nameof(CardStatusColor));
        OnPropertyChanged(nameof(BackPreviewMeta));
        OnPropertyChanged(nameof(BackPreviewNumber));
        OnPropertyChanged(nameof(BackPreviewReading));
        OnPropertyChanged(nameof(FrontPreviewText));

        if (SelectedCard is null)
            return;

        var cardItem = Cards.FirstOrDefault(item => item.Card.Id == SelectedCard.Id);
        cardItem?.UpdateFrontText(FrontText);

        if (SelectedDeck is not null)
        {
            var status = FlashcardRules.GetStatus(DeckCardType, SelectedCard,
                Meanings.Select(meaning => meaning.Model).ToArray());
            cardItem?.UpdateStatus(status);
        }
    }

    partial void OnFrontTextChanged(string value)
    {
        QueueCardSave();
        OnPropertyChanged(nameof(FrontPreviewText));
    }

    partial void OnHiraganaChanged(string value)
    {
        QueueCardSave();
        OnPropertyChanged(nameof(BackPreviewReading));
    }

    partial void OnVerbKindChanged(VerbKind? value)
    {
        QueueCardSave();
        OnPropertyChanged(nameof(BackPreviewMeta));
        OnPropertyChanged(nameof(BackPreviewMetaColor));
    }

    partial void OnTransitivityChanged(VerbTransitivity? value)
    {
        QueueCardSave();
        OnPropertyChanged(nameof(BackPreviewMeta));
    }

    partial void OnAdverbClassificationChanged(AdverbClassification? value)
    {
        QueueCardSave();
        OnPropertyChanged(nameof(BackPreviewMeta));
        OnPropertyChanged(nameof(BackPreviewMetaColor));
    }
    partial void OnNounCategoryChanged(NounCategory? value) => QueueCardSave();
}
