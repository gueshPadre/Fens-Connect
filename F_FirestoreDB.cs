using Google.Cloud.Firestore;
using Grpc.Auth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace FENS_Connect
{
    internal class F_FirestoreDB
    {
        FirestoreDb myDb;

        // NAMES OF THE FIELDS ON THE FIRESTORE DB. SO CONST FOR THAT REASON
        const string DISPLAY_NAME = "DisplayName";


        /// <summary>
        /// Connects the user to the Firebase database for the first time and sets the display name
        /// </summary>
        public async Task ConnectUserToDb(string _uID, MapPage.UserProfile _user)
        {
            try
            {
                await ConnectToDb();

                var pRef = myDb.Collection("Users").Document(_uID);
                var pUser = new
                {
                    DisplayName = _user.DisplayName,
                    MyEmail = _user.UserEmail,
                    MyLocation = _user.UserLocation,
                    FriendList = _user.FriendList,
                };


                var r = await pRef.SetAsync(pUser);

                Debug.WriteLine($"[FB] WORKED {r.UpdateTime}");
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[FB] CRITICAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }

        }

        async Task ConnectToDb()
        {
            // 1. Get the current assembly where the JSON is embedded
            var assembly = Assembly.GetExecutingAssembly();

            // 2. Locate the resource name. 
            string resourceName = "FENS_Connect.fens-connect-db-firebase-adminsdk-fbsvc-f84005c877.json";

            string jsonCredentials = "";
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    // This helper will list all embedded resource names in your logs if it fails
                    Debug.WriteLine("[FB] ERROR: Could not find embedded resource. Available resources:");
                    foreach (var name in assembly.GetManifestResourceNames())
                    {
                        Debug.WriteLine($" - {name}");
                    }
                    return;
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    jsonCredentials = await reader.ReadToEndAsync();
                }
            }

            var channelCredentials = Google.Apis.Auth.OAuth2.GoogleCredential
                                    .FromJson(jsonCredentials)                 // Safely parses the text data
                                    .ToChannelCredentials();                   // Converts it into a connection channel

            // 3. Attach the channel credentials directly to the client builder
            var clientBuilder = new Google.Cloud.Firestore.V1.FirestoreClientBuilder
            {
                ChannelCredentials = channelCredentials
            };

            myDb = FirestoreDb.Create("fens-connect-db", clientBuilder.Build());
        }


        public async Task<MapPage.UserProfile> RetrieveUserInfo(string _uID)
        {
            await ConnectToDb();
            var pDocRef = myDb.Collection("Users").Document(_uID);

            var pUser = await pDocRef.GetSnapshotAsync();

            Dictionary<string, object> pDict = pUser.ToDictionary();

            var myUser = new MapPage.UserProfile();

            var name = pDict[DISPLAY_NAME].ToString();
            if(name != null) myUser.DisplayName = name;

            var email = pDict["MyEmail"].ToString();
            if (name != null) myUser.DisplayName = name;

            GeoPoint myLoc = (GeoPoint)pDict["MyLocation"];
            myUser.UserLocation = myLoc;

            return myUser;
        }

    }
}