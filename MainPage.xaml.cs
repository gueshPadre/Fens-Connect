namespace FENS_Connect
{
    public partial class MainPage : ContentPage
    {
        const string OtherOption = "Other (Specify)";

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCityPickerSelectedIndexChanged(object? sender, EventArgs e)
        {
            var isOther = CityPicker.SelectedItem is string city && city == OtherOption;
            OtherCityEntry.IsVisible = isOther;

            if (!isOther)
                OtherCityEntry.Text = string.Empty;
        }

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            if (CityPicker.SelectedItem is not string city)
            {
                await DisplayAlertAsync("Oops, you forgot to select a city!", "Please choose the city you're in.", "Got it!");
                return;
            }

            switch (city)
            {
                case "Vancouver":
                    OnVancouverClicked(sender, e);
                    break;
                case "Victoria":
                    OnVictoriaClicked(sender, e);
                    break;
                case OtherOption:
                    if (string.IsNullOrWhiteSpace(OtherCityEntry.Text))
                    {
                        await DisplayAlertAsync("Specify your city", "Please enter the city you're in.", "Got it!");
                        return;
                    }
                    OnOtherClicked(sender, e);
                    break;
            }
        }

        private async void OnVancouverClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"{nameof(MapPage)}?city=Vancouver");
        }

        private void OnVictoriaClicked(object? sender, EventArgs e)
        {
            ConfirmBtn.Text = "Clicked On Vic";
        }

        private void OnOtherClicked(object? sender, EventArgs e)
        {
            ConfirmBtn.Text = $"Clicked: {OtherCityEntry.Text.Trim()}";
        }
    }
}
