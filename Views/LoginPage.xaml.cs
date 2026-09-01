using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage; // Herramienta clave para guardar la sesión

namespace SafetyAppMobile;

public partial class LoginPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    // Molde ACTUALIZADO para entender la respuesta de tu servidor Python
    public class LoginResponse
    {
        public string message { get; set; }
        public int user_id { get; set; }
        public string full_name { get; set; }
        public string email { get; set; }
        public string sos_email { get; set; } // <-- ¡NUEVO: Agregamos el correo SOS!
    }

    public LoginPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // On first app start, check if user should auto-navigate to dashboard
        bool rememberMe = Preferences.Default.Get("remember_me", false);
        int currentUserId = Preferences.Default.Get("current_user_id", 0);

        if (rememberMe && currentUserId != 0)
        {
            // User already logged in - check if they've completed onboarding
            string onboardingKey = $"IsFirstLaunch_User_{currentUserId}";
            bool isFirstLaunch = Preferences.Default.Get(onboardingKey, true);

            try
            {
                // Longer delay to ensure Shell is fully initialized
                await Task.Delay(500);

                // ¡ARREGLO DE NAVEGACIÓN!: Reconstruimos el Shell si no existe
                if (Shell.Current == null)
                {
                    Application.Current.MainPage = new AppShell();
                }

                if (isFirstLaunch)
                {
                    await Shell.Current.GoToAsync("///onboarding", false);
                }
                else
                {
                    await Shell.Current.GoToAsync("///dashboard", false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginPage] OnAppearing navigation error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // ==========================================
    // MÉTODOS DEL MODAL DE CÓDIGO DE PAÍS
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
    // LÓGICA DE LOGIN
    // ==========================================

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        var rawPhone = PhoneEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        // 1. Extraemos el código desde nuestro Label personalizado
        string selectedCode = SelectedCountryCodeLabel.Text ?? "+52";

        // 2. Validar que no haya campos vacíos y que el número tenga 10 dígitos
        if (string.IsNullOrWhiteSpace(rawPhone) || rawPhone.Length < 10 || string.IsNullOrWhiteSpace(password))
        {
            StatusLabel.Text = "Ingresa tu número a 10 dígitos y contraseña.";
            return;
        }

        LoginBtn.Text = "Iniciando sesión...";
        LoginBtn.IsEnabled = false;

        // 3. LA FUSIÓN: Armamos el número tal cual está en la base de datos
        string fullPhoneNumber = selectedCode + rawPhone;

        // 4. Enviamos el número completo a la nube
        var loginData = new { phone_number = fullPhoneNumber, password = password };
        var json = JsonSerializer.Serialize(loginData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // USAMOS TU CLASE LoginResponse DE FORMA SEGURA
                var result = JsonSerializer.Deserialize<LoginResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // VALIDACIÓN DE SEGURIDAD: Evita el NullReferenceException
                if (result == null || result.user_id == 0)
                {
                    StatusLabel.Text = "Error al leer los datos del servidor.";
                    return;
                }

                int userId = result.user_id;

                // 1. Guardamos los datos principales
                Preferences.Default.Set("current_user_id", userId);
                Preferences.Default.Set("UserFullName", result.full_name ?? "");
                Preferences.Default.Set("UserPhone", fullPhoneNumber);

                // 2. GUARDAMOS AMBOS CORREOS
                Preferences.Default.Set("UserEmail", result.email ?? "");
                Preferences.Default.Set("SosEmail", result.sos_email ?? "");

                // 3. Guardamos si quiere ser recordado
                Preferences.Default.Set("remember_me", RememberMeCheckBox.IsChecked);

                // 4. Verificar si es el primer lanzamiento PARA ESTE USUARIO
                string onboardingKey = $"IsFirstLaunch_User_{userId}";
                bool isFirstLaunch = Preferences.Default.Get(onboardingKey, true);

                // ¡ARREGLO DE NAVEGACIÓN!: Reconstruimos el Shell si no existe
                if (Shell.Current == null)
                {
                    Application.Current.MainPage = new AppShell();
                }

                // Ahora sí podemos navegar tranquilamente usando las rutas de Shell
                if (isFirstLaunch)
                {
                    await Shell.Current.GoToAsync("///onboarding", false);
                }
                else
                {
                    await Shell.Current.GoToAsync("///dashboard", false);
                }
            }
            else
            {
                // Entra aquí si el backend devuelve 404 (No existe) o 401 (Contraseña mal)
                StatusLabel.Text = "Teléfono o contraseña incorrectos.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginPage] Login error: {ex.Message}");
            StatusLabel.Text = "Sin conexión con el servidor.";
        }
        finally
        {
            // Siempre volvemos a habilitar el botón
            LoginBtn.Text = "Entrar";
            LoginBtn.IsEnabled = true;
        }
    }

    private void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        Application.Current.MainPage = new RegisterPage();
    }

    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ForgotPasswordPage());
    }
}