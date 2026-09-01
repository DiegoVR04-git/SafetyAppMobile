using System.Text;
using System.Text.Json;

namespace SafetyAppMobile;

public partial class VerifyCodePage : ContentPage
{
    private readonly string _email;
    private readonly string _actionType;
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    public VerifyCodePage(string email, string actionType)
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _email = email;
        _actionType = actionType;

        SubtitleLabel.Text = $"Ingresa los 6 dígitos que enviamos a {_email}.";
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnVerifyClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var code = CodeEntry.Text?.Trim();

        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            MostrarError("El código debe tener 6 números.");
            return;
        }

        VerifyBtn.Text = "Verificando...";
        VerifyBtn.IsEnabled = false;

        var payload = new { email = _email, code = code, type = _actionType };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/verify-code", content);

            if (response.IsSuccessStatusCode)
            {
                if (_actionType == "register")
                {
                    await DisplayAlert("Éxito", "Cuenta verificada correctamente.", "OK");
                    await Navigation.PopModalAsync();
                }
                else if (_actionType == "reset_password")
                {
                    // Si el código es correcto, abrimos la pantalla para nueva contraseña
                    await Navigation.PushModalAsync(new NewPasswordPage(_email, code));
                    Navigation.RemovePage(this);
                }
            }
            else
            {
                MostrarError("Código inválido o expirado.");
            }
        }
        catch
        {
            MostrarError("Error de conexión al servidor.");
        }

        VerifyBtn.Text = "Verificar Código";
        VerifyBtn.IsEnabled = true;
    }

    private async void OnResendCodeTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert("Reenviado", "El código ha sido enviado de nuevo a tu correo.", "OK");
        // Aquí podrías añadir la misma llamada a _httpClient para reenviar el código
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorLabel.IsVisible = true;
    }
}