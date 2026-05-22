namespace FENS_Connect
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

#if WINDOWS
            window.Created += OnWindowCreated;
#endif
            return window;
        }

#if WINDOWS
        static void OnWindowCreated(object? sender, EventArgs e)
        {
            if (sender is not Window window)
                return;

            window.Created -= OnWindowCreated;

            var handler = window.Handler;
            if (handler?.PlatformView is null)
                return;

            var nativeWindow = handler.PlatformView;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Phone-like portrait window (9:16)
            const int width = 390;
            const int height = 844;
            appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        }
#endif
    }
}
