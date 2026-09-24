using Android.App;
using Android.Content.PM;
using Android.OS;
using CommunityToolkit.Mvvm.Messaging;
using FENS_Connect.Messages;
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

            HandleIntent(Intent);
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

        private void HandleIntent(Android.Content.Intent? intent)
        {
            if (intent == null)
                return;

            var alertId = intent.GetStringExtra("alertId");
            var type = intent.GetStringExtra("type");

            // When notification is tapped, the app is launched with the intent containing the alertId and type.
            if (!string.IsNullOrEmpty(alertId))
            {
                System.Diagnostics.Debug.WriteLine($"🔥 Notification tap!");
                System.Diagnostics.Debug.WriteLine($"Type: {type}");
                System.Diagnostics.Debug.WriteLine($"Alert ID: {alertId}");

                // Tell MAUI/app navigation about it
                
                WeakReferenceMessenger.Default.Send(new FriendAlertMessage(new Dictionary<string, string>
                {
                    { "alertId", alertId },
                    { "type", type ?? string.Empty }
                }));
            }
        }

        protected override void OnNewIntent(Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

    }
}
