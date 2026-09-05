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

        async Task ConnectToDb()
        {
            if (myDb != null) return;
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


        /// <summary>
        /// Connects the user to the Firebase database for the first time and sets the display name
        /// </summary>
        public async Task ConnectUserToDb(string _uID, MapPage.UserProfile _user, bool _needToUpdate = false)
        {
            try
            {
                await ConnectToDb();

                var pRef = myDb.Collection("Users").Document(_uID);
                var pUser = new
                {
                    Name = _user.Name,
                    DisplayName = _user.DisplayName,
                    MyEmail = _user.UserEmail,
                    MyLocation = _user.UserLocation,
                };
                if (_needToUpdate)
                    await pRef.SetAsync(pUser, SetOptions.MergeAll);
                else
                    await pRef.SetAsync(pUser);


                var pFriendRef = pRef.Collection("FriendList");
                foreach (var f in _user.FriendList)
                {
                    var frndData = new
                    {
                        Name = f.Name,
                        IsCloseFriend = f.isCloseFriend,
                        DisplayName = f.DisplayName,
                        uID = f.UniqueId,
                    };

                    if (_needToUpdate)
                        await pFriendRef.Document(f.Name).SetAsync(frndData, SetOptions.MergeAll);
                    else
                        await pFriendRef.Document(f.Name).SetAsync(frndData);
                }

                var pSettingsRef = pRef.Collection("UserSettings");
                if(_user.UserSettings == null)
                {
                    _user.UserSettings = new Dictionary<string, bool>();
                    _user.UserSettings[MapPage.UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY] = true; // default value
                }
                var pSettingsData = new
                {
                    AlertsShownToFriends = _user.UserSettings[MapPage.UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY]
                };
                await pSettingsRef.Document("GeneralSettings").SetAsync(pSettingsData, SetOptions.MergeAll);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] CRITICAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }

        }


        public async Task<MapPage.UserProfile> RetrieveUserInfo(string _uID)
        {
            await ConnectToDb();
            var pDocRef = myDb.Collection("Users").Document(_uID);

            var pUser = await pDocRef.GetSnapshotAsync();

            Dictionary<string, object> pDict = pUser.ToDictionary();

            var myUser = new MapPage.UserProfile();

            var name = pDict["DisplayName"].ToString();
            if (name != null) myUser.DisplayName = name;

            var pName = pDict["Name"].ToString();
            if (pName != null) myUser.Name = pName;

            var email = pDict["MyEmail"].ToString();
            if (email != null) myUser.UserEmail = email;

            GeoPoint myLoc = (GeoPoint)pDict["MyLocation"];
            myUser.UserLocation = myLoc;

            var pFriendSnapShot = pDocRef.Collection("FriendList");
            var pFriendL = await pFriendSnapShot.GetSnapshotAsync();
            foreach (var doc in pFriendL.Documents)
            {
                MapPage.FriendInfo myUserFriendProfile = new MapPage.FriendInfo();
                Dictionary<string, object> docDict = doc.ToDictionary();
                myUserFriendProfile.Name = (string)docDict["Name"];
                myUserFriendProfile.DisplayName = (string)docDict["DisplayName"];
                myUserFriendProfile.isCloseFriend = (bool)docDict["IsCloseFriend"];
                myUserFriendProfile.UniqueId = (string)docDict["uID"];

                myUser.FriendList.Add(myUserFriendProfile);
            }

            var pSettingsSnapShot = await pDocRef.Collection("UserSettings").GetSnapshotAsync();

            foreach (var set in pSettingsSnapShot.Documents)
            {
                Dictionary<string, object> setDict = set.ToDictionary();
                myUser.UserSettings[MapPage.UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY] = (bool)setDict["AlertsShownToFriends"];
            }

            return myUser;
        }


        public async void UpdateSettings(string _uID)
        {
            try
            {
                if (myDb == null)
                {
                    await ConnectToDb();
                }

                var pRef = myDb.Collection("Users").Document(_uID);

                var pSettingsRef = pRef.Collection("UserSettings");
                // Initialize the variable on the DB. just set to true by default
                var pSettingsData = new
                {
                    AlertsShownToFriends = true
                };
                await pSettingsRef.Document("GeneralSettings").SetAsync(pSettingsData, SetOptions.MergeAll);
            }
            catch (Exception ex) 
            {
                Debug.WriteLine($"[FB] ERROR in updating settings: {ex.Message}");
                throw; // rethrow the exception to be handled by the caller
            }
        }

        /// <summary>
        /// With these parameters, the database updates the settings with the settings of the existing user
        /// </summary>
        /// <param name="_uID"></param>
        /// <param name="_user"></param>
        public async void UpdateSettings(string _uID, MapPage.UserProfile _user)
        {
            if(myDb == null)
            {
                await ConnectToDb();
            }
            var pRef = myDb.Collection("Users").Document(_uID);

            var pSettingsRef = pRef.Collection("UserSettings");
            // Initialize the variable on the DB. just set to true by default
            var pSettingsData = new
            {
                AlertsShownToFriends = _user.UserSettings[MapPage.UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY]
            };
            await pSettingsRef.Document("GeneralSettings").SetAsync(pSettingsData, SetOptions.MergeAll);
        }


        public async Task AddAlertToDb(string _userID, MapPage.AlertMarkerInfo _alertID)
        {
            try
            {

                await ConnectToDb();
                var pDocRef = myDb.Collection("Users").Document(_userID);

                var pAlerts = pDocRef.Collection("Alerts");

                var pUserAlert = new
                {
                    alertID = _alertID.MarkerID,
                    Note = _alertID.Note,
                    AlertOwner = _alertID.CreatedBy,
                    alertLocation = _alertID.alertLoc,
                    UserId = _alertID.UserID,
                    AvailableToFriends = _alertID.AvailableToFriends
                };

                await pAlerts.Document(_alertID.MarkerID).SetAsync(pUserAlert, SetOptions.MergeAll);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] CRITICAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }

            }
        }


        public async Task<List<MapPage.AlertMarkerInfo>> RetrieveAlertMarkers(string _userID, bool _checkFriend = false, bool _includeNonAvailables = false)
        {
            List<MapPage.AlertMarkerInfo> alerts = new List<MapPage.AlertMarkerInfo>();
            try
            {
                await ConnectToDb();
                var pDocRef = myDb.Collection("Users").Document(_userID);
                var pAlerts = pDocRef.Collection("Alerts");
                var pAlertList = await pAlerts.GetSnapshotAsync();
                foreach (var doc in pAlertList.Documents)
                {
                    Dictionary<string, object> docDict = doc.ToDictionary();
                    string alertID = (string)docDict["alertID"];
                    string note = (string)docDict["Note"];
                    string createdBy = (string)docDict["AlertOwner"];
                    GeoPoint alertLoc = (GeoPoint)docDict["alertLocation"];
                    string userID = (string)docDict["UserId"];
                    bool availableToFriends = (bool)docDict["AvailableToFriends"];

                    if (!_includeNonAvailables)  // If we're not checking friends, we only want to add alerts that are available from our friends
                    {
                        if(availableToFriends == false)
                        {
                            continue; // Skip this alert if it's not available to friends
                        }
                    }
                    //Debug.WriteLine($"[FB] Retrieved Alert ID: {alertID}, and {note}");

                    alerts.Add(new MapPage.AlertMarkerInfo
                    {
                        MarkerID = alertID,
                        Note = note,
                        CreatedBy = createdBy,
                        alertLoc = alertLoc,
                        UserID = userID,
                        AvailableToFriends = availableToFriends
                    });
                }

                if (_checkFriend)
                {
                    List<string> friendIds = new List<string>();
                    // check & Get Friends alerts
                    var pFriendSnapShot = pDocRef.Collection("FriendList");
                    var pFriendL = await pFriendSnapShot.GetSnapshotAsync();
                    foreach (var doc in pFriendL.Documents)
                    {
                        if (!doc.Exists) continue;
                        Dictionary<string, object> docDict = doc.ToDictionary();
                        friendIds.Add((string)docDict["uID"]);
                    }

                    // Recursively retrieve alerts from friends
                    foreach (var friendId in friendIds)
                    {
                        var frndAlerts = await RetrieveAlertMarkers(friendId);
                        alerts.AddRange(frndAlerts);

                    }

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] CRITICAL ERROR in retrieving alerts: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }
            return alerts;
        }

        public async Task RemoveAlert(string _userID, string _alertID)
        {
            DocumentReference pDocRef = myDb.Collection("Users").Document(_userID).
                Collection("Alerts").Document(_alertID);
            try
            {
                await pDocRef.DeleteAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[FB] NOT DELETED? {e.Message}");
            }
        }

        /// <summary>
        /// For now it's searching and automatically adding. Will change to make them separate
        /// </summary>
        /// <param name="_friendName">The Display Name we're searching for</param>
        /// <returns>List of users found by ID (string)</returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<string>> SearchFriend(string _friendName, bool _isCloseFriend)
        {
            try
            {
                List<string> foundPplId = new List<string>();
                await ConnectToDb();
                var pUsers = myDb.Collection("Users");
                var pQuery = await pUsers.GetSnapshotAsync();
                foreach (var doc in pQuery.Documents)
                {
                    Dictionary<string, object> docDict = doc.ToDictionary();
                    var pDN = (string)docDict["DisplayName"];
                    if (pDN == _friendName)
                    {
                        foundPplId.Add(doc.Id);
                    }
                }
                return foundPplId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] ERROR searching friend: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }
            return new List<string>(); // return empty list if no users found
            //throw new Exception("[FB] No user found with the name: {_friendName}");
        }

        public async void SendRequest(string _userID, string _friendID, string _friendDN, string _friendName, bool _isCloseFrnd)
        {
            try
            {
                // Add the request on the firend's side
                var pDocRef = myDb.Collection("Users").Document(_friendID);
                var pFriendReq = pDocRef.Collection("FriendRequests");
                Timestamp time = Timestamp.FromDateTime(DateTime.UtcNow);
                var pRequest = new
                {
                    fromID = _userID,
                    fromDisplayName = _friendDN,
                    fromName = _friendName,
                    RequestTime = time
                };
                await pFriendReq.Document(_userID).SetAsync(pRequest, SetOptions.MergeAll);

                //Add the request on the user's side
                var pUserRef = myDb.Collection("Users").Document(_userID);
                var pReq = pUserRef.Collection("friendRqstSent");

                var pRequestSent = new
                {
                    toID = _friendID,
                    isCloseFriend = _isCloseFrnd
                };
                await pReq.Document(_friendID).SetAsync(pRequestSent, SetOptions.MergeAll);

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] ERROR sending friend request: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }
        }

        public async Task<List<MapPage.FriendRequest>> GetFriendRequests(string _userID)
        {
            try
            {
                List<MapPage.FriendRequest> friendRequests = new List<MapPage.FriendRequest>();
                var pDocRef = myDb.Collection("Users").Document(_userID);
                var pFriendReq = pDocRef.Collection("FriendRequests");

                var pReq = await pFriendReq.GetSnapshotAsync();
                foreach (var r in pReq.Documents)
                {
                    MapPage.FriendRequest myRequest = new MapPage.FriendRequest();
                    Dictionary<string, object> docDict = r.ToDictionary();
                    myRequest.SenderDisplayName = (string)docDict["fromDisplayName"];
                    myRequest.SenderId = (string)docDict["fromID"];
                    myRequest.SenderName = (string)docDict["fromName"];

                    friendRequests.Add(myRequest);
                }

                return friendRequests;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FB] ERROR getting friend requests: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                }
            }
            return new List<MapPage.FriendRequest>(); // return empty list if no requests found
        }


        /// <summary>
        /// Retrieves the Display Name and Email of a user by their ID. 
        /// Not fetching the entire profile cuz no need
        /// </summary>
        /// <param name="_id"></param>
        /// <returns></returns>
        public async Task<MapPage.UserProfile> GetNameAndEmail(string _id)
        {
            var pDocRef = myDb.Collection("Users").Document(_id);
            var pUser = await pDocRef.GetSnapshotAsync();
            Dictionary<string, object> pDict = pUser.ToDictionary();
            var myUser = new MapPage.UserProfile();
            var name = pDict["Name"].ToString();
            if (name != null) myUser.Name = name;
            var email = pDict["MyEmail"].ToString();
            if (email != null) myUser.UserEmail = email;
            return myUser;
        }

        public async Task<MapPage.FriendInfo> AddFriend(string _friendID, bool _isCloseFriend, string _userID)
        {
            // Get Friend Info
            try
            {
                var pUsers = myDb.Collection("Users");
                var pQ = await pUsers.Document(_friendID).GetSnapshotAsync();
                MapPage.FriendInfo myUserFriendProfile = new MapPage.FriendInfo();
                Dictionary<string, object> docDict = pQ.ToDictionary();

                myUserFriendProfile.Name = (string)docDict["Name"];
                myUserFriendProfile.DisplayName = (string)docDict["DisplayName"];
                myUserFriendProfile.isCloseFriend = _isCloseFriend;
                myUserFriendProfile.UniqueId = _friendID;


                var pFL = pUsers.Document(_userID).Collection("FriendList");
                var p = new
                {
                    Name = myUserFriendProfile.Name,
                    DisplayName = myUserFriendProfile.DisplayName,
                    IsCloseFriend = myUserFriendProfile.isCloseFriend,
                    uID = myUserFriendProfile.UniqueId
                };

                await pFL.Document(myUserFriendProfile.Name).SetAsync(p, SetOptions.MergeAll);
                return myUserFriendProfile;
            }
            catch (Exception ex)
            {
                throw new Exception($"[FB] ERROR adding friend: {ex.Message}");
            }
        }

        public async Task UpdateCloseFriendStatus(string _userID, string _friendName, bool _isCloseFriend)
        {
            try
            {
                var pUsers = myDb.Collection("Users");
                var pFL = pUsers.Document(_userID).Collection("FriendList");
                var p = new
                {
                    IsCloseFriend = _isCloseFriend
                };
                await pFL.Document(_friendName).SetAsync(p, SetOptions.MergeAll);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[FB] ERROR updating close friend status: {e.Message}");
            }
        }

        public async Task AddFriendForOtherUser(string _senderID, string _userID, string _Name, string _DN)
        {
            try
            {
                // get the close friend status from the sender's friend request sent collection
                MapPage.FriendInfo friendUserFriendProfile = new MapPage.FriendInfo();
                var pUsers = myDb.Collection("Users");
                var pReqColl = pUsers.Document(_senderID).Collection("friendRqstSent");
                var pCollQ = await pReqColl.Document(_userID).GetSnapshotAsync();

                Dictionary<string, object> docDict = pCollQ.ToDictionary();
                friendUserFriendProfile.isCloseFriend = (bool)docDict["isCloseFriend"];

                // Add the friend to the senders friendlist
                var pFL = pUsers.Document(_senderID).Collection("FriendList");
                var p = new
                {
                    Name = _Name,
                    DisplayName = _DN,
                    IsCloseFriend = friendUserFriendProfile.isCloseFriend,
                    uID = _userID
                };

                await pFL.Document(_Name).SetAsync(p, SetOptions.MergeAll);

            }
            catch (Exception e)
            {
                Debug.WriteLine($"[FB] ERRORR SOMEWHERE: {e.Message}");
            }
        }


        /// <summary>
        /// Removes the requests from both the sender and the user. This is called after a friend request is accepted or rejected.
        /// </summary>
        /// <param name="_senderID"></param>
        /// <param name="_userID"></param>
        /// <returns></returns>
        public async Task RemoveRequests(string _senderID, string _userID)
        {
            // Remove the request from the user's side
            var pUsers = myDb.Collection("Users");
            await pUsers.Document(_userID).Collection("FriendRequests").Document(_senderID).DeleteAsync();

            //Remove the request from the sender's side
            await pUsers.Document(_senderID).Collection("friendRqstSent").Document(_userID).DeleteAsync();
        }


        // Remove friend from db
        public async Task RemoveFriend(string _userID, string _FullName)
        {
            DocumentReference pDocRef = myDb.Collection("Users").Document(_userID)
                .Collection("FriendList").Document(_FullName);
            try
            {
                await pDocRef.DeleteAsync();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[FB] NOT DELETED? {e.Message}");
            }
        }

        /// <summary>
        /// Saves or updates this device's FCM token under Users/{userId}/Devices/{deviceId}.
        /// </summary>
        public async Task SaveDeviceTokenAsync(string userId, string deviceId, string token, string platform = "android")
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(deviceId))
                return;

            try
            {
                await ConnectToDb();
                var deviceRef = myDb.Collection("Users").Document(userId)
                    .Collection("Devices").Document(deviceId);

                await deviceRef.SetAsync(new Dictionary<string, object>
                {
                    { "token", token },
                    { "platform", platform },
                    { "updatedAt", Timestamp.GetCurrentTimestamp() }
                }, SetOptions.MergeAll);

                Debug.WriteLine($"[FCM] Saved device token for user {userId} device {deviceId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] Failed to save device token: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a cloud-managed friend alert request. A Cloud Function sends FCM — the client never sends push directly.
        /// </summary>
        public async Task<string?> CreateFriendAlertRequestAsync(
            string fromUserId,
            double lat,
            double lng,
            string? targetFriendId = null,
            string type = "friend_sos")
        {
            if (string.IsNullOrWhiteSpace(fromUserId))
                return null;

            try
            {
                await ConnectToDb();
                var requestRef = myDb.Collection("AlertRequests").Document();
                var data = new Dictionary<string, object>
                {
                    { "fromUserId", fromUserId },
                    { "createdAt", Timestamp.GetCurrentTimestamp() },
                    { "type", type },
                    { "lat", lat },
                    { "lng", lng },
                    { "status", "pending" }
                };

                if (!string.IsNullOrWhiteSpace(targetFriendId))
                    data["targetFriendId"] = targetFriendId;

                await requestRef.SetAsync(data);
                Debug.WriteLine($"[FCM] AlertRequest created: {requestRef.Id}");
                return requestRef.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] Failed to create AlertRequest: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a cloud-managed friend request Alert. A Cloud Function sends FCM — the client never sends push directly.
        /// </summary>
        public async Task<string?> CreateFriendRequestAlertRequestAsync(
            string fromUserId,
            double lat,
            double lng,
            string? targetFriendId = null,
            string type = "friendRequest")
        {
            if (string.IsNullOrWhiteSpace(fromUserId))
                return null;

            try
            {
                await ConnectToDb();
                var requestRef = myDb.Collection("AlertRequests").Document();
                var data = new Dictionary<string, object>
                {
                    { "fromUserId", fromUserId },
                    { "createdAt", Timestamp.GetCurrentTimestamp() },
                    { "type", type },
                    { "lat", lat },
                    { "lng", lng },
                    { "status", "pending" }
                };

                if (!string.IsNullOrWhiteSpace(targetFriendId))
                    data["targetFriendId"] = targetFriendId;

                await requestRef.SetAsync(data);
                Debug.WriteLine($"[FCM] AlertRequest created: {requestRef.Id}");
                return requestRef.Id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] Failed to create AlertRequest: {ex.Message}");
                return null;
            }
        }


    }
}