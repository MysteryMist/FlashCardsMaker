namespace FlashCardsMaker
{
    public partial class MainPage : ContentPage
    {
        private readonly ViewModels.MainViewModel _viewModel;
        private bool _initialized;
        private ScrollView? MainScrollView => Content as ScrollView;

        public MainPage(ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = _viewModel = viewModel;
            _viewModel.MeaningAdding += CaptureMeaningScrollPosition;
            _viewModel.MeaningAdded += RestoreMeaningScrollPosition;
        }

        private double _meaningScrollY;

        private void CaptureMeaningScrollPosition(object? sender, EventArgs e)
        {
            _meaningScrollY = MainScrollView?.ScrollY ?? 0;
        }

        private async void RestoreMeaningScrollPosition(object? sender, EventArgs e)
        {
            await Task.Delay(100);
            if (MainScrollView is { } scrollView)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    scrollView.ScrollToAsync(0, _meaningScrollY, false));
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;

            _initialized = true;
            await _viewModel.InitializeAsync();
        }
    }
}
