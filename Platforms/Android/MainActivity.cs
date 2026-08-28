using Android.App;
using Android.Content.PM;
using Android.OS;
using FENS_Connect.Platforms.Android.Firebase;

namespace FENS_Connect
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            CreateFriendAlertChannel();
        }

        void CreateFriendAlertChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var channel = new NotificationChannel(
                FensFirebaseMessagingService.AlertChannelId,
                "Friend Alerts",
                NotificationImportance.High)
            {
                Description = "Emergency alerts from FENS friends"
            };

            var manager = GetSystemService(NotificationService) as NotificationManager;
            manager?.CreateNotificationChannel(channel);
        }
    }
}
