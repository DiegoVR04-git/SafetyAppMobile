using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text;

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
        string correoGuardado = Preferences.Default.Get("UserEmail", "");
        string telefonoGuardado = Preferences.Default.Get("UserPhone", "");

        // 1. Asignar los datos actuales como Placeholders
        NameEntry.Placeholder = string.IsNullOrEmpty(nombreGuardado) ? "Tu nombre" : nombreGuardado;
        EmailEntry.Placeholder = string.IsNullOrEmpty(correoGuardado) ? "ejemplo@correo.com" : correoGuardado;

        // 2. Extraer la lada (+52 o +1) y asignar el número al Placeholder
        if (telefonoGuardado.StartsWith("+52"))
        {
            CountryCodePicker.SelectedIndex = 0; // Selecciona +52
            PhoneEntry.Placeholder = telefonoGuardado.Substring(3); // Quita los primeros 3 caracteres
        }
        else if (telefonoGuardado.StartsWith("+1"))
        {
            CountryCodePicker.SelectedIndex = 1; // Selecciona +1
            PhoneEntry.Placeholder = telefonoGuardado.Substring(2); // Quita los primeros 2 caracteres
        }
        else
        {
            CountryCodePicker.SelectedIndex = 0;
            PhoneEntry.Placeholder = string.IsNullOrEmpty(telefonoGuardado) ? "A 10 dígitos" : telefonoGuardado;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Si el usuario no escribió nada nuevo (el campo está vacío), rescatamos el dato actual desde Preferences
        string nombreActual = Preferences.Default.Get("UserFullName", "");
        string correoActual = Preferences.Default.Get("UserEmail", "");
        string telActual = Preferences.Default.Get("UserPhone", "");

        string nuevoNombre = string.IsNullOrWhiteSpace(NameEntry.Text) ? nombreActual : NameEntry.Text.Trim();
        string nuevoCorreo = string.IsNullOrWhiteSpace(EmailEntry.Text) ? correoActual : EmailEntry.Text.Trim();

        // Armar el nuevo teléfono verificando si el usuario escribió algo
        string ladaSeleccionada = CountryCodePicker.SelectedItem?.ToString() ?? "+52";
        string numeroRaw = string.IsNullOrWhiteSpace(PhoneEntry.Text) ? "" : PhoneEntry.Text.Trim();

        string nuevoTelefono;
        if (string.IsNullOrEmpty(numeroRaw))
        {
            // Si no escribió un número nuevo, asumimos que quiere conservar el actual,
            // PERO tomamos en cuenta si cambió la lada en el Picker
            string numeroViejo = telActual.StartsWith("+52") ? telActual.Substring(3) :
                                 telActual.StartsWith("+1") ? telActual.Substring(2) : telActual;
            nuevoTelefono = ladaSeleccionada + numeroViejo;
        }
        else
        {
            nuevoTelefono = ladaSeleccionada + numeroRaw;
        }

        if (string.IsNullOrWhiteSpace(nuevoNombre) || string.IsNullOrWhiteSpace(nuevoTelefono))
        {
            await DisplayAlert("Error", "Nombre y teléfono no pueden quedar vacíos.", "OK");
            return;
        }

        if (!IsValidEmail(nuevoCorreo))
        {
            await DisplayAlert("Error", "Ingresa un correo electrónico válido.", "OK");
            return;
        }

        SaveBtn.Text = "Guardando...";
        SaveBtn.IsEnabled = false;

        var updateData = new
        {
            full_name = nuevoNombre,
            phone_number = nuevoTelefono,
            email = nuevoCorreo
        };

        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var client = new HttpClient();
            var response = await client.PutAsync($"https://safety-app-api.onrender.com/users/{_currentUserId}/profile", content);

            if (response.IsSuccessStatusCode)
            {
                Preferences.Default.Set("UserFullName", nuevoNombre);
                Preferences.Default.Set("UserEmail", nuevoCorreo);
                Preferences.Default.Set("UserPhone", nuevoTelefono);

                // Limpiamos las cajas de texto porque ahora los nuevos datos pasarán a ser los Placeholders
                NameEntry.Text = "";
                PhoneEntry.Text = "";
                EmailEntry.Text = "";

                CargarDatosDelPerfil(); // Recargamos para que los Placeholders se actualicen visualmente

                await DisplayAlert("¡Éxito!", "Tu perfil ha sido actualizado.", "OK");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                await DisplayAlert("⚠️ Aviso", "Ese número de teléfono ya está registrado en otra cuenta. Intenta con otro.", "OK");
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