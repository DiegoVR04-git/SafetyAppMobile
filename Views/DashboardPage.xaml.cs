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
    private bool _isPulsing = false;

    // Herramienta para pausar el código hasta que el usuario responda la alerta custom
    private TaskCompletionSource<bool> _alertTcs;

    public DashboardPage()
    {
        InitializeComponent();
        _currentUserId = Preferences.Default.Get("current_user_id", 0);
        _httpClient = new HttpClient();
    }

    protected override void OnDisappearing()
    {
        _isPulsing = false;
        App.EmergencyAlertActivated -= OnEmergencyAlertActivated;
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CargarSaludoPersonalizado();

        if (!_isPulsing)
        {
            _isPulsing = true;
            StartPulseLoop();
        }

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
                ActivarModoEmergenciaUI(activeAlertId);
            else
                DesactivarModoEmergenciaUI();

            try { await SincronizarContactoOffline(); } catch { }
            try { await SolicitarPermisosSeguridad(); } catch { }
            try { await CentrarMapaInicial(); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] Critical error in OnAppearing: {ex.Message}");
        }
    }

    // ========================================================
    // MOTOR DE ALERTAS PERSONALIZADO (REEMPLAZA A DISPLAYALERT)
    // ========================================================
    private async Task<bool> ShowCustomAlert(string icon, string title, string message, string confirmText, string cancelText = null, bool isDestructive = false)
    {
        AlertIconLabel.Text = icon;
        AlertTitleLabel.Text = title;
        AlertMessageLabel.Text = message;

        AlertConfirmBtn.Text = confirmText;
        AlertConfirmBtn.BackgroundColor = isDestructive ? Color.FromArgb("#E5484D") : Color.FromArgb("#37246B"); // Rojo o Morado

        if (!string.IsNullOrEmpty(cancelText))
        {
            AlertCancelBtn.Text = cancelText;
            AlertCancelBtn.IsVisible = true;
        }
        else
        {
            AlertCancelBtn.IsVisible = false;
        }

        UniversalAlertOverlay.IsVisible = true;
        await UniversalAlertOverlay.FadeTo(1, 200);

        _alertTcs = new TaskCompletionSource<bool>();
        return await _alertTcs.Task;
    }

    private async void OnAlertConfirmClicked(object sender, EventArgs e)
    {
        await UniversalAlertOverlay.FadeTo(0, 200);
        UniversalAlertOverlay.IsVisible = false;
        _alertTcs?.TrySetResult(true);
    }

    private async void OnAlertCancelClicked(object sender, EventArgs e)
    {
        await UniversalAlertOverlay.FadeTo(0, 200);
        UniversalAlertOverlay.IsVisible = false;
        _alertTcs?.TrySetResult(false);
    }
    // ========================================================

    private async void StartPulseLoop()
    {
        while (_isPulsing)
        {
            _ = PulseRing(Ring1);
            await Task.Delay(700);
            _ = PulseRing(Ring2);
            await Task.Delay(700);
            _ = PulseRing(Ring3);
            await Task.Delay(1400);
        }
    }

    private async Task PulseRing(Microsoft.Maui.Controls.Shapes.Ellipse ring)
    {
        if (ring == null) return;
        ring.Scale = 1.0;
        ring.Opacity = 0.6;

        var anim = new Animation(t =>
        {
            ring.Scale = 1.0 + t * 1.2;
            ring.Opacity = 0.6 * (1 - t);
        });

        anim.Commit(ring, "pulse", length: 2800, easing: Easing.CubicOut);
        await Task.Delay(2800);
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
            ShowWhatsAppResults(alertId);

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
            WhatsAppResultsCard.IsVisible = false;
            WhatsAppContactRows.Children.Clear();
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
                await ShowCustomAlert("ℹ️", "Información", "No hay alerta activa en este momento.", "Entendido");
                return;
            }

            StatusLabel.Text = "📤 Desactivando alerta...";
            StatusLabel.TextColor = Colors.Orange;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await ShowCustomAlert("⚠️", "Sin Conexión", "No hay conexión a internet para detener la alerta en la nube.", "Aceptar");
                ResetPanicState("Alerta activa · Cierre sin confirmar", Colors.Orange);
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
                catch { }
