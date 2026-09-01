using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SafetyAppMobile;

public partial class NewPasswordPage : ContentPage
{
    private readonly string _email;
    private readonly string _verificationCode;
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    public NewPasswordPage(string email, string verificationCode)
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _email = email;
        _verificationCode = verificationCode;
    }

    private async void OnSavePasswordClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var pass1 = PasswordEntry.Text;
        var pass2 = ConfirmPasswordEntry.Text;

        if (string.IsNullOrEmpty(pass1) || string.IsNullOrEmpty(pass2))
        {
            MostrarError("Por favor llena ambos campos.");
            return;
        }

        // VALIDACIÓN ESTRICTA (Igual que en RegisterPage)
        string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$";
        if (!Regex.IsMatch(pass1, passwordPattern))
        {
            MostrarError("La contraseña debe tener mínimo 8 caracteres, 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial.");
            return;
        }

        if (pass1 != pass2)
        {
            MostrarError("Las contraseñas no coinciden.");
            return;
        }

        SaveBtn.Text = "Guardando...";
        SaveBtn.IsEnabled = false;

        var payload = new { email = _email, code = _verificationCode, new_password = pass1 };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PutAsync($"{BaseUrl}/auth/reset-password", content);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Éxito", "Contraseña actualizada correctamente.", "OK");

                // SOLUCIÓN MAESTRA: Reasignamos el MainPage para limpiar todo el stack de modales
                Application.Current.MainPage = new LoginPage();
            }
            else
            {
                MostrarError("Hubo un problema al actualizar la contraseña.");
            }
        }
        catch
        {
            MostrarError("Error de conexión con el servidor.");
        }

        SaveBtn.Text = "Guardar y Entrar";
        SaveBtn.IsEnabled = true;
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorLabel.IsVisible = true;
    }
}