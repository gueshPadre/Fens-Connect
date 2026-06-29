using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Google.Cloud.Firestore;

namespace FENS_Connect;

//[QueryProperty(nameof(City), "city")]
public partial class MapPage : ContentPage
{
    public class UserProfile
    {
        public string DisplayName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public List<FriendInfo> FriendList { get; set; } = new List<FriendInfo>();

        public GeoPoint UserLocation { get; set; } = new GeoPoint();

    }


    public class LocationMarkers
    {
        public string Name { get; set; } = string.Empty;
        public double Lng { get; set; }
        public double Lat { get; set; }
    }

    public class RouteStep
    {
        public string Instruction { get; set; } = string.Empty;
        public string Distance { get; set; } = string.Empty;
    }

    public class FriendInfo
    {
        private bool _isCloseFriend;
        private string starImgSource = "star.png";
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UniqueId { get; set; } = string.Empty;
        public string StarImgSource { get => starImgSource; set => starImgSource = value; }
        public bool isCloseFriend
        {
            get => _isCloseFriend;
            set
            {
                if (value)
                {
                    _isCloseFriend = value;
                    // change star to yellow
                    starImgSource = "staryellow.png"; // Change to yellow star image
                }
                else
                {
                    _isCloseFriend = false;
                    starImgSource = "star.png"; // Change back to default star image
                }
            }
        }

    }

    private readonly IFirebaseAuthService _authService;
    private F_FirestoreDB _db;

    // TODO: Hide the token key in production, this is just for testing purposes
    string Token = "pk.eyJ1IjoiZ3Vlc2giLCJhIjoiY21wZG9sdWFqMGRyYzJ6bzgyOWc3ZmdwMyJ9.mqd_v91FCsLiCVizOxLT9g";

    public ObservableCollection<RouteStep> RouteSteps { get; set; } = new ObservableCollection<RouteStep>();
    public ObservableCollection<FriendInfo> FriendsListCollection { get; set; } = new ObservableCollection<FriendInfo>();


    string _city = string.Empty;
    List<LocationMarkers> businessDict = new List<LocationMarkers>();
    Location currentLoc;
    string currentCity = string.Empty;
    LocationMarkers closestLoc;

    LocationMarkers navigatingBusiness;     // the business that the user is currently navigating to, used to update the directions
    public static UserProfile CurrentUser { get; private set; }


    public bool IsAlone { get; set; }
    public string City
    {
        get => _city;
        set
        {
            _city = Uri.UnescapeDataString(value ?? string.Empty);
        }
    }

    string currentFirebaseUserId;

    public MapPage(IFirebaseAuthService authService)
    {
        InitializeComponent();

        currentFirebaseUserId = authService.GetCurrentUserId();

        _db = new F_FirestoreDB();

        BindingContext = this;
        _authService = authService;

        // HTML maps integration
        LoadingMap();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AloneModeState.Changed -= OnAloneModeChanged;
        AloneModeState.Changed += OnAloneModeChanged;
        SetAloneMode(AloneModeState.IsAlone, saveState: false);

        if (!string.IsNullOrEmpty(currentFirebaseUserId))
        {
            try
            {
                CurrentUser = await GetUserInfo(currentFirebaseUserId);
                UpdateAppToProfile(CurrentUser);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", "Could not load user profile data.", "OK");
            }
        }
    }    

    protected override void OnDisappearing()
    {
        AloneModeState.Changed -= OnAloneModeChanged;
        base.OnDisappearing();
    }
    

    async Task<UserProfile> GetUserInfo(string _currentUserId)
    {
        return await _db.RetrieveUserInfo(_currentUserId);
    }

