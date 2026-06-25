#if ANDROID
using System.Diagnostics;
using System.Threading.Tasks;
using Android.Gms.Tasks;
using Firebase.Auth;
using Task = Android.Gms.Tasks.Task;


namespace FENS_Connect.Platforms.Android
{
    public class AndroidAuthService : Java.Lang.Object, IFirebaseAuthService, IOnCompleteListener
    {
        private readonly FirebaseAuth _auth;
        private TaskCompletionSource<string> _tcs;

        public AndroidAuthService()
        {
            // Accesses the native Firebase Auth instance initialized in MauiProgram.cs
            _auth = FirebaseAuth.Instance;
        }

        public string GetCurrentUserId()
        {
            var user = _auth.CurrentUser;

            return user?.Uid;
        }

        public void SignOut()
        {
            _auth.SignOut();
        }

        /// <summary>
        /// Creates a User with an email and a password
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <returns>The unique Firebase Identifier</returns>
        public async Task<string> SetEmailPassword(string email, string password)
        {
            var r = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
            return r.User.Uid;
        }

        public Task<string> GetEmailPasswordAsync(string email, string password)
        {
            _tcs = new TaskCompletionSource<string>();

            // Native Android Task execution requires a listener to convert Java Tasks to .NET Tasks

            _auth.SignInWithEmailAndPassword(email, password)
                 .AddOnCompleteListener(this);

            return _tcs.Task;
        }

        // This method handles the response callback from the native Java SDK
        public void OnComplete(Task task)
        {
            if (task.IsSuccessful)
            {
                var user = _auth.CurrentUser;
                _tcs.SetResult(user?.Uid ?? "Success");
            }
            else
            {
                _tcs.SetException(task.Exception ?? new System.Exception("Authentication failed."));
            }
        }
    }
}
#endif
