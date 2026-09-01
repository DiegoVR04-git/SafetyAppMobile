using System.Text;
using System.Text.Json;

namespace SafetyAppMobile;

public partial class ForgotPasswordPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    public ForgotPasswordPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private async void OnSendCodeClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var email = EmailEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            MostrarError("Ingresa un correo válido.");
            return;
        }

        SendCodeBtn.Text = "Enviando...";
        SendCodeBtn.IsEnabled = false;

        // Llamada a tu API para generar y enviar el código (usando Resend en el backend)
        var payload = new { email = email, type = "reset_password" };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/send-code", content);

            if (response.IsSuccessStatusCode)
            {
                // Si el correo se envía, abrimos la pantalla para que ingrese el código
                await Navigation.PushModalAsync(new VerifyCodePage(email, "reset_password"));
                // Removemos esta página del stack para que no regrese aquí por error
                Navigation.RemovePage(this);
            }
            else
            {
                MostrarError("No encontramos una cuenta con ese correo.");
            }
        }
        catch
        {
            MostrarError("Sin conexión al servidor.");
        }

        SendCodeBtn.Text = "Enviar Código";
        SendCodeBtn.IsEnabled = true;
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorLabel.IsVisible = true;
    }
}