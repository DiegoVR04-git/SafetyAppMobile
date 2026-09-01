using System.Text;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Maps;
using SkiaSharp.Extended.UI.Controls;

namespace SafetyAppMobile;

public partial class DashboardPage : ContentPage
{
    private readonly int _currentUserId;
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    private CancellationTokenSource _cancellationTokenSource;
    private const int HoldDurationMs = 2000;
    private int _currentAlertId;

    public DashboardPage()
    {
        InitializeComponent();
        _currentUserId = Preferences.Default.Get("current_user_id", 0);
        _httpClient = new HttpClient();
    }

    protected override void OnDisappearing()
    {
        App.EmergencyAlertActivated -= OnEmergencyAlertActivated;
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CargarSaludoPersonalizado();

        try
        {
            App.EmergencyAlertActivated -= OnEmergencyAlertActivated;
            App.EmergencyAlertActivated += OnEmergencyAlertActivated;

#if ANDROID
            try
            {
                var context = Android.App.Application.Context;
                var intent = new Android.Content.Intent(context, typeof(SafetyAppMobile.Platforms.Android.VolumeListeningService));
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                    context.StartForegroundService(intent);
                else
                    context.StartService(intent);
            }
            catch (Exception serviceEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Error starting VolumeListeningService: {serviceEx.Message}");
            }
#endif

            int activeAlertId = Preferences.Default.Get("active_alert_id", 0);

            if (activeAlertId == -1)
            {
                Preferences.Default.Remove("active_alert_id");
                activeAlertId = 0;
            }

            if (activeAlertId != 0)
            {
                ActivarModoEmergenciaUI(activeAlertId);
            }
            else
            {
                DesactivarModoEmergenciaUI();
            }

            try
            {
                await SincronizarContactoOffline();
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Error in SincronizarContactoOffline: {syncEx.Message}");
            }

            try
            {
                await SolicitarPermisosSeguridad();
            }
            catch (Exception permEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Error in SolicitarPermisosSeguridad: {permEx.Message}");
            }

            try
            {
                await CentrarMapaInicial();
            }
            catch (Exception mapEx)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Error in CentrarMapaInicial: {mapEx.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] Critical error in OnAppearing: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnEmergencyAlertActivated(int alertId)
    {
        ActivarModoEmergenciaUI(alertId);
    }

    private void ActivarModoEmergenciaUI(int alertId)
    {
        _currentAlertId = alertId;
        Preferences.Default.Set("active_alert_id", alertId);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PanicButton.IsEnabled = false;
            PanicButton.Opacity = 0.5;
            SafetyButton.IsVisible = true;
            StatusLabel.Text = "🚨 RASTREO EN PROCESO";
            StatusLabel.TextColor = Colors.Red;
            HoldProgressBar.Opacity = 0;

            if (StatusLottie != null)
                StatusLottie.Source = new SKFileLottieImageSource { File = "alert.json" };
        });
    }

    private void DesactivarModoEmergenciaUI()
    {
        _currentAlertId = 0;
        Preferences.Default.Remove("active_alert_id");

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PanicButton.IsEnabled = true;
            PanicButton.Opacity = 1.0;
            SafetyButton.IsVisible = false;
            StatusLabel.Text = "Sistema Listo y Seguro";
            StatusLabel.TextColor = Color.FromArgb("#2E7D32");

            if (StatusLottie != null)
                StatusLottie.Source = new SKFileLottieImageSource { File = "shield.json" };
        });
    }

    private async void OnSafetyButtonClicked(object sender, EventArgs e)
    {
        try
        {
            int activeAlertId = _currentAlertId != 0 ? _currentAlertId : Preferences.Default.Get("active_alert_id", 0);

            if (activeAlertId == 0)
            {
                await DisplayAlert("ℹ️ Información", "No hay alerta activa en este momento.", "OK");
                return;
            }

            StatusLabel.Text = "📤 Desactivando alerta en el servidor...";
            StatusLabel.TextColor = Colors.Orange;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await DisplayAlert("⚠️ Sin Conexión", "No hay conexión a internet.", "OK");
                DesactivarModoEmergenciaUI();
                return;
            }

            var url = $"{BaseUrl}/alerts/{activeAlertId}";
            var request = new HttpRequestMessage(HttpMethod.Put, url);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
#if ANDROID
                try
                {
                    var context = Android.App.Application.Context;
                    var intent = new Android.Content.Intent(context, typeof(SafetyAppMobile.Platforms.Android.AndroidLocationService));
                    context.StopService(intent);
                }
                catch (Exception gpsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SafetyButton] Error al detener GPS: {gpsEx.Message}");
                }
