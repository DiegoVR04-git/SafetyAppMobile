using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SafetyAppMobile;

public partial class RegisterPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    // Variables temporales para almacenar los datos mientras se verifica el correo
    private string _tempName, _tempEmail, _tempPhone, _tempPassword;

    public RegisterPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
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
    // LÓGICA DE REGISTRO
    // ==========================================
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        _tempName = NameEntry.Text?.Trim();
        _tempEmail = EmailEntry.Text?.Trim();
        var rawPhone = PhoneEntry.Text?.Trim();
        _tempPassword = PasswordEntry.Text;
        var confirmPassword = ConfirmPasswordEntry.Text;

        // Extraemos el código de país desde el Label estilizado
        string selectedCode = SelectedCountryCodeLabel.Text ?? "+52";

        if (string.IsNullOrWhiteSpace(_tempName) || string.IsNullOrWhiteSpace(_tempEmail) ||
            string.IsNullOrWhiteSpace(rawPhone) || string.IsNullOrWhiteSpace(_tempPassword) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            StatusLabel.Text = "Por favor, llena todos los campos.";
            return;
        }

        if (!_tempEmail.Contains("@"))
        {
            StatusLabel.Text = "Ingresa un correo electrónico válido.";
            return;
        }

        if (rawPhone.Length < 10)
        {
            StatusLabel.Text = "El número debe tener 10 dígitos exactos.";
            return;
        }

        string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$";
        if (!Regex.IsMatch(_tempPassword, passwordPattern))
        {
            StatusLabel.Text = "La contraseña debe tener mínimo 8 caracteres, 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial.";
            return;
        }

        if (_tempPassword != confirmPassword)
        {
            StatusLabel.Text = "Las contraseñas no coinciden.";
            return;
        }

        RegisterBtn.Text = "Enviando código...";
        RegisterBtn.IsEnabled = false;

        _tempPhone = selectedCode + rawPhone;

        try
        {
            // 1. Enviar solicitud del código al backend
            var codeData = new { email = _tempEmail, type = "register" };
            var jsonCode = JsonSerializer.Serialize(codeData);
            var contentCode = new StringContent(jsonCode, Encoding.UTF8, "application/json");

            var responseCode = await _httpClient.PostAsync($"{BaseUrl}/auth/send-register-code", contentCode);

            if (!responseCode.IsSuccessStatusCode)
            {
                var errorContent = await responseCode.Content.ReadAsStringAsync();
                string errorMessage = "Error al enviar el código.";

                try
                {
                    using var doc = JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                    {
                        errorMessage = detailProp.GetString() ?? errorMessage;
                    }
                }
                catch
                {
                    // Manejo si no es JSON
                }

                StatusLabel.Text = errorMessage;
                RegisterBtn.Text = "Unirse a la Red Segura";
                RegisterBtn.IsEnabled = true;
                return;
            }

            // 2. Mostrar nuestro Overlay de verificación
            OverlayInstructionLabel.Text = $"Ingresa el código de 6 dígitos que enviamos a:\n{_tempEmail}";
            VerificationCodeEntry.Text = "";
            VerificationOverlay.IsVisible = true;
            VerificationOverlay.Opacity = 0;
            await VerificationOverlay.FadeTo(1, 250);
        }
        catch (Exception)
        {
            StatusLabel.Text = "No hay conexión con el servidor.";
            RegisterBtn.Text = "Unirse a la Red Segura";
            RegisterBtn.IsEnabled = true;
        }
    }

    private async void OnConfirmVerificationClicked(object sender, EventArgs e)
    {
        string enteredCode = VerificationCodeEntry.Text?.Trim();
        OverlayErrorLabel.IsVisible = false;

        if (string.IsNullOrWhiteSpace(enteredCode) || enteredCode.Length < 6)
        {
            OverlayErrorLabel.Text = "Ingresa un código válido de 6 dígitos.";
            OverlayErrorLabel.IsVisible = true;
            return;
        }

        VerifyBtn.Text = "Verificando...";
        VerifyBtn.IsEnabled = false;

        try
        {
            var registerData = new
            {
                full_name = _tempName,
                email = _tempEmail,
                phone_number = _tempPhone,
                password = _tempPassword
            };

            var jsonReg = JsonSerializer.Serialize(registerData);
            var contentReg = new StringContent(jsonReg, Encoding.UTF8, "application/json");

            var responseVerify = await _httpClient.PostAsync($"{BaseUrl}/auth/verify-and-register?code={enteredCode}", contentReg);

            if (responseVerify.IsSuccessStatusCode)
            {
                await VerificationOverlay.FadeTo(0, 200);
                VerificationOverlay.IsVisible = false;

                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = "¡Cuenta creada con éxito!";

                Preferences.Default.Set("UserFullName", _tempName);

                await Task.Delay(1500);
                Application.Current.MainPage = new LoginPage();
            }
            else
            {
                var errorResponse = await responseVerify.Content.ReadAsStringAsync();
                string userMessage = "Código incorrecto. Intenta de nuevo.";

                try
                {
                    using var doc = JsonDocument.Parse(errorResponse);
                    if (doc.RootElement.TryGetProperty("detail", out var detailProp))
                    {
                        userMessage = detailProp.GetString() ?? userMessage;
                    }
                }
                catch { }

                OverlayErrorLabel.Text = userMessage;
                OverlayErrorLabel.IsVisible = true;
                VerificationCodeEntry.Text = "";
            }
        }
        catch (Exception)
        {
            OverlayErrorLabel.Text = "Error de conexión con el servidor.";
            OverlayErrorLabel.IsVisible = true;
        }
        finally
        {
            VerifyBtn.Text = "Verificar y Crear";
            VerifyBtn.IsEnabled = true;
        }
    }

    private async void OnCancelVerificationClicked(object sender, EventArgs e)
    {
        await VerificationOverlay.FadeTo(0, 200);
        VerificationOverlay.IsVisible = false;
        RegisterBtn.Text = "Unirse a la Red Segura";
        RegisterBtn.IsEnabled = true;
        StatusLabel.Text = "Registro cancelado.";
    }

    private void OnBackToLoginTapped(object sender, TappedEventArgs e)
    {
        Application.Current.MainPage = new LoginPage();
    }
}