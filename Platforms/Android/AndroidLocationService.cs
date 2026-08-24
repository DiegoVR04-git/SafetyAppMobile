using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using System.Text.Json;
using System.Text;
using Microsoft.Maui.Devices.Sensors; // Para el GPS
using Android.Content.PM;
using Microsoft.Maui.Networking;

namespace SafetyAppMobile.Platforms.Android;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
public class AndroidLocationService : Service
{
    private System.Timers.Timer _timer;
    private int _currentAlertId;
    private const string ChannelId = "SafetyAppTrackerChannel";
    private const int NotificationId = 10001;
    private HttpClient _httpClient;
    private const string BaseUrl = "https://safety-app-api.onrender.com";

    // Requisito de Android para los servicios, pero no lo usaremos
    public override IBinder OnBind(Intent intent) => null;

    // 🚀 ESTE ES EL MOTOR: Se ejecuta cuando la app da la orden de iniciar el rastreo
    public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
    {
        // 1. Recibimos el ID de la alerta que nos mandará la interfaz de C#
        _currentAlertId = intent?.GetIntExtra("alert_id", 0) ?? 0;
        var latInicial = intent?.GetDoubleExtra("lat", 0) ?? 0;
        var lonInicial = intent?.GetDoubleExtra("lon", 0) ?? 0;
        _httpClient = new HttpClient();

        // 2. Crear el canal de notificación (Requisito obligatorio de Android 8+)
        CreateNotificationChannel();

        // 3. Construir la notificación "Inborrable"
        var notification = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("🚨 EMERGENCIA ACTIVA")
            .SetContentText("Safety App está transmitiendo tu ubicación a tu red.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogAlert) // Icono nativo de alerta del sistema
            .SetOngoing(true) // ¡Esto hace que el usuario no pueda deslizarla para borrarla!
            .SetPriority(NotificationCompat.PriorityHigh)
            .Build();

        // 4. Arrancamos el servicio y lo atamos a la notificación
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        // 5. Enviamos la primera ubicación de forma inmediata usando la coordenada ya calculada
        _ = EnviarUbicacionInicial(latInicial, lonInicial);

        // 6. Inicializar la base de datos offline
        _ = OfflineDatabase.Init();

        // 7. Iniciar nuestro Bucle de 2 minutos (120,000 milisegundos)
        _timer = new System.Timers.Timer(120000);
        _timer.Elapsed += async (sender, e) => await EnviarUbicacionSilenciosa();
        _timer.Start();

        // Le decimos a Android: "Si te quedas sin RAM y me matas, revíveme de inmediato"
        return StartCommandResult.Sticky;
    }

    private async Task EnviarUbicacionInicial(double lat, double lon)
    {
        try
        {
            if (_currentAlertId != 0 && lat != 0 && lon != 0)
            {
                await PostLocationToServer(_currentAlertId, lat, lon);
            }
        }
        catch (Exception)
        {
            // Falla silenciosa: si el primer envío falla, el temporizador seguirá intentando cada 2 minutos.
        }
    }

    private async Task EnviarUbicacionSilenciosa()
    {
        // Validar si hay conexión a internet
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            // 📱 SIN INTERNET: Guardar la ubicación actual en la BD offline
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null && _currentAlertId != 0)
                {
                    await OfflineDatabase.SaveLocationAsync(_currentAlertId, location.Latitude, location.Longitude);
                }
            }
            catch (Exception)
            {
                // Falla silenciosa: si no hay GPS tampoco, simplemente esperamos al siguiente ciclo
            }
            return;
        }

        // 🌐 CON INTERNET: Enviar la ubicación actual
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
            var location = await Geolocation.Default.GetLocationAsync(request);

            if (location != null && _currentAlertId != 0)
            {
                bool success = await PostLocationToServer(_currentAlertId, location.Latitude, location.Longitude);

                // Si el envío fue exitoso, procesar la cola offline
                if (success)
                {
                    await ProcessOfflineQueue();
                }
            }
        }
        catch (Exception)
        {
            // Falla silenciosa: Si entra a un túnel sin GPS, el servicio no crashea, simplemente lo intenta de nuevo a los 2 minutos.
        }
    }

    /// <summary>
    /// Envía una ubicación al servidor. Retorna true si fue exitoso (código 2xx).
    /// </summary>
    private async Task<bool> PostLocationToServer(int alertId, double lat, double lon)
    {
        try
        {
            var trackData = new { alert_id = alertId, latitude = lat, longitude = lon };
            var json = JsonSerializer.Serialize(trackData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/alerts/track", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Envía un lote de ubicaciones al servidor en un único POST.
    /// Esto es más eficiente que enviar cada una individualmente.
    /// Retorna true si fue exitoso (código 2xx).
    /// </summary>
    private async Task<bool> PostBatchLocationsToServer(List<PendingLocation> pendingLocations)
    {
        try
        {
            if (pendingLocations == null || pendingLocations.Count == 0)
                return true; // Consideramos como éxito si no hay nada que enviar

            // Proyectar a formato limpio
            var batchData = pendingLocations.Select(p => new 
            { 
                alert_id = p.AlertId, 
                latitude = p.Latitude, 
                longitude = p.Longitude 
            }).ToList();

            var json = JsonSerializer.Serialize(batchData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/alerts/track/batch", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Procesa la cola de ubicaciones pendientes almacenadas offline.
    /// Envía todas en un único batch POST y las elimina si son exitosas.
    /// </summary>
    private async Task ProcessOfflineQueue()
    {
        try
        {
            var pendingLocations = await OfflineDatabase.GetPendingLocationsAsync();

            // Si hay elementos pendientes, enviar en batch
            if (pendingLocations.Any())
            {
                bool success = await PostBatchLocationsToServer(pendingLocations);

                if (success)
                {
                    // Borrar todas las ubicaciones procesadas de una sola vez
                    await OfflineDatabase.DeleteAllLocationsAsync(pendingLocations);
                }
                // Si falla el batch, se reintentará en el próximo ciclo de 2 minutos
            }
        }
        catch (Exception)
        {
            // Falla silenciosa: si hay error procesando la cola, se reintentará en el próximo ciclo
        }
    }

    // 🛑 SE EJECUTA CUANDO PRESIONAN EL BOTÓN "ESTOY A SALVO"
    public override void OnDestroy()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _httpClient?.Dispose();
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Rastreo de Emergencia", NotificationImportance.High)
            {
                Description = "Canal para notificar que el rastreo continuo está activo"
            };
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }
    }
}