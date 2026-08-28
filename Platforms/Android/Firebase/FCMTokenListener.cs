using Android.Gms.Tasks;
using Firebase.Auth;
using System.Diagnostics;
using Task = Android.Gms.Tasks.Task;

namespace FENS_Connect.Platforms.Android.Firebase
{
    public class FCMTokenListener : Java.Lang.Object, IOnCompleteListener
    {
        public void OnComplete(Task task)
        {
            if (!task.IsSuccessful)
            {
                Debug.WriteLine($"[FCM] token failed: {task.Exception}");
                return;
            }

            var token = task.Result?.ToString();
            Debug.WriteLine($"[FCM] TOKEN: {token}");

            if (string.IsNullOrEmpty(token))
                return;

            var userId = FirebaseAuth.Instance?.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(userId))
            {
                Debug.WriteLine("[FCM] Token ready but user not logged in yet — will save after login");
                return;
            }

            _ = FcmDeviceRegistration.SaveTokenAsync(token, userId);
        }
    }
}