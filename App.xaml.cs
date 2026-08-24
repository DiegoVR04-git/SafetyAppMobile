using Microsoft.Maui.ApplicationModel;

namespace SafetyAppMobile;

public partial class App : Application
{
    public static event Action<int>? EmergencyAlertActivated;

    public App()
    {
        InitializeComponent();

        // Always start with AppShell for consistent navigation throughout the app
        MainPage = new AppShell();

        // Don't navigate on startup - let AppShell handle the default route
        // The app will show the default ShellContent which is the first one defined
        // Navigation logic will happen in the pages' OnAppearing if needed

        // Global exception handler for debugging
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[App] UnhandledException: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[App] UnobservedTaskException: {e.Exception}");
            e.SetObserved();
        };
    }

    public static void RaiseEmergencyAlertActivated(int alertId)
    {
        MainThread.BeginInvokeOnMainThread(() => EmergencyAlertActivated?.Invoke(alertId));
    }
}