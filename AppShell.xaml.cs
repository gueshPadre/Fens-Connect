namespace FENS_Connect
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(MapPage), typeof(MapPage));
        }
    }
}
