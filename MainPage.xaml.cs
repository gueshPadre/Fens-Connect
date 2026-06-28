
namespace FENS_Connect
{
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
        }


        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (e.NewTextValue.Length > 0) 
            {
                ConfirmBtn.Text = "Send input and explore app";
            }
            else
            {
                ConfirmBtn.Text = "Enter";
            }
        }

        private async void OnConfirmClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"{nameof(MapPage)}");

        }

    }
}
