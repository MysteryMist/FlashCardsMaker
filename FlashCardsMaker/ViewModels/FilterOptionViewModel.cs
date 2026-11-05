using CommunityToolkit.Mvvm.ComponentModel;

namespace FlashCardsMaker.ViewModels;

public partial class FilterOptionViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    public FilterOptionViewModel(string value, Action selectionChanged)
    {
        Value = value;
        _selectionChanged = selectionChanged;
    }

    public string Value { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                _selectionChanged();
        }
    }
}
