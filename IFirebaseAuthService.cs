using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FENS_Connect
{
    public interface IFirebaseAuthService
    {
        Task<string> GetEmailPasswordAsync(string email, string password);

        Task<string> SetEmailPassword(string email, string password);

        string GetCurrentUserId();

        void SignOut();
    }
}
