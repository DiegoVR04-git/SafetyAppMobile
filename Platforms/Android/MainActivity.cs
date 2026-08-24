using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace SafetyAppMobile;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    Icon = "@mipmap/pulso_violeta_icon",
    RoundIcon = "@mipmap/pulso_violeta_icon",
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private Platforms.Android.VolumeTriggerReceiver _volumeReceiver;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }
}