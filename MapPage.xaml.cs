using Google.Cloud.Firestore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace FENS_Connect;

//[QueryProperty(nameof(City), "city")]
public partial class MapPage : ContentPage
{
    public class UserProfile
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public List<FriendInfo> FriendList { get; set; } = new List<FriendInfo>();

        public GeoPoint UserLocation { get; set; } = new GeoPoint();
        public Dictionary<string, bool> UserSettings { get; set; } = new Dictionary<string, bool>();

        public const string ALERTS_SHOWN_TO_FRIENDS_KEY = "AlertsShownToFriends";

    }

    public class AlertMarkerInfo
    {
        public string MarkerID { get; set; } = string.Empty;
        public string UserID { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public GeoPoint alertLoc { get; set; } = new GeoPoint();
        public bool AvailableToFriends { get; set; } = false;
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

    public class FriendInfo : INotifyPropertyChanged
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
                    OnPropertyChanged(nameof(StarImgSource));    // update visuals
                }
                else
                {
                    _isCloseFriend = false;
                    starImgSource = "star.png"; // Change back to default star image
                    OnPropertyChanged(nameof(StarImgSource));    // update visuals
                }
            }
        }
        private bool _showRemoveButton;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool ShowRemoveButton
        {
            get => _showRemoveButton;
            set
            {
                if (_showRemoveButton != value)
                {   // When value changes
                    _showRemoveButton = value;

                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


    }

    public class FriendRequest
    {
        public string SenderId { get; set; } = string.Empty;
        public string SenderDisplayName { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public Timestamp RequestTimestamp { get; set; } = Timestamp.FromDateTime(DateTime.UtcNow);
    }


    public class FriendSearchResult
    {
        public string NameFound { get; set; } = string.Empty;
        public string IDFoundDisplayed { get; set; } = string.Empty;
        public bool CloseFrnd { get; set; } = false;
        public string FullID { get; set; } = string.Empty;


    }

    private readonly IFirebaseAuthService _authService;
    private F_FirestoreDB _db;

    // TODO: Hide the token key in production, this is just for testing purposes
    string Token = "pk.eyJ1IjoiZ3Vlc2giLCJhIjoiY21wZG9sdWFqMGRyYzJ6bzgyOWc3ZmdwMyJ9.mqd_v91FCsLiCVizOxLT9g";

    public ObservableCollection<RouteStep> RouteSteps { get; set; } = new ObservableCollection<RouteStep>();
    public ObservableCollection<FriendInfo> FriendsListCollection { get; set; } = new ObservableCollection<FriendInfo>();
    public ObservableCollection<FriendRequest> FriendsRequestCollection { get; set; } = new ObservableCollection<FriendRequest>();
    public ObservableCollection<FriendSearchResult> PeopleFound { get; set; } = new ObservableCollection<FriendSearchResult>();


    string _city = string.Empty;
    List<LocationMarkers> businessDict = new List<LocationMarkers>();
    Location currentLoc;
    string currentCity = string.Empty;
    LocationMarkers closestLoc;
    private CancellationTokenSource? _holdCancellationTokenSource;
    private const int touchHoldTime = 500; // Time in milliseconds to consider a touch as a long press

    LocationMarkers? navigatingBusiness;     // the business that the user is currently navigating to, used to update the directions

    public static UserProfile? CurrentUser { get; private set; }


    public bool IsAlone { get; set; }
    public string City
    {
        get => _city;
        set
        {
            _city = Uri.UnescapeDataString(value ?? string.Empty);
        }
    }


    private readonly Style selectedTabStyle = (Style)Application.Current!.Resources["SelectedTabStyle"];
    private readonly Style UnselectedTabStyle = (Style)Application.Current!.Resources["UnselectedTabStyle"];
    private readonly Style SelectedTabLabelStyle = (Style)Application.Current!.Resources["SelectedTabLabelStyle"];
    private readonly Style UnselectedTabLabelStyle = (Style)Application.Current!.Resources["UnselectedTabLabelStyle"];

    string currentFirebaseUserId;
    public string CurrentFirebaseUserId { get => currentFirebaseUserId; }

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
                await UpdateAppToProfile(CurrentUser);
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"Could not load user profile data. {ex.Message}", "Refresh");

                _db.UpdateSettings(currentFirebaseUserId);
                await Shell.Current.GoToAsync($"{nameof(MapPage)}");

            }
        }
    }

    protected override void OnDisappearing()
    {
        AloneModeState.Changed -= OnAloneModeChanged;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (FriendsFound.IsVisible)
        {
            BackFromFriendsSearch(null, new EventArgs());
            AddFriendMenuBorder.IsVisible = true;
            return true;
        }

        if (AddFriendMenuBorder.IsVisible)
        {
            AddFriendMenuBorder.IsVisible = false;
            return true;
        }


        if (FriendsMenu.IsVisible)
        {
            FriendsMenu.IsVisible = false;
            return true;
        }

        return base.OnBackButtonPressed();
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
            else if (e.Url.Contains("setNewAlert"))
            {
                // Set Alert to Db
                var pUri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(pUri.Query);
                string alertID = query["markerID"];
                string userID = query["userID"];
                string note = query["note"];
                string createdBy = query["createdBy"];
                string lat = query["lat"];
                string lng = query["lng"];
                string availableToFriends = query["availableToFriends"];

                var pAlert = new AlertMarkerInfo
                {
                    MarkerID = alertID,
                    UserID = userID,
                    Note = note,
                    CreatedBy = createdBy,
                    alertLoc = new GeoPoint(double.Parse(lat), double.Parse(lng)),
                    AvailableToFriends = bool.Parse(availableToFriends)
                };

                if (userID != null)
                {
                    await AddAlertToDb(userID, pAlert);
                }
                //await DisplayAlertAsync("Success", $"alertMarker ID: {alertID}, user: {userID}", "good");
            }
            else if (e.Url.Contains("DeleteAlert"))
            {
                var pUri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(pUri.Query);
                string alertID = query["markerID"];
                try
                {
                    var pAlerts = await _db.RetrieveAlertMarkers(currentFirebaseUserId,false,true);

                    var pAlToDel = pAlerts.FirstOrDefault(alert => alert.MarkerID == alertID);

                    if (pAlToDel != null)
                    {
                        await _db.RemoveAlert(currentFirebaseUserId, pAlToDel.MarkerID);
                        await DisplayAlertAsync("Success", $"Alert deleted successfully", "OK");
                    }
                    else
                    {
                        await DisplayAlertAsync("Error", $"Error deleting the alert", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FB] Non deletion??: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Debug.WriteLine($"[FB] Inner Error: {ex.InnerException.Message}");
                    }
                }
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
        //currentLoc = new Location(48.43463846486408, -123.3831884197297);     // Vancouver's coordinates for testing purposes
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
        //StartLocationTracking();
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
                    if (navigatingBusiness != null)
                    {
                        // update directions 
                        string url =
                                    $"https://api.mapbox.com/directions/v5/mapbox/walking/" +
                                    $"{currentLoc.Longitude},{currentLoc.Latitude};{navigatingBusiness.Lng},{navigatingBusiness.Lat}" +
                                    $"?alternatives=true&geometries=geojson&language=en&overview=full&steps=true&access_token={Token}";

                        await GetDirrectionsAsync(url, navigatingBusiness.Name);
                    }
                    previousLoc = location;
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
        await GetDirrectionsAsync(url, closestLoc.Name);
    }


    /// <summary>
    /// Get the directions to the location described in the url and draw the route on the map, 
    /// also get the steps and display them in the itinerary
    /// IMPORTANT: ALWAYS UPDATE THE NAVIGATING BUSINESS BEFORE CALLING THIS FUNCTION
    /// </summary>
    /// <param name="url">URL containing the parameters of the itinerary</param>
    async Task GetDirrectionsAsync(string url, string _busName)
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
        navigatingBusiness = null;     // Reset the navigating business since we're no longer navigating

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
        await GetDirrectionsAsync(url, _busName.Name);

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

        // For the first time openning the popup, for some reason needs to do that to have the proper scale on the friends tab
        OnShowRequestsListTap(null, null);
        OnShowFriendsListTap(null, null);
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

    private async void ShowGeneralOptions(object? sender, EventArgs e)
    {
        GeneralSettingsSubMenu.IsVisible = true;
        if (!CurrentUser!.UserSettings.ContainsKey(UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY))
        {
            CurrentUser!.UserSettings.Add(UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY, alertsShownToFriendsCheckmark.IsVisible);
        }
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


    private void AlertsShownToFriendsBoxClicked(object? sender, TappedEventArgs e)
    {
        alertsShownToFriendsCheckmark.IsVisible = !alertsShownToFriendsCheckmark.IsVisible;
        CurrentUser!.UserSettings[UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY] = alertsShownToFriendsCheckmark.IsVisible;
        // later want to update only when going back to the settings menu, but for now, update it immediately
        _db.UpdateSettings(currentFirebaseUserId, CurrentUser);
    }


    private void SendAlertToFriends(object? sender, EventArgs e)
    {
        
    }

    // Triggered when clicking on the "Add Friend" button in the friends menu
    private void AddFriendMenu(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrEmpty(currentFirebaseUserId))
        {
            DisplayAlertAsync("Error", "You must be logged in to add friends.", "OK");
            return;
        }
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

    private async void SendFriendRequest(string _userID, string _friendID, bool _isCloseFrnd)
    {
        _db.SendRequest(_userID, _friendID, CurrentUser!.DisplayName, CurrentUser.Name, _isCloseFrnd);
    }


    private async void OnCompletedAddFriendForm(object? sender, EventArgs e)
    {
        var pDN = FriendIDEntry.Text.Trim();
        var pCloseFriend = CloseFriendSwitch.IsToggled;

        var pFoundPpl = await _db.SearchFriend(pDN, pCloseFriend);

        if (pFoundPpl.Count > 1)  // If we found more than one person with that display name
        {

            //Check in our friends if we have some, if so, exclude them from the list of people found
            foreach (var frnd in FriendsListCollection)
            {
                if (pFoundPpl.Contains(frnd.UniqueId))
                {
                    pFoundPpl.Remove(frnd.UniqueId);
                }
            }

            // After we've removed the friends with same display Name from the list, check if we still have some people left
            if (pFoundPpl.Count < 1)
            {
                var msg = $"No user found with: {pDN} that is not already your friend";
                await DisplayErrorMsgAfterFriendSearch(msg, true);
                return;
            }
            PeopleFoundDisplayName.Text = $"Many were found for {pDN}";

            foreach (var pPpl in pFoundPpl)
            {
                var pUserFound = await _db.GetNameAndEmail(pPpl);
                var frnd = new FriendSearchResult
                {
                    NameFound = pUserFound.Name,
                    IDFoundDisplayed = $"ID: {pPpl.Substring(0, 12)}",
                    CloseFrnd = pCloseFriend,
                    FullID = pPpl
                };
                PeopleFound.Add(frnd);
            }
            FriendsFound.IsVisible = true;
        }
        else if (pFoundPpl.Count > 0)  // If we found exactly one person with that display name
        {
            SendFriendRequest(currentFirebaseUserId, pFoundPpl.First(), pCloseFriend);

            var msg = $"You've successfully sent {pDN} a request!";
            await DisplayErrorMsgAfterFriendSearch(msg, false);
            AddFriendMenuBorder.IsVisible = false;
        }
        else
        {
            // didn't find anyone
            var msg = $"No user found with: {pDN}. \n It's case-sensitive, so make sure spelling is right.";
            await DisplayErrorMsgAfterFriendSearch(msg, true);
        }

    }

    private async Task DisplayErrorMsgAfterFriendSearch(string _msg, bool _isError)
    {
        Color pRed = Color.FromRgb(255, 0, 0);
        Color pGreen = Color.FromRgb(58, 181, 74);
        AddFriendErrorMsgLabel.IsVisible = true;
        AddFriendErrorMsgLabel.TextColor = _isError ? pRed : pGreen;
        AddFriendErrorMsgLabel.Text = _msg;

        var pLength = _isError ? 5000 : 2000;
        await Task.Delay(pLength);

        AddFriendErrorMsgLabel.IsVisible = false;

    }


    private void BackFromFriendsSearch(object? sender, EventArgs e)
    {
        FriendsFound.IsVisible = false;
        PeopleFound.Clear();        // reset
    }


    // When user finds the proper user to send the friend request
    private async void OnProperFriendTapped(object? sender, TappedEventArgs e)
    {
        var pFrnRslt = e.Parameter as FriendSearchResult;
        SendFriendRequest(currentFirebaseUserId, pFrnRslt!.FullID, pFrnRslt.CloseFrnd);

        var msg = $"You've successfully sent {pFrnRslt.NameFound} a request!";
        await DisplayErrorMsgAfterFriendSearch(msg, false);
        AddFriendMenuBorder.IsVisible = false;
        FriendsFound.IsVisible = false;
        PeopleFound.Clear();        // reset
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
            await DisplayAlertAsync("Oops", $"No account found", "Try again");

            //SendSignupErrorMsg("UNSuccessful!! ");
        }

        if (pUser.DisplayName != string.Empty)
        {
            LoginPopup.IsVisible = false;

            CurrentUser = pUser;        // Update CurrentUser variable
            await UpdateAppToProfile(pUser);
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
            Name = NameEntry.Text,
            DisplayName = DisplayNameEntry.Text,
            UserEmail = SignupEmailEntry.Text,
            UserLocation = new GeoPoint(currentLoc.Latitude, currentLoc.Longitude),
        };

        currentFirebaseUserId = pId;
        await _db.ConnectUserToDb(pId, pNewProf, needToUpdate);

        await DisplayAlertAsync("SUCCESS!", "You're officially in!", "Yay");

        SignupPopup.IsVisible = false;

        if (needToUpdate)
            await UpdateAppToProfile(pNewProf);
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

    private void CloseAddFriendMenu(object? sender, TappedEventArgs e)
    {
        AddFriendMenuBorder.IsVisible = false;
    }

    private void CloseFriendListMenu(object? sender, TappedEventArgs e)
    {
        FriendsMenu.IsVisible = false;
    }

    private void CloseFriendFoundMenu(object? sender, TappedEventArgs e)
    {
        BackFromFriendsSearch(null, new EventArgs());
        AddFriendMenuBorder.IsVisible = true;
    }

    private void OnShowRequestsListTap(object? sender, TappedEventArgs e)
    {
        SelectRequests();
        FriendsListCollectionView.IsVisible = false;
        AddFriendButton.IsVisible = false;
        FriendsTitleText.Text = "Friend Requests";

        FriendRequestsCollectionView.IsVisible = true;
    }

    // Select the request tab in friends Menu
    void SelectRequests()
    {
        RequestsTab.Style = selectedTabStyle;
        RequestTabLabel.Style = SelectedTabLabelStyle;

        FriendsTab.Style = UnselectedTabStyle;
        FriendsTabLabel.Style = UnselectedTabLabelStyle;
    }

    private void OnShowFriendsListTap(object? sender, TappedEventArgs e)
    {
        SelectFriends();
        FriendRequestsCollectionView.IsVisible = false;
        FriendsTitleText.Text = "Friends";
        AddFriendButton.IsVisible = true;

        FriendsListCollectionView.IsVisible = true;
    }

    // Select the friends tab in friends Menu
    void SelectFriends()
    {
        FriendsTab.Style = selectedTabStyle;
        FriendsTabLabel.Style = SelectedTabLabelStyle;

        RequestsTab.Style = UnselectedTabStyle;
        RequestTabLabel.Style = UnselectedTabLabelStyle;
    }

    public async void OnStarTapped(object? sender, TappedEventArgs e)
    {
        string pFriendID = (string)e.Parameter!;
        var pFriend = CurrentUser!.FriendList.Find(f => f.UniqueId == pFriendID) ?? throw new Exception($"Friend with ID {pFriendID} not found in the current user's friend list.");

        pFriend.isCloseFriend  = !pFriend.isCloseFriend;        // Toggle the close friend status


        // Reorder the friends list to have close friends at the top
        ReorderFriendList();
        await _db.UpdateCloseFriendStatus(currentFirebaseUserId, pFriend.Name, pFriend.isCloseFriend);
    }


    private async void OnAcceptFriendRequest(object? sender, TappedEventArgs e)
    {
        FriendRequest? fr = e.Parameter as FriendRequest;
        var frnd = await _db.AddFriend(fr!.SenderId, false, currentFirebaseUserId);  // Automatically add the friend as a non-close friend when accepting the request
        FriendsListCollection.Add(frnd);

        // Reorder the friends list to have close friends at the top
        ReorderFriendList();


        // add this user to the other person's friend list as well
        await _db.AddFriendForOtherUser(fr.SenderId, currentFirebaseUserId, CurrentUser!.Name, CurrentUser.DisplayName);


        // Remove the accepted friend request from the collection
        await _db.RemoveRequests(fr.SenderId, currentFirebaseUserId);
        var pReqToRem = FriendsRequestCollection.Where(a => a.SenderId == fr.SenderId).ToList();
        FriendsRequestCollection.Remove(pReqToRem.First());         // Should always only be one request from a specific user, so we can safely remove the first one
        if (FriendsRequestCollection.Count < 1) { friendRqstCircleImg.IsVisible = false; }


        OnShowFriendsListTap(null, null); // Refresh the friends list to show the updated state
        //Update markers from friends
        await GetAllVisibleAlerts();
    }

    void ReorderFriendList()
    {
        var pSortedList = FriendsListCollection.OrderByDescending(f => f.isCloseFriend)
            .ThenBy(f => f.DisplayName)
            .ToList();

        FriendsListCollection.Clear();

        foreach (var frn in pSortedList)
        {
            FriendsListCollection.Add(frn);
        }
    }


    private async void OnRefuseFriendRequest(object? sender, TappedEventArgs e)
    {
        FriendRequest? fr = e.Parameter as FriendRequest;
        await _db.RemoveRequests(fr!.SenderId, currentFirebaseUserId);
        var pReqToRem = FriendsRequestCollection.Where(a => a.SenderId == fr.SenderId).ToList();
        FriendsRequestCollection.Remove(pReqToRem.First());         // Should always only be one request from a specific user, so we can safely remove the first one
        if (FriendsRequestCollection.Count < 1) { friendRqstCircleImg.IsVisible = false; }


        OnShowFriendsListTap(null, null); // Refresh the friends list to show the updated state
    }

    // Called when long pressed on the name in list
    private async void OnLongPressRemoveFriendEnter(object? sender, PointerEventArgs e)
    {
        // Cancel any existing hold timer just in case
        _holdCancellationTokenSource?.Cancel();
        _holdCancellationTokenSource = new CancellationTokenSource();
        var token = _holdCancellationTokenSource.Token;

        try
        {
            // Wait for the required hold duration
            await Task.Delay(touchHoldTime, token);

            // If we reached here without getting cancelled, the user successfully held it!
            if (!token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (sender is Border brd)
                    {
                        if (brd.BindingContext is FriendInfo friend)
                        {
                            var targetFrnd = FriendsListCollection.FirstOrDefault(f => f.UniqueId == friend.UniqueId);
                            if (targetFrnd != null)
                            {
                                targetFrnd.ShowRemoveButton = !targetFrnd.ShowRemoveButton;
                            }
                        }
                    }
                });
            }
        }
        catch (TaskCanceledException)
        {
            // Touch was released too quickly; do nothing
        }
    }

    private void OnLongPressRemoveFriendReleased(object? sender, PointerEventArgs e)
    {
        _holdCancellationTokenSource?.Cancel();
    }



    // Called when remove button is clicked in the list selection
    private async void OnRemoveButtonClicked(object? sender, EventArgs e)
    {
        if (sender is Button friendInfoButton)
        {
            var param = friendInfoButton.CommandParameter as FriendInfo;
            CurrentUser.FriendList.Remove(CurrentUser!.FriendList.Find(f => f.UniqueId == param!.UniqueId));
            await _db.RemoveFriend(currentFirebaseUserId, param!.Name);

            await UpdateAppToProfile(CurrentUser);
            Debug.WriteLine($"[TAP] friend removed");
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

    async Task AddAlertToDb(string _userID, AlertMarkerInfo _alertID)
    {
        await _db.AddAlertToDb(_userID, _alertID);
    }

    async Task UpdateAppToProfile(UserProfile _userProf)
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

        alertsShownToFriendsCheckmark.IsVisible = CurrentUser!.UserSettings[UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY];

        FriendsListCollection.Clear();      // reset the friends list collection to avoid duplicates
        //Friends
        foreach (var frnds in _userProf.FriendList)
        {
            FriendsListCollection.Add(frnds);
        }

        ReorderFriendList();

        //Get Friend requests
        List<FriendRequest> pFriendReqs = await _db.GetFriendRequests(currentFirebaseUserId);
        //Show the friend requests circle in the UI
        friendRqstCircleImg.IsVisible = pFriendReqs.Count > 0;

        if (pFriendReqs.Count > 0)
        {
            // Get friend requests
            for (int i = 0; i < pFriendReqs.Count; i++)
            {
                FriendsRequestCollection.Add(pFriendReqs[i]);
            }
        }


        // updateJsWithNameAndID
        await MapView.EvaluateJavaScriptAsync($"UpdateUserNameAndId('{_userProf.DisplayName}', '{currentFirebaseUserId}', '{_userProf.UserSettings[UserProfile.ALERTS_SHOWN_TO_FRIENDS_KEY]}')");


        // Update the visible alerts
        await GetAllVisibleAlerts();

    }

    async Task GetAllVisibleAlerts()
    {
        var pAlerts = await _db.RetrieveAlertMarkers(currentFirebaseUserId, true,true);

        for (int i = 0; i < pAlerts.Count; i++)
        {
            await MapView.EvaluateJavaScriptAsync($"setAlertMarker('{pAlerts[i].MarkerID}', '{pAlerts[i].Note}', '{pAlerts[i].alertLoc.Longitude}', '{pAlerts[i].alertLoc.Latitude}', '{pAlerts[i].CreatedBy}', '{pAlerts[i].UserID}')");
        }
    }


}
