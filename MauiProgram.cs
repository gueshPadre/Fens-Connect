using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace FENS_Connect
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                }).ConfigureLifecycleEvents(events =>
                {
#if ANDROID
                    events.AddAndroid(android => android.OnCreate((activity, bundle) =>
                    {
                        // This initializes the native Android Firebase instance using your JSON file
                        Firebase.FirebaseApp.InitializeApp(activity);
                    }));
#endif
                });

            // Register your platform-specific service implementation
#if ANDROID
            builder.Services.AddSingleton<IFirebaseAuthService, Platforms.Android.AndroidAuthService>();
            builder.Services.AddTransient<App>();
#else
    // Fallback stub for Windows/iOS so compilation won't crash when running mock states
    //builder.Services.AddSingleton<IFirebaseAuthService, MockAuthService>(); 
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
