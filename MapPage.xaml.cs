using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace FENS_Connect;

[QueryProperty(nameof(City), "city")]
public partial class MapPage : ContentPage
{

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
    // TODO: Hide the token key in production, this is just for testing purposes
    string Token = "pk.eyJ1IjoiZ3Vlc2giLCJhIjoiY21wZG9sdWFqMGRyYzJ6bzgyOWc3ZmdwMyJ9.mqd_v91FCsLiCVizOxLT9g";

    public ObservableCollection<RouteStep> RouteSteps { get; set; } = new ObservableCollection<RouteStep>();
    string _city = string.Empty;
    List<LocationMarkers> businessDict = new List<LocationMarkers>();
    Location currentLoc;
    string currentCity = string.Empty;
    LocationMarkers closestLoc;

    LocationMarkers navigatingBusiness;     // the business that the user is currently navigating to, used to update the directions
    bool isNavigatingToBus = false;     // whether the user is currently navigating to a business, used to update the directions if the user's location changes significantly

    public string City
    {
        get => _city;
        set
        {
            _city = Uri.UnescapeDataString(value ?? string.Empty);
            UpdateCityDisplay();
        }
    }

    public MapPage()
    {
        InitializeComponent();
        BindingContext = this;

        // HTML maps integration
        LoadingMap();
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
        MapBorder.IsVisible = false;
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

        Debug.WriteLine($"Closest business: {pClosestBusiness?.Name}");

        closestLoc = pClosestBusiness;
        // Display closest
        SafePlacesAround.Text = $"You have {visibleBusinesses.Count}  safe places around you." +
            $" \nThe closest one is {pClosestBusiness?.Name}";

        CityTitleLabel.Text = $"You're only {((pDist * 1000) / 1.8f /*Average walking speed*/
            / 60 /*to show in minutes*/).ToString("##")} min from safety";
        CityTitleLabel.IsVisible = true;
        SafePlacesAround.IsVisible = true;
        GoToClosestLocBtn.IsVisible = true;
    }


    // Go to the start location of the user
    async Task GoToLocation()
    {
        // Get my position      // COMMENTED FOR TESTING PURPOSES, UNCOMMENT WHEN TESTING ON DEVICE
        //currentLoc = new Location(48.43114761028363, -123.39322136294639);     // Vancouver's coordinates for testing purposes
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
        int index = 0;
        var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
        Location previousLoc = currentLoc;
        while (true)
        {
            index++;
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateCityDisplay();
    }

    void UpdateCityDisplay()
    {
        var displayCity = string.IsNullOrWhiteSpace(_city) ? "Your area" : _city;
        CityTitleLabel.Text = displayCity;
        Title = displayCity;
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
        isNavigatingToBus = true;
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

        await MapView.EvaluateJavaScriptAsync($"drawRoute('{geoJson}')");
    }


    private async void GoBackFromItinerary(object? sender, EventArgs e)
    {
        isNavigatingToBus = false;
        FreeRoamGrid.IsVisible = true;
        ItineraryGrid.IsVisible = false;

        //Empty the route steps
        RouteSteps.Clear();

        await MapView.EvaluateJavaScriptAsync($"clearRoute()");
    }

    public async Task GetDirectionsToBusiness(LocationMarkers _busName)
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
            await MapView.EvaluateJavaScriptAsync($"getDistanceToBusiness('{pBus.Name}','{(pDist).ToString("##.#")}')");
        }

        await MapView.EvaluateJavaScriptAsync($"displayFullList()");
    }

    private void FriendTabClicked(object? sender, TappedEventArgs e)
    {
        // Show a list of friends
        Debug.WriteLine($"[TAP] Clicked On Friend Tab");
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

}
