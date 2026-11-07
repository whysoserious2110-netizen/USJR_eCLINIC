using Microsoft.Extensions.Logging;

namespace USJR_eCLINIC
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoAndroidEntryUnderline", (handler, view) =>
            {
                handler.PlatformView.Background = null;
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("NoAndroidEditorUnderline", (handler, view) =>
            {
                handler.PlatformView.Background = null;
            });
#endif

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
