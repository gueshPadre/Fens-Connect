using Android.App;
using Android.Content;
using Android.OS;
using Firebase.Auth;
using Firebase.Messaging;
using SysDebug = System.Diagnostics.Debug;

namespace FENS_Connect.Platforms.Android.Firebase;

[Service(
    Exported = false,
    Name = "com.fensconnect.FensFirebaseMessagingService"
)]
[IntentFilter(new[]
{
    "com.google.firebase.MESSAGING_EVENT"
})]
public class FensFirebaseMessagingService : FirebaseMessagingService
{
    public const string AlertChannelId = "fens_friend_alerts";
    const int NotificationIdBase = 2000;

    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        SysDebug.WriteLine($"[FCM] OnNewToken: {token}");

        var user = FirebaseAuth.Instance?.CurrentUser;
        if (user == null)
        {
            SysDebug.WriteLine("[FCM] No user logged in — token will be saved after login");
            return;
        }

        _ = FcmDeviceRegistration.SaveTokenAsync(token, user.Uid);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        var title = message.GetNotification()?.Title
            ?? (message.Data.TryGetValue("title", out var dataTitle) ? dataTitle : "FENS Alert");
        var body = message.GetNotification()?.Body
            ?? (message.Data.TryGetValue("body", out var dataBody) ? dataBody : "A friend needs help");

        ShowNotification(title, body, message.Data);
    }

    void ShowNotification(string title, string body, IDictionary<string, string> data)
    {
        EnsureChannel();

        var intent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        intent?.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);

        var pendingIntent = PendingIntent.GetActivity(
            this,
            0,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(this, AlertChannelId)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyChat)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .SetPriority((int)NotificationPriority.High);

        if (data != null && data.TryGetValue("lat", out var lat) && data.TryGetValue("lng", out var lng))
        {
            builder.SetStyle(new Notification.BigTextStyle().BigText($"{body}\nLocation: {lat}, {lng}"));
        }

        var manager = NotificationManager.FromContext(this);
        manager?.Notify(NotificationIdBase + Random.Shared.Next(0, 999), builder.Build());
    }

    void EnsureChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(
            AlertChannelId,
            "Friend Alerts",
            NotificationImportance.High)
        {
            Description = "Emergency alerts from FENS friends"
        };

        var manager = NotificationManager.FromContext(this);
        manager?.CreateNotificationChannel(channel);
    }
}
