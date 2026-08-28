using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.LocalNotification;

#if ANDROID
using Firebase.Messaging;
using FENS_Connect.Platforms.Android.Firebase;
#endif

namespace FENS_Connect
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification()
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
                        try
                        {
                            Firebase.FirebaseApp.InitializeApp(activity);

                             FirebaseMessaging.Instance.GetToken().AddOnCompleteListener(new FCMTokenListener());
                        }
                        catch (Exception ex)
                        {
                            // Fresh devices without a Google account / Play Services can fail here.
                            System.Diagnostics.Debug.WriteLine($"[FCM] Startup init skipped: {ex.Message}");
                        }
                    }));
#endif
                });

            // Register your platform-specific service implementation
#if ANDROID
            builder.Services.AddSingleton<IFirebaseAuthService, Platforms.Android.AndroidAuthService>();
            builder.Services.AddTransient<MapPage>();
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
