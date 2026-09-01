using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text;
using Microsoft.Maui.Storage;

namespace SafetyAppMobile;

public partial class ProfilePage : ContentPage
{
    private readonly int _currentUserId;

    public ProfilePage()
    {
        InitializeComponent();
        _currentUserId = Preferences.Default.Get("current_user_id", 0);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CargarDatosDelPerfil();
    }

    private void CargarDatosDelPerfil()
    {
        string nombreGuardado = Preferences.Default.Get("UserFullName", "");
        string correoPersonalGuardado = Preferences.Default.Get("UserEmail", "");
        string correoSosGuardado = Preferences.Default.Get("SosEmail", "");
        string telefonoGuardado = Preferences.Default.Get("UserPhone", "");

        // 1. Asignar los datos actuales como Placeholders
        NameEntry.Placeholder = string.IsNullOrEmpty(nombreGuardado) ? "Tu nombre" : nombreGuardado;
        PersonalEmailEntry.Placeholder = string.IsNullOrEmpty(correoPersonalGuardado) ? "tu@correo.com" : correoPersonalGuardado;
        SosEmailEntry.Placeholder = string.IsNullOrEmpty(correoSosGuardado) ? "emergencia@correo.com" : correoSosGuardado;

        // Actualizar la inicial y el nombre del Header
        if (!string.IsNullOrEmpty(nombreGuardado))
        {
            HeaderNameLabel.Text = nombreGuardado.Split(' ')[0];
            AvatarInitialLabel.Text = nombreGuardado.Substring(0, 1).ToUpper();
        }

        // 2. Extraer la lada (+52 o +1) hacia el Label personalizado
        if (telefonoGuardado.StartsWith("+52"))
        {
            SelectedCountryCodeLabel.Text = "+52";
            PhoneEntry.Placeholder = telefonoGuardado.Substring(3);
        }
        else if (telefonoGuardado.StartsWith("+1"))
        {
            SelectedCountryCodeLabel.Text = "+1";
            PhoneEntry.Placeholder = telefonoGuardado.Substring(2);
        }
        else
        {
            SelectedCountryCodeLabel.Text = "+52";
            PhoneEntry.Placeholder = string.IsNullOrEmpty(telefonoGuardado) ? "A 10 dígitos" : telefonoGuardado;
        }
    }

    // ==========================================
    // LÓGICA DEL SELECTOR DE PAÍS
    // ==========================================
    private async void OnOpenCountryCodeTapped(object sender, EventArgs e)
    {
        CountryCodeOverlay.IsVisible = true;
        CountryCodeOverlay.Opacity = 0;
        await CountryCodeOverlay.FadeTo(1, 200);
    }

    private async void OnCloseCountryCodeOverlay(object sender, EventArgs e)
    {
        await CountryCodeOverlay.FadeTo(0, 200);
        CountryCodeOverlay.IsVisible = false;
    }

    private void OnCountrySelected(object sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            if (btn.Text.Contains("+52"))
            {
                SelectedCountryCodeLabel.Text = "+52";
            }
            else if (btn.Text.Contains("+1"))
            {
                SelectedCountryCodeLabel.Text = "+1";
            }
        }