    async void LoadingMap()
    {
        // Get the Map

        MapView.Navigated += OnNavigated;
        MapView.Source = "map.html";

        LoadingText.IsVisible = true;
        LoadingText.Text = "Map is loading...";
        LoadingImage.IsVisible = true;

    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("app://"))
        {
            e.Cancel = true; // Cancel the navigation to prevent loading an invalid URL

            if (e.Url.Contains("getDirections"))
            {
                var pUri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(pUri.Query);
                string businessName = query["businessName"];
                //Get the proper business from the name
                var pBus = businessDict.FirstOrDefault(b => b.Name == businessName);

                if (pBus == null) { throw new Exception($"No business found in the dictionnary with the name: {businessName}"); }
                GetDirectionsToBusiness(pBus);
            }
        }
    }

    /// <summary>
    /// When map is done loading, the loading things disappear
    /// </summary>
    /// <param name="_"></param>
    /// <param name="e">The arguments of the event</param>
    void OnNavigated(object? _, WebNavigatedEventArgs e)
    {
        if (e.Result == WebNavigationResult.Success)
        {
            HandleMapLocation();
        }
    }

    // map location on startup
    async void HandleMapLocation()
    {
        MapView.IsVisible = false;
        //MapBorder.IsVisible = true;
        await GoToLocation();

        LoadingText.IsVisible = false;
        LoadingImage.IsVisible = false;
        MapBorder.IsVisible = true;
        MapView.IsVisible = true;

        FetchAllLocations();
    }

    async void FetchAllLocations()
    {
        var pVisibleMarkers = await MapView.EvaluateJavaScriptAsync("getMarkerList()");
        var pNames = await MapView.EvaluateJavaScriptAsync("getNameList()");

        // Helper function to identify empty JS arrays or null responses
        bool IsJsArrayEmpty(string jsResult) =>
            string.IsNullOrWhiteSpace(jsResult) || jsResult == "[]" || jsResult == "null";

        // Intercept the failure state immediately
        if (IsJsArrayEmpty(pVisibleMarkers) || IsJsArrayEmpty(pNames))
        {
            SafePlacesAround.Text = $"Uh-oh, no safe places close to you right now." +
                $"\nMake sure you let your close friends know where you are!";

            SafePlacesAround.IsVisible = true;

            // Change button function to send alert to friends
            GoToClosestLocBtn.Text = "Send quick msg to friends";
            GoToClosestLocBtn.Clicked += SendAlertToFriends;
            GoToClosestLocBtn.IsVisible = true;

            TitleLabel.Text = "Stay Vigilant!";
            TitleLabel.IsVisible = true;
            return;
        }



        var pNameList = pNames.Split('"');
        var pGenNameList = new List<string>();
        foreach (var name in pNameList)
        {
            if (!name.Any(char.IsAsciiLetter)) { continue; }
            pGenNameList.Add(name.Trim('\\', ',', '[', ']', '}'));
        }
        // Iterate through all the businesses and get their infos
        var pMarLists = pVisibleMarkers.Split(',');
        int busIndex = 0;
        var newBus = new LocationMarkers() { Name = pGenNameList[busIndex] };
        bool instantiateNewBusiness = false;
        foreach (var pMarker in pMarLists)
        {
            bool isLng = false;
            bool isLat = false;
            if (instantiateNewBusiness)
            {
                busIndex++;
                newBus = new LocationMarkers();
                newBus.Name = pGenNameList[busIndex];
                instantiateNewBusiness = false;
            }

            for (int i = 0; i < pMarker.Length; i++)
            {
                if (pMarker[i] == 'l' && pMarker[i + 1] == 'n')         // lng
                {
                    isLng = true;
                }

                if (pMarker[i] == 'l' && pMarker[i + 1] == 'a')         // lat
                {
                    isLat = true;
                }

                if (pMarker[i] == ':')        // last char before number
                {
                    if (isLng)      // Get longitude
                    {
                        newBus.Lng = double.Parse(pMarker.Substring(i + 1, length: pMarker.Substring(i + 1).Length - 2));       // length of the number
                        isLng = false;
                        break;
                    }
                    if (isLat)      // Get latitude
                    {
                        newBus.Lat = double.Parse(pMarker.Substring(i + 1, length: pMarker.Substring(i + 1).Length - 2));  // length of the number
                        businessDict.Add(newBus);
                        isLat = false;
                        instantiateNewBusiness = true;
                        break;
                    }
                }

            }
        }

        foreach (var bus in businessDict)
        {
            Debug.WriteLine($"Name: {bus.Name}, Lat: {bus.Lat}, Lng: {bus.Lng}");

        }
        await Task.Delay(500);     // Delay to ensure the map has updated the center before fetching bounds
        //Get all bounds
        var pBounds = await MapView.EvaluateJavaScriptAsync($"getBounds()");
        double north = 0, south = 0, east = 0, west = 0;


        var pBoundSplit = pBounds.Split(',');
        int count = 0;
        if (pBoundSplit.Length <= 0)
        {
            throw new Exception("Bounds of the map were not properly fetched from Java" +
            " or the split didn't happen properly");
        }
        foreach (var bound in pBoundSplit)
        {
            var pSplits = bound.Split(':');
            foreach (var split in pSplits)
            {
                double res;
                if (double.TryParse(split.Trim('\\', ',', '[', ']', '}'), out res))
                {
                    switch (count)
                    {
                        case 0:
                            north = res;
                            count++;
                            break;
                        case 1:
                            south = res;
                            count++;
                            break;
                        case 2:
                            east = res;
                            count++;
                            break;
                        case 3:
                            west = res;
                            count++;
                            break;
                    }
                }
            }
        }

        GetClosestLocation(north, south, east, west);

    }

    // Get the information of the closest locations and display it,
    // also save the closest location for later use when navigating
    void GetClosestLocation(double _north, double _south, double _east, double _west)
    {
        //Debug.WriteLine($"{_north}, south: {_south}, East {_east}, west {_west}");
        var visibleBusinesses = businessDict.Where(bus =>
        bus.Lat <= _north &&
        bus.Lat >= _south &&
        bus.Lng <= _east &&
        bus.Lng >= _west
    ).ToList();

        foreach (var bus in visibleBusinesses)
        {
            Debug.WriteLine($"Visible business: {bus.Name}");
        }

        double pDist = 0;
        var pClosestBusiness = visibleBusinesses.OrderBy(bus =>
        {
            var busLoc = new Location(bus.Lat, bus.Lng);
            pDist = currentLoc.CalculateDistance(busLoc, DistanceUnits.Kilometers);
            return pDist;
        }).FirstOrDefault();


        //Debug.WriteLine($"Closest business: {pClosestBusiness?.Name} and dist: {}");

        closestLoc = pClosestBusiness;
        // Display info
        // If no places around
        if (visibleBusinesses.Count <= 0)
        {
            var pCloseLoc = businessDict.MinBy(b => currentLoc.CalculateDistance(new Location(b.Lat, b.Lng), DistanceUnits.Kilometers));
            if (pCloseLoc != null)
            {
                var pActDist = currentLoc.CalculateDistance(new Location(pCloseLoc.Lat, pCloseLoc.Lng), DistanceUnits.Kilometers);
                SafePlacesAround.Text = $"Uh-oh, the closest one is {pCloseLoc.Name} at: {pActDist.ToString("##.#")} km." +
                    $"\nZoom out to see all your options." +
                $"\nMake sure you let your close friends know where you are!";
            }
            else
            {
                SafePlacesAround.Text = $"Uh-oh, no safe places close to you right now." +
                    $"\nMake sure you let your close friends know where you are!";
            }

            SafePlacesAround.IsVisible = true;

            // Change button function to send alert to friends
            GoToClosestLocBtn.Text = "Send quick msg to friends";
            GoToClosestLocBtn.Clicked += SendAlertToFriends;
            GoToClosestLocBtn.IsVisible = true;

            TitleLabel.Text = "Stay Vigilant!";
            TitleLabel.IsVisible = true;
            return;
        }
        SafePlacesAround.Text = $"You have {visibleBusinesses.Count} safe places around you." +
            $" \nThe closest one is {pClosestBusiness?.Name}";

        var busLoc = new Location(pClosestBusiness.Lat, pClosestBusiness.Lng);
        var pClosestDist = (currentLoc.CalculateDistance(busLoc, DistanceUnits.Kilometers)) * 1000f / 1.8f / 60;
        // Average walking speed shown in minutes
        TitleLabel.Text = $"You're only {pClosestDist.ToString("##")} min from safety";
        TitleLabel.IsVisible = true;
        SafePlacesAround.IsVisible = true;
        GoToClosestLocBtn.IsVisible = true;
    }


    // Go to the start location of the user
    async Task GoToLocation()
    {
        // Get my position      // COMMENTED FOR TESTING PURPOSES, UNCOMMENT WHEN TESTING ON DEVICE
        //currentLoc = new Location(48.43001925275716, -123.41092919016779);     // Vancouver's coordinates for testing purposes
        var _currentLoc = await Geolocation.GetLocationAsync(
            new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.High
            });
        if (_currentLoc != null)
        {
            currentLoc = new Location(_currentLoc.Latitude, _currentLoc.Longitude);
        }

        double lat;
        double lng;
        if (currentLoc == null)
        {
            throw new Exception("Location was not found");
        }
        else
        {
            lat = currentLoc.Latitude;
            lng = currentLoc.Longitude;
        }

        var placemarks = await Geocoding.Default.GetPlacemarksAsync(lat, lng);
        var placemark = placemarks?.FirstOrDefault();

        if (placemark != null)
        {
            // Locality generally represents the City name
            currentCity = placemark.Locality;
        }

        // Go to my location
        await MapView.EvaluateJavaScriptAsync($"flyToStartLoc({lng},{lat})");

        // Load all markers on the map
        await LoadAllMarkers();

        // don't await it beucase it'll stall
        StartLocationTracking();
    }

    private async Task StartLocationTracking()
    {
        // Start tracking the user's location and update the map accordingly
        var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
        Location previousLoc = currentLoc;
        while (true)
        {
            var location = await Geolocation.GetLocationAsync(request);
            if (location != null)
            {
                currentLoc = new Location(location.Latitude, location.Longitude);
                await MapView.EvaluateJavaScriptAsync($"updateUserLocation({currentLoc.Longitude}, {currentLoc.Latitude})");

                // Check if position has changed enough to update directions (e.g., more than 200 meters)
                if (currentLoc.CalculateDistance(previousLoc, DistanceUnits.Kilometers) >= 0.015f)
                {
                    // update directions 
                    string url =
                    $"https://api.mapbox.com/directions/v5/mapbox/walking/" +
                    $"{currentLoc.Longitude},{currentLoc.Latitude};{navigatingBusiness.Lng},{navigatingBusiness.Lat}" +
                    $"?alternatives=true&geometries=geojson&language=en&overview=full&steps=true&access_token={Token}";

                    GetDirections(url, navigatingBusiness.Name);
                }
            }
            await Task.Delay(2000); // Update every 2 seconds
        }
    }


    async Task LoadAllMarkers()
    {
        await MapView.EvaluateJavaScriptAsync($"getPreCookedMarkers()");

    }

    /// <summary>
    /// Draws the route to the closest safe place
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void NavigateToClosestLocation(object? sender, EventArgs e)
    {

        string url =
        $"https://api.mapbox.com/directions/v5/mapbox/walking/" +
        $"{currentLoc.Longitude},{currentLoc.Latitude};{closestLoc.Lng},{closestLoc.Lat}" +
        $"?alternatives=true&geometries=geojson&language=en&overview=full&steps=true&access_token={Token}";

        //await MapView.EvaluateJavaScriptAsync("ShowBusinessInfo('{test, test safeword, testExit}', 'true')");
        await MapView.EvaluateJavaScriptAsync($"ShowBusinessInfo('', true, '{closestLoc.Name}')");    // Later change to ID instead of name

        navigatingBusiness = closestLoc;     // Set the navigating business to the closest one for future updates
        GetDirections(url, closestLoc.Name);
    }


    /// <summary>
    /// Get the directions to the location described in the url and draw the route on the map, 
    /// also get the steps and display them in the itinerary
    /// IMPORTANT: ALWAYS UPDATE THE NAVIGATING BUSINESS BEFORE CALLING THIS FUNCTION
    /// </summary>
    /// <param name="url">URL containing the parameters of the itinerary</param>
    async void GetDirections(string url, string _busName)
    {
        FreeRoamGrid.IsVisible = false;
        ItineraryGrid.IsVisible = true;
        HttpClient client = new();

        string response = await client.GetStringAsync(url);


        JsonDocument doc = JsonDocument.Parse(response);

        var geometry =
            doc.RootElement
               .GetProperty("routes")[0]
               .GetProperty("geometry");

        var steps = doc.RootElement
            .GetProperty("routes")[0]
            .GetProperty("legs")[0]
            .GetProperty("steps");

        RouteSteps.Clear();         // Clear the route in case it's not yet
        int i = 0;
        foreach (var step in steps.EnumerateArray())
        {
            bool isLastOne = i >= steps.EnumerateArray().Count() - 1;
            var instructions = step.GetProperty("maneuver").GetProperty("instruction");

            double distance = step.GetProperty("distance").GetDouble();

            RouteSteps.Add(new RouteStep
            {
                Instruction = instructions.ToString(),
                Distance = isLastOne ? $"{distance.ToString("##")}" : $"{distance.ToString("##")}m"
            });
            i++;        // Iterate through the steps
        }
        DirectionsBusinessName.Text = _busName;
        DirectionsBusinessName.IsVisible = true;

        string geoJson = geometry.ToString();
        string stepJson = JsonSerializer.Serialize(RouteSteps);

        await MapView.EvaluateJavaScriptAsync($"drawRoute('{geoJson}', '{stepJson}')");
    }


    private async void GoBackFromItinerary(object? sender, EventArgs e)
    {
        FreeRoamGrid.IsVisible = true;
        ItineraryGrid.IsVisible = false;

        //Empty the route steps
        RouteSteps.Clear();

        await MapView.EvaluateJavaScriptAsync($"clearRoute()");
    }

    public async void GetDirectionsToBusiness(LocationMarkers _busName)
    {
        //Navigate to that business location and show the itinerary

        string url =
        $"https://api.mapbox.com/directions/v5/mapbox/walking/" +
        $"{currentLoc.Longitude},{currentLoc.Latitude};{_busName.Lng},{_busName.Lat}" +
        $"?alternatives=true&geometries=geojson&language=en&overview=full&steps=true&access_token={Token}";

        navigatingBusiness = _busName;     // Set the navigating business for future updates
        GetDirections(url, _busName.Name);

        //Close popup
        await MapView.EvaluateJavaScriptAsync($"changeToMinimized()");      // minimize the popup
    }

    // Triggered when clicked on the top right button to show the full list of businesses
    private async void ShowBusinessList(object? sender, EventArgs a)
    {
        foreach (var pBus in businessDict)
        {
            var busLoc = new Location(pBus.Lat, pBus.Lng);
            var pDist = currentLoc.CalculateDistance(busLoc, DistanceUnits.Kilometers);
            //var pProperDist = pDist < 1 ? pDist : pDist * 1000;   if we want to show in meters when it's less than 1 km
            await MapView.EvaluateJavaScriptAsync($"setDistanceToBusiness('{pBus.Name}','{(pDist).ToString("##.#")}')");
        }

        await MapView.EvaluateJavaScriptAsync($"displayFullList()");
    }

    private void FriendTabClicked(object? sender, TappedEventArgs e)
    {
        // Show a list of friends
        FriendsMenu.IsVisible = !FriendsMenu.IsVisible;
    }

    private void GroupTabClicked(object? sender, TappedEventArgs e)
    {
        // Show their group list
        Debug.WriteLine($"[TAP] Clicked On Group Tab");

        //premium shows walking group
    }

    private void OutTabClicked(object? sender, TappedEventArgs e)
    {
        // Show safe places that are poppin'
        Debug.WriteLine($"[TAP] Clicked On Out Tab");

        //premium can ask ask for bid?
    }


    private void ActivateAloneMode(object? sender, TappedEventArgs e)
    {
        ToggleAloneMode();
    }

    public void ToggleAloneMode()
    {
        SetAloneMode(!IsAlone);
    }

    public void SetAloneMode(bool isAlone, bool saveState = true)
    {
        IsAlone = isAlone;
        AloneCheckMark.IsVisible = IsAlone;

        if (saveState)
        {
            AloneModeState.Set(IsAlone);
        }
    }

    private void OnAloneModeChanged(object? sender, bool isAlone)
    {
        MainThread.BeginInvokeOnMainThread(() => SetAloneMode(isAlone, saveState: false));
    }

    private async void OpenSettings(object? sender, TappedEventArgs e)
    {
        SettingsMenu.InputTransparent = false;
        await SettingsMenu.TranslateToAsync(0, 0, 250, Easing.SinInOut);
    }


    async Task CloseSettingsMenu()
    {
        await SettingsMenu.TranslateToAsync(-this.Width, 0, 200, Easing.SinIn);
        GeneralSettingsGrid.IsVisible = true;       // Make sure that when we re-open, we're on the general menu again
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await CloseSettingsMenu();
    }

    private async void ShowAloneOptions(object? sender, EventArgs e)
    {

        AloneSettingsGrid.IsVisible = true;
        await AloneSettingsGrid.TranslateToAsync(0, 0, 200, Easing.SinIn);
        GeneralSettingsGrid.IsVisible = false;
    }

    private async void BackFromAloneModeSettings(object? sender, EventArgs e)
    {
        await BackToSettingsMenu();
    }

    async Task BackToSettingsMenu()
    {
        GeneralSettingsGrid.IsVisible = true;
        await AloneSettingsGrid.TranslateToAsync(-this.Width, 0, 200, Easing.SinIn);
        AloneSettingsGrid.IsVisible = false;
    }

    private void CallFriendBoxClicked(object? sender, TappedEventArgs e)
    {
        callFriendCheckmark.IsVisible = !callFriendCheckmark.IsVisible;
    }

    /// <summary>
    /// The checkbox in the Alone Mode Settings clicked
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DirectionsBoxClicked(object? sender, TappedEventArgs e)
    {
        directionsCheckmark.IsVisible = !directionsCheckmark.IsVisible;
    }

    private void SendSignalBoxClicked(object? sender, TappedEventArgs e)
    {
        sendSignalCheckmark.IsVisible = !sendSignalCheckmark.IsVisible;
    }

    private void AutomaticActivationBoxClicked(object? sender, TappedEventArgs e)
    {
        ActivateAutomaticallyCheckmark.IsVisible = !ActivateAutomaticallyCheckmark.IsVisible;
    }

    private void SendAlertToFriends(object? sender, EventArgs e)
    {

    }

    // Triggered when clicking on the "Add Friend" button in the friends menu
    private void AddFriendMenu(object? sender, TappedEventArgs e)
    {
        AddFriendMenuBorder.IsVisible = true;
        // reset entries
        FriendIDEntry.Text = "";
        CloseFriendSwitch.IsToggled = false;
    }

    private void AlertSpecificFriend(object? sender, TappedEventArgs e)
    {
        if (e.Parameter != null)
            Debug.WriteLine($"[TAP] Alerting specific friend with ID: {e.Parameter.ToString()}");
    }

    private void OnCompletedAddFriendForm(object? sender, EventArgs e)
    {
        //FriendsListCollection.Add(new FriendInfo
        //{
        //    Name = "Raj",
        //    DisplayName = "TheNepaleseKing",
        //    UniqueId = "Rpk1", // Find a good way to generate a unique friendID
        //    isCloseFriend = true
        //});
        var uID = FriendIDEntry.Text;
        var pCloseFriend = CloseFriendSwitch.IsToggled;
        FriendsListCollection.Add(new FriendInfo
        {
            Name = $"{uID}",
            DisplayName = "No Display Name",
            UniqueId = uID, // Find a good way to generate a unique friendID
            isCloseFriend = pCloseFriend
        });

        AddFriendMenuBorder.IsVisible = false;
    }

    private async void OnProfileClicked(object? sender, EventArgs e)
    {
        if (CurrentUser == null)
        {
            // Not connected yet
            LoginOrCreateOption.IsVisible = !LoginOrCreateOption.IsVisible;
        }
        else
        {
            // show logged in User Info
            LoginOrCreateOption.IsVisible = false;
            UserProfileInfo.IsVisible = !UserProfileInfo.IsVisible;
            ProfileText.Text = $"Welcome {CurrentUser.DisplayName}";
        }
    }
    
    /// <summary>
    /// COMMENTED FOR SERIALIZE LOGIC, IF EVER WE WANT TO BRING IT BACK
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>

    //private async void OnLoginClicked(object? sender, EventArgs e)
    //{
    //    string pKey = string.Empty;
    //    string pPassw = string.Empty;

    //    pKey = $"{LoginEmailEntry.Text}";   // Concatenate both entries to find the unique username
    //    Debug.WriteLine($"[TAP] MY USERNAME: {pKey}");
    //    pPassw = LoginPasswordEntry.Text;
    //    Debug.WriteLine($"[TAP] Password: {pPassw}");

    //    var accSerialized = JsonSerializer.Serialize(new { Username = pKey, Password = pPassw });
    //    // Retrieve user data
    //    string profile = string.Empty;
    //    UserProfile userProf = new UserProfile();
    //    if (accSerialized != null || string.IsNullOrEmpty(accSerialized))
    //    {
    //        profile = await SecureStorage.Default.GetAsync(accSerialized);
    //        userProf = JsonSerializer.Deserialize<UserProfile>(profile);
    //        Debug.WriteLine($"[SECURE STORAGE] DATA retried Successfully!with friend: {userProf?.FriendList[0].DisplayName}");
    //        //LoginOrCreateProfileBorder.IsVisible = false;


    //    }

    //}

    private async void OpenLoginPopup(object? sender, EventArgs e)
    {
        LoginPopup.IsVisible = true;
        LoginOrCreateOption.IsVisible = false;
    }

    private async void OpenSignupPopup(object? sender, EventArgs e)
    {
        SignupPopup.IsVisible = true;
        LoginOrCreateOption.IsVisible = false;
    }



    private async void OnLoginButtonClicked(object sender, EventArgs e)
    {
        UserProfile pUser = new();
        try
        {
            // Execute the simple Firebase request
            string userUid = await _authService.GetEmailPasswordAsync(LoginEmailEntry.Text, LoginPasswordEntry.Text);

            currentFirebaseUserId = userUid;

            pUser = await _db.RetrieveUserInfo(userUid);

            //await DisplayAlertAsync("Success", $"Logged in successfully! User ID: {userUid} and my email: {pUser.UserEmail}", "Got it");

            //SendSignupErrorMsg("Successful!! ", true);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Login failed: {ex.Message}", "rip");

            //SendSignupErrorMsg("UNSuccessful!! ");
        }

        if (pUser.DisplayName != string.Empty)
        {
            LoginPopup.IsVisible = false;

            UpdateAppToProfile(pUser);
            CurrentUser = pUser;
        }
    }


    private async void OnCreateProfileClicked(object? sender, EventArgs e)
    {
        // At least 4 characters for the name
        if (DisplayNameEntry.Text.Length < 4)
        {
            await DisplayAlertAsync("Woops!", "Name should be at least 4 characters!", "Try again");
            return;
        }
        string pId = string.Empty;
        bool needToUpdate = false;
        try
        {
            pId = await _authService.SetEmailPassword(SignupEmailEntry.Text, SignupPasswordEntry.Text);

        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Already Signed", $"{ex.Message}", "kk");

            pId = await _authService.GetEmailPasswordAsync(SignupEmailEntry.Text, SignupPasswordEntry.Text);
            needToUpdate = true;
        }
        UserProfile pNewProf = new UserProfile()
        {
            DisplayName = DisplayNameEntry.Text,
            UserEmail = SignupEmailEntry.Text,
            UserLocation = new GeoPoint(currentLoc.Latitude, currentLoc.Longitude),
            FriendList = new List<FriendInfo>() { new FriendInfo() { Name = "Raj", DisplayName = "TheNepaleseKing", UniqueId = "", isCloseFriend = true } }
        };

        currentFirebaseUserId = pId;
        await _db.ConnectUserToDb(pId, pNewProf, needToUpdate);

        await DisplayAlertAsync("SUCCESS!", "You're in the DB!", "Yay");

        SignupPopup.IsVisible = false;

        if (needToUpdate)
            UpdateAppToProfile(pNewProf);
        CurrentUser = pNewProf;

    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        _authService.SignOut();

        // Clean variables
        CurrentUser = null;

        // Hide targeted texts
        ProfileNameText.IsVisible = false;

        if (Application.Current != null)
        {
            await Shell.Current.GoToAsync($"{nameof(MapPage)}");
        }
    }


    //void SendSignupErrorMsg(string _msg, bool _isSuccessful = false)
    //{
    //    if (!_isSuccessful)
    //    {
    //        LoginMsgTxt.Text = _msg;
    //        Color red = new Color(255, 0, 0);
    //        LoginMsgTxt.TextColor = red;
    //    }
    //    else
    //    {
    //        LoginMsgTxt.Text = _msg;
    //        Color green = new Color(58, 181, 74);
    //        LoginMsgTxt.TextColor = green;
    //    }
    //}

    void UpdateAppToProfile(UserProfile _userProf)
    {
        //Update friends, settings, and other things according to the profile that just logged in
        //UI 
        ProfileNameText.Text = $"{_userProf.DisplayName}";
        ProfileNameText.IsVisible = true;
        //SettingsProfileImage.IsVisible = false;
        //AloneSettingsProfileImage.IsVisible = false;

        ProfileDisplayShow.Text = $"Your display Name: \n{_userProf.DisplayName}";
        ProfileIDShow.Text = $"Your ID: \n{currentFirebaseUserId}";
        ProfileEmailShow.Text = $"Your Email: \n{_userProf.UserEmail}";

        //Friends
        foreach (var frnds in _userProf.FriendList)
        {
            FriendsListCollection.Add(frnds);
        }
    }


}