#endif
                DesactivarModoEmergenciaUI();
                WhatsAppResults.Clear(_currentUserId);
                await ShowCustomAlert("", "A Salvo", "Alerta SOS Suspendida", "Entendido");
                ResetPanicState("Sistema Seguro - Alerta Finalizada", Color.FromArgb("#2E7D32"));
            }
            else
            {
                await ShowCustomAlert("❌", "Error", $"Hubo un problema. Código: {response.StatusCode}", "Aceptar", null, true);
                ResetPanicState($"Error: {response.StatusCode}", Colors.Red);
            }
        }
        catch (HttpRequestException)
        {
            await ShowCustomAlert("❌", "Error de Red", "No se pudo conectar con el servidor.", "Aceptar", null, true);
            ResetPanicState("Error de conexión", Colors.Red);
        }
        catch (Exception ex)
        {
            await ShowCustomAlert("❌", "Error Inesperado", ex.Message, "Aceptar", null, true);
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
        catch { }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            ResetPanicState("Sin internet · Alerta no enviada", Colors.Orange);
            await ShowCustomAlert("⚠️", "Sin conexión", "Necesitas internet para enviar tu alerta por WhatsApp. Conéctate e inténtalo de nuevo.", "Entendido");
            return;
        }

        Preferences.Default.Set("active_alert_id", -1);
        StatusLabel.Text = "📡 Creando alerta en el servidor...";

        string sosEmail = Preferences.Default.Get("SosEmail", "anonimo");

        var alertData = new { user_id = _currentUserId, latitude = lat, longitude = lon, sos_email = sosEmail };
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

                WhatsAppResults.Save(_currentUserId, alertId, doc.RootElement);
                ActivarModoEmergenciaUI(alertId);
                StartTracking(alertId, lat, lon);

                ResetPanicState("Alerta activa · Consulta tus avisos", Colors.Red);
            }
            else
            {
                DesactivarModoEmergenciaUI();
                ResetPanicState("Error al enviar alerta", Colors.Red);
            }
        }
        catch
        {
            if (_currentAlertId > 0)
            {
                ResetPanicState("Alerta activa · Revisa el rastreo", Colors.Orange);
                await ShowCustomAlert("⚠️", "Alerta registrada", "La alerta se creó, pero hubo un problema al iniciar el rastreo. Los resultados de WhatsApp aparecen en la tarjeta de avisos.", "Entendido");
                return;
            }
            DesactivarModoEmergenciaUI();
            ResetPanicState("Envío sin confirmar", Colors.Orange);
            await ShowCustomAlert("⚠️", "Envío sin confirmar", "No pudimos confirmar el resultado de la alerta. Es posible que el servidor haya recibido la solicitud.", "Entendido");
        }
    }

    private void ShowWhatsAppResults(int alertId)
    {
        WhatsAppResultsCard.IsVisible = true;
        WhatsAppContactRows.Children.Clear();
        var results = WhatsAppResults.Load(_currentUserId, alertId);
        if (results == null)
        {
            WhatsAppSummaryLabel.Text = "Alerta registrada. No hay resultados de envío disponibles.";
            return;
        }
        if (results.Count == 0)
        {
            WhatsAppSummaryLabel.Text = "No se enviaron avisos: no hay contactos registrados.";
            return;
        }

        int accepted = results.Count(result => result.Status == "accepted");
        WhatsAppSummaryLabel.Text = $"WhatsApp aceptó {accepted} de {results.Count} solicitudes.";
        foreach (var result in results)
        {
            var row = new VerticalStackLayout { Spacing = 2 };
            row.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(result.Name) ? "Contacto" : result.Name,
                FontFamily = "PoppinsBold",
                FontSize = 14,
                TextColor = Color.FromArgb("#37246B")
            });
            row.Children.Add(new Label
            {
                Text = result.StatusText,
                FontSize = 13,
                TextColor = Color.FromArgb(result.StatusColor)
            });
            WhatsAppContactRows.Children.Add(row);
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
        catch { }
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

                    if (root.TryGetProperty("contacts", out var contactsArray) && contactsArray.GetArrayLength() > 0)
                    {
                        Preferences.Default.Set("offline_contact_phone", contactsArray[0].GetProperty("phone_number").GetString());
                    }
                    else
                    {
                        Preferences.Default.Remove("offline_contact_phone");
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
        GreetingLabel.Text = $"Hola, {primerNombre}";
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

    // ========================================================
    // LÓGICA DEL BOTÓN 911 (USANDO LA ALERTA CUSTOM)
    // ========================================================
    private async void OnCall911Clicked(object sender, EventArgs e)
    {
        await Call911Button.ScaleTo(0.95, 100, Easing.CubicOut);
        await Call911Button.ScaleTo(1.0, 100, Easing.BounceOut);

        bool confirmar = await ShowCustomAlert("🚨", "Emergencia 911", "¿Estás a punto de contactar al 911. ¿Deseas continuar?", "Sí, Llamar", "Cancelar", true);

        if (confirmar)
        {
            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open("911");
                }
                else
                {
                    await ShowCustomAlert("❌", "No Soportado", "Tu dispositivo no soporta llamadas telefónicas nativas.", "Aceptar", null, true);
                }
            }
            catch (Exception ex)
            {
                await ShowCustomAlert("❌", "Error", $"No se pudo realizar la llamada: {ex.Message}", "Aceptar", null, true);
            }
        }
    }
}
