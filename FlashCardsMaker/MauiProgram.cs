using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using FlashCardsMaker.Data;
using FlashCardsMaker.Export;
using FlashCardsMaker.ViewModels;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace FlashCardsMaker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            QuestPDF.Settings.UseSystemFonts = true;
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
            QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IDeckRepository, SqliteDeckRepository>();
            builder.Services.AddSingleton<IFlashcardExporter, FlashcardExporter>();
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