#endif
                DesactivarModoEmergenciaUI();
                await DisplayAlert("✅ Éxito", "Alerta desactivada correctamente.", "OK");
                ResetPanicState("Sistema Seguro - Alerta Finalizada", Color.FromArgb("#2E7D32"));

                response?.Dispose();
                request?.Dispose();
            }
            else
            {
                await DisplayAlert("❌ Error", $"Código: {response.StatusCode}", "OK");
                ResetPanicState($"Error: {response.StatusCode}", Colors.Red);

                response?.Dispose();
                request?.Dispose();
            }
        }
        catch (HttpRequestException)
        {
            await DisplayAlert("❌ Error de Red", "No se pudo conectar con el servidor.", "OK");
            ResetPanicState("Error de conexión", Colors.Red);
        }
        catch (TaskCanceledException)
        {
            await DisplayAlert("⏱️ Timeout", "La petición tardó demasiado.", "OK");
            ResetPanicState("Timeout en la conexión", Colors.Red);
        }
        catch (Exception ex)
        {
            await DisplayAlert("❌ Error", $"Error: {ex.Message}", "OK");
            ResetPanicState("Error inesperado", Colors.Red);
        }
    }

    private async Task TriggerPanicAlert()
    {
        StatusLabel.Text = "📍 Calibrando GPS exacto...";
        StatusLabel.TextColor = Colors.Orange;
        double lat = 0, lon = 0;

        try
        {
            var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(5)));
            if (loc != null)
            {
                lat = loc.Latitude;
                lon = loc.Longitude;
                if (UserMap != null)
                    UserMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(lat, lon), Distance.FromKilometers(0.5)));
            }
        }
        catch (Exception geoEx)
        {
            System.Diagnostics.Debug.WriteLine($"[TriggerPanicAlert] Geolocation error: {geoEx.Message}");
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await DispararAlertaOffline(lat, lon);
            return;
        }

        Preferences.Default.Set("active_alert_id", -1);
        StatusLabel.Text = "📡 Creando alerta en el servidor...";

        // 🌟 Leemos el correo configurado para el S.O.S.
        string sosEmail = Preferences.Default.Get("SosEmail", "anonimo");

        var alertData = new
        {
            user_id = _currentUserId,
            latitude = lat,
            longitude = lon,
            sos_email = sosEmail
        };
        var json = JsonSerializer.Serialize(alertData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/alerts", content);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseContent);
                int alertId = doc.RootElement.GetProperty("alert").GetProperty("alert_id").GetInt32();

                ActivarModoEmergenciaUI(alertId);
                await Task.Delay(2000);
                StartTracking(alertId, lat, lon);

                ResetPanicState("¡S.O.S ENVIADO Y RASTREO ACTIVO!", Colors.Red);
            }
            else
            {
                DesactivarModoEmergenciaUI();
                ResetPanicState("Error al enviar alerta", Colors.Red);
            }
        }
        catch
        {
            DesactivarModoEmergenciaUI();
            await DispararAlertaOffline(lat, lon);
        }
    }

    private void StartTracking(int alertId, double lat, double lon)
    {
#if ANDROID
        var context = Android.App.Application.Context;
        var intent = new Android.Content.Intent(context, typeof(SafetyAppMobile.Platforms.Android.AndroidLocationService));
        intent.PutExtra("alert_id", alertId);
        intent.PutExtra("lat", lat);
        intent.PutExtra("lon", lon);

        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
#endif
    }

    private async Task CentrarMapaInicial()
    {
        try
        {
            if (UserMap == null) return;

            var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
            if (loc != null && UserMap != null)
                UserMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(loc.Latitude, loc.Longitude), Distance.FromKilometers(1)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] Error in CentrarMapaInicial: {ex.Message}");
        }
    }

    private async Task SolicitarPermisosSeguridad()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            var statusNotificaciones = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (statusNotificaciones != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
    }

    private async Task SincronizarContactoOffline()
    {
        try
        {
            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var response = await _httpClient.GetAsync($"{BaseUrl}/contacts/{_currentUserId}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseContent);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("contacts", out var contactsArray))
                    {
                        if (contactsArray.GetArrayLength() > 0)
                        {
                            string primerNumero = contactsArray[0].GetProperty("phone_number").GetString();
                            Preferences.Default.Set("offline_contact_phone", primerNumero);
                        }
                        else
                        {
                            Preferences.Default.Remove("offline_contact_phone");
                        }
                    }
                }
            }
        }
        catch { }
    }

    private async void OnPanicButtonPressed(object sender, EventArgs e)
    {
        string contactoEmergencia = Preferences.Default.Get("offline_contact_phone", "");

        if (string.IsNullOrEmpty(contactoEmergencia))
        {
            CustomAlertOverlay.IsVisible = true;
            CustomAlertOverlay.Opacity = 0;
            await CustomAlertOverlay.FadeTo(1, 250);
            return;
        }

        await PanicButton.ScaleTo(0.92, 100, Easing.CubicOut);

        _cancellationTokenSource = new CancellationTokenSource();
        StatusLabel.Text = "Mantén presionado...";
        StatusLabel.TextColor = Colors.Orange;
        HoldProgressBar.Opacity = 1;
        HoldProgressBar.Progress = 0;

        try
        {
            _ = HoldProgressBar.ProgressTo(1, (uint)HoldDurationMs, Easing.Linear);
            await Task.Delay(HoldDurationMs, _cancellationTokenSource.Token);
            await TriggerPanicAlert();
        }
        catch (TaskCanceledException)
        {
            ResetPanicState("Alerta Cancelada. Sistema Seguro.", Colors.Gray);
        }
    }

    private async void OnPanicButtonReleased(object sender, EventArgs e)
    {
        await PanicButton.ScaleTo(1.0, 100, Easing.BounceOut);
        _cancellationTokenSource?.Cancel();
    }

    private async Task DispararAlertaOffline(double lat, double lon)
    {
        string num = Preferences.Default.Get("offline_contact_phone", "");
        if (string.IsNullOrEmpty(num))
        {
            await DisplayAlert("⚠️ Aviso", "No hay red y no tienes contacto de respaldo.", "OK");
            return;
        }
        var sms = new SmsMessage($"🚨 EMERGENCIA: Mi ubicación: http://maps.google.com/?q={lat},{lon}", new[] { num });
        await Sms.Default.ComposeAsync(sms);
        ResetPanicState("SMS de emergencia preparado", Colors.Orange);
    }

    private void ResetPanicState(string message, Color color)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = message;
            StatusLabel.TextColor = color;
            HoldProgressBar.Progress = 0;
            HoldProgressBar.Opacity = 0;
        });
    }

    private void CargarSaludoPersonalizado()
    {
        string nombreCompleto = Preferences.Default.Get("UserFullName", "");
        string primerNombre = string.IsNullOrWhiteSpace(nombreCompleto) ? "Valiente" : nombreCompleto.Split(' ')[0];
        GreetingLabel.Text = $"Hola, {primerNombre} 💜";
    }

    private async void OnGoToContactsClicked(object sender, EventArgs e)
    {
        await CustomAlertOverlay.FadeTo(0, 200);
        CustomAlertOverlay.IsVisible = false;
        await Shell.Current.GoToAsync("//contacts");
    }

    private async void OnCancelAlertClicked(object sender, EventArgs e)
    {
        await CustomAlertOverlay.FadeTo(0, 200);
        CustomAlertOverlay.IsVisible = false;
    }
}