using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.Devices.Sensors;
using System.Text;
using System.Text.Json;
using Application = Android.App.Application;

namespace SafetyAppMobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
public class VolumeTriggerReceiver : BroadcastReceiver
{
    private static int _clickCount = 0;
    private static DateTime _lastClickTime = DateTime.MinValue;
    private const int MaxDelaySeconds = 3;
    private const int ClicksRequired = 4;
    private const int DebounceMilliseconds = 200; // Mínimo tiempo entre clics humanos reales

    public override void OnReceive(Context context, Intent intent)
    {
        if (intent.Action == "android.media.VOLUME_CHANGED_ACTION")
        {
            // 🛑 FILTRO 1: Si ya hay una alerta activa o en proceso, ignorar el botón
            int activeAlertId = Microsoft.Maui.Storage.Preferences.Default.Get("active_alert_id", 0);
            if (activeAlertId != 0) return;

            var now = DateTime.Now;

            // 🛑 FILTRO 2 (Anti-Deslizamiento): Si los eventos llegan demasiado rápido (< 200ms), 
            // es un deslizamiento de software, no un clic físico. Lo ignoramos.
            if (_lastClickTime != DateTime.MinValue && (now - _lastClickTime).TotalMilliseconds < DebounceMilliseconds)
            {
                return;
            }

            // Si pasó mucho tiempo desde el último clic válido, reiniciamos la cuenta
            if ((now - _lastClickTime).TotalSeconds > MaxDelaySeconds)
            {
                _clickCount = 0;
            }

            _clickCount++;
            _lastClickTime = now;

            if (_clickCount == ClicksRequired)
            {
                _clickCount = 0;

                // 📝 BLOQUEO TEMPORAL: Marcamos la preferencia como -1 para indicar que 
                // una alerta ya se está enviando a la nube y bloquear más clics.
                Microsoft.Maui.Storage.Preferences.Default.Set("active_alert_id", -1);

                // Feedback táctil confirmando que se detectó el patrón
                Vibrator vibrator = (Vibrator)context.GetSystemService(Context.VibratorService);
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    vibrator?.Vibrate(VibrationEffect.CreateOneShot(500, VibrationEffect.DefaultAmplitude));
                }
                else
                {
                    vibrator?.Vibrate(500);
                }

                var appContext = global::Android.App.Application.Context;
                Task.Run(async () => await DispararAlertaOculta(appContext));
            }
        }
    }

    private async Task DispararAlertaOculta(Context context)
    {
        Console.WriteLine("🚨 ¡PATRÓN DETECTADO! DISPARANDO S.O.S OCULTO...");

        try
        {
            int userId = Microsoft.Maui.Storage.Preferences.Default.Get("current_user_id", 0);
            if (userId == 0)
            {
                Microsoft.Maui.Storage.Preferences.Default.Remove("active_alert_id");
                return;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null)
            {
                var alertData = new { user_id = userId, latitude = location.Latitude, longitude = location.Longitude };
                var json = JsonSerializer.Serialize(alertData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var client = new HttpClient();
                var response = await client.PostAsync("https://safety-app-api.onrender.com/alerts", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseContent);
                    int alertId = doc.RootElement.GetProperty("alert").GetProperty("alert_id").GetInt32();
                    WhatsAppResults.Save(userId, alertId, doc.RootElement);

                    // 📝 Actualizamos con el ID real recibido de la base de datos
                    Microsoft.Maui.Storage.Preferences.Default.Set("active_alert_id", alertId);
                    global::SafetyAppMobile.App.RaiseEmergencyAlertActivated(alertId);

                    Console.WriteLine($"✅ ALERTA CREADA (ID: {alertId}). ENCENDIENDO RASTREADOR...");

                    var serviceIntent = new Intent(context, typeof(AndroidLocationService));
                    serviceIntent.PutExtra("alert_id", alertId);

                    if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    {
                        context.StartForegroundService(serviceIntent);
                    }
                    else
                    {
                        context.StartService(serviceIntent);
                    }
                }
                else
                {
                    // Si la API falla, liberamos el bloqueo para que el usuario pueda intentar de nuevo
                    Microsoft.Maui.Storage.Preferences.Default.Remove("active_alert_id");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en alerta oculta: {ex.Message}");
            Microsoft.Maui.Storage.Preferences.Default.Remove("active_alert_id");
        }
    }
}