        OnCloseCountryCodeOverlay(null, null);
    }

    // ==========================================
    // LÓGICA DE GUARDAR Y CERRAR SESIÓN
    // ==========================================
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Rescatamos los datos actuales desde Preferences
        string nombreActual = Preferences.Default.Get("UserFullName", "");
        string correoPersonalActual = Preferences.Default.Get("UserEmail", "");
        string correoSosActual = Preferences.Default.Get("SosEmail", "");
        string telActual = Preferences.Default.Get("UserPhone", "");

        string nuevoNombre = string.IsNullOrWhiteSpace(NameEntry.Text) ? nombreActual : NameEntry.Text.Trim();
        string nuevoCorreoPersonal = string.IsNullOrWhiteSpace(PersonalEmailEntry.Text) ? correoPersonalActual : PersonalEmailEntry.Text.Trim();
        string nuevoCorreoSos = string.IsNullOrWhiteSpace(SosEmailEntry.Text) ? correoSosActual : SosEmailEntry.Text.Trim();

        // Extraemos la lada seleccionada desde el Label
        string ladaSeleccionada = SelectedCountryCodeLabel.Text ?? "+52";
        string numeroRaw = string.IsNullOrWhiteSpace(PhoneEntry.Text) ? "" : PhoneEntry.Text.Trim();

        string nuevoTelefono;
        if (string.IsNullOrEmpty(numeroRaw))
        {
            string numeroViejo = telActual.StartsWith("+52") ? telActual.Substring(3) :
                                 telActual.StartsWith("+1") ? telActual.Substring(2) : telActual;
            nuevoTelefono = ladaSeleccionada + numeroViejo;
        }
        else
        {
            nuevoTelefono = ladaSeleccionada + numeroRaw;
        }

        // Validaciones
        if (string.IsNullOrWhiteSpace(nuevoNombre) || string.IsNullOrWhiteSpace(nuevoTelefono))
        {
            await DisplayAlert("Error", "El nombre y teléfono no pueden quedar vacíos.", "OK");
            return;
        }

        if (!IsValidEmail(nuevoCorreoPersonal))
        {
            await DisplayAlert("Error", "Ingresa un correo personal válido.", "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(nuevoCorreoSos) && !IsValidEmail(nuevoCorreoSos))
        {
            await DisplayAlert("Error", "Ingresa un correo S.O.S válido.", "OK");
            return;
        }

        SaveBtn.Text = "Guardando...";
        SaveBtn.IsEnabled = false;

        // Construimos el Payload JSON conectando con el modelo de FastAPI
        var updateData = new
        {
            full_name = nuevoNombre,
            phone_number = nuevoTelefono,
            email = nuevoCorreoPersonal,
            sos_email = nuevoCorreoSos
        };

        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var client = new HttpClient();
            var response = await client.PutAsync($"https://safety-app-api.onrender.com/users/{_currentUserId}/profile", content);

            if (response.IsSuccessStatusCode)
            {
                // Actualizamos las preferencias locales
                Preferences.Default.Set("UserFullName", nuevoNombre);
                Preferences.Default.Set("UserEmail", nuevoCorreoPersonal);
                Preferences.Default.Set("SosEmail", nuevoCorreoSos);
                Preferences.Default.Set("UserPhone", nuevoTelefono);

                // Limpiamos las cajas de texto
                NameEntry.Text = "";
                PhoneEntry.Text = "";
                PersonalEmailEntry.Text = "";
                SosEmailEntry.Text = "";

                CargarDatosDelPerfil();

                await DisplayAlert("¡Éxito!", "Tu perfil ha sido actualizado.", "OK");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                await DisplayAlert("⚠️ Aviso", "Ese número de teléfono o correo ya está registrado en otra cuenta. Intenta con otro.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Ocurrió un problema en el servidor.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error de red", "Verifica tu conexión a internet.", "OK");
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Error: {ex.Message}");
        }
        finally
        {
            SaveBtn.Text = "Guardar Cambios";
            SaveBtn.IsEnabled = true;
        }
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailPattern);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Cerrar Sesión", "¿Estás seguro de que deseas salir?", "Sí", "Cancelar");
        if (!confirm) return;

        int currentUserId = Preferences.Default.Get("current_user_id", 0);
        string onboardingKey = $"IsFirstLaunch_User_{currentUserId}";
        bool hasCompletedOnboarding = Preferences.Default.Get(onboardingKey, true);

        Preferences.Default.Clear();

        if (currentUserId != 0)
        {
            Preferences.Default.Set(onboardingKey, hasCompletedOnboarding);
        }

        try
        {
            await Shell.Current.GoToAsync("//login", false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Logout navigation error: {ex.Message}");
        }
    }
}