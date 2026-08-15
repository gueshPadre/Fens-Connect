using System.Diagnostics;

#if ANDROID
using Android.Gms.Tasks;
using Android.Provider;
using Firebase.Auth;
using Firebase.Messaging;
using Task = System.Threading.Tasks.Task;
#endif

namespace FENS_Connect;

/// <summary>
/// Registers the current device FCM token in Firestore after login / token refresh.
/// </summary>
public static class FcmDeviceRegistration
{
    const string DeviceIdPreferenceKey = "fcm_device_id";

    public static string GetOrCreateDeviceId()
    {
        var existing = Preferences.Default.Get(DeviceIdPreferenceKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
            return existing;

#if ANDROID
        var androidId = Settings.Secure.GetString(
            global::Android.App.Application.Context.ContentResolver,
            Settings.Secure.AndroidId);
        if (!string.IsNullOrEmpty(androidId))
        {
            Preferences.Default.Set(DeviceIdPreferenceKey, androidId);
            return androidId;
        }
#endif
        var generated = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(DeviceIdPreferenceKey, generated);
        return generated;
    }

    public static async Task RegisterAsync(string? userId = null)
    {
#if ANDROID
        try
        {
            userId ??= FirebaseAuth.Instance?.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[FCM] Skip token save — no logged-in user");
                return;
            }

            var tokenTask = FirebaseMessaging.Instance.GetToken();
            var token = await ToAwaitable(tokenTask);
            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[FCM] GetToken returned empty");
                return;
            }

            var deviceId = GetOrCreateDeviceId();
            var db = new F_FirestoreDB();
            await db.SaveDeviceTokenAsync(userId, deviceId, token, "android");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FCM] RegisterAsync failed: {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }

    public static async Task SaveTokenAsync(string token, string? userId = null)
    {
#if ANDROID
        try
        {
            userId ??= FirebaseAuth.Instance?.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return;

            var deviceId = GetOrCreateDeviceId();
            var db = new F_FirestoreDB();
            await db.SaveDeviceTokenAsync(userId, deviceId, token, "android");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FCM] SaveTokenAsync failed: {ex.Message}");
        }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    static Task<string?> ToAwaitable(Android.Gms.Tasks.Task javaTask)
    {
        var tcs = new TaskCompletionSource<string?>();
        javaTask.AddOnCompleteListener(new TokenCompleteListener(tcs));
        return tcs.Task;
    }

    sealed class TokenCompleteListener : Java.Lang.Object, IOnCompleteListener
    {
        readonly TaskCompletionSource<string?> _tcs;

        public TokenCompleteListener(TaskCompletionSource<string?> tcs) => _tcs = tcs;

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (!task.IsSuccessful)
            {
                _tcs.TrySetResult(null);
                return;
            }

            _tcs.TrySetResult(task.Result?.ToString());
        }
    }
#endif
}
