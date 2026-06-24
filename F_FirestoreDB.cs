using Google.Cloud.Firestore;
using Grpc.Auth;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FENS_Connect
{
    internal class F_FirestoreDB
    {
        string credentialPath = @"C:\Users\guesh\Documents\GamesRelated\mApp\FENS-Connect\fens-connect-db-firebase-adminsdk-fbsvc-f84005c877.json";
        //Environment.SetEnvironmentVariable()


        //public F_FirestoreDB Instance { get; private set; }

        //public F_FirestoreDB()
        //{
        //    Instance = new F_FirestoreDB();
        //}


        /// <summary>
        /// Connects the user to the Firebase database for the first time and sets the display name
        /// </summary>
        public async Task ConnectUserToDb(string _uID, MapPage.UserProfile _user)
        {
            try
            {
                // 1. Get the current assembly where the JSON is embedded
                //var assembly = IntrospectionExtensions.GetTypeInfo(typeof(F_FirestoreDB)).Assembly;
                var assembly = Assembly.GetExecutingAssembly();

                // 2. Locate the resource name. 
                string resourceName = "FENS_Connect.fens-connect-db-firebase-adminsdk-fbsvc-f84005c877.json";

                string jsonCredentials = "";
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        // This helper will list all embedded resource names in your logs if it fails
                        Debug.WriteLine("[FIREBASE] ERROR: Could not find embedded resource. Available resources:");
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

                //string credentialPath = AppDomain.CurrentDomain.BaseDirectory + "fens-connect-db-firebase-adminsdk-fbsvc-f84005c877.json";
                //Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);

                FirestoreDb pDb = FirestoreDb.Create("fens-connect-db",clientBuilder.Build());
                //FirestoreDb pDb = new FirestoreDbBuilder
                //{
                //    ProjectId = "fens-connect-db",
                //    CredentialsPath = jsonCredentials,
                //}.Build();

                var pRef = pDb.Collection("Users").Document(_uID);
                var pUser = new
                {
                    DisplayName = _user.DisplayName,
                };


                var r = await pRef.SetAsync(pUser);

                Debug.WriteLine($"[FIREBASE] WORKED {r.UpdateTime}");
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"[FIREBASE] CRITICAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FIREBASE] Inner Error: {ex.InnerException.Message}");
                }
            }

        }

    }
}