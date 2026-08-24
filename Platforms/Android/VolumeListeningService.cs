using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Android.Content.PM;

namespace SafetyAppMobile.Platforms.Android;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
public class VolumeListeningService : Service
{
    private VolumeTriggerReceiver _volumeReceiver;
    private const string ChannelId = "SafetyAppListenerChannel";
    private const int NotificationId = 10002;

    public override IBinder OnBind(Intent intent) => null;

    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();

        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Modo Escolta: Activo")
            .SetContentText("Safety App está prestando atención a los botones físicos.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityMin)
            .Build();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        if (_volumeReceiver == null)
        {
            _volumeReceiver = new VolumeTriggerReceiver();
            var filter = new IntentFilter("android.media.VOLUME_CHANGED_ACTION");
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                RegisterReceiver(_volumeReceiver, filter, ReceiverFlags.Exported);
            else
                RegisterReceiver(_volumeReceiver, filter);
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        if (_volumeReceiver != null)
        {
            UnregisterReceiver(_volumeReceiver);
            _volumeReceiver = null;
        }
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Escucha Constante S.O.S", NotificationImportance.Min)
            {
                Description = "Mantiene activa la detección de botones desde el fondo"
            };
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }
    }
}
