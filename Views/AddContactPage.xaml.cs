using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace SafetyAppMobile;

public partial class AddContactPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    public AddContactPage()
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
    // LÓGICA DE GUARDAR CONTACTO
    // ==========================================
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Limpiamos errores previos
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        var name = NameEntry.Text;
        var rawPhone = PhoneEntry.Text?.Trim();

        // 1. Extraer el código de país desde el nuevo Label
        string selectedCode = SelectedCountryCodeLabel.Text ?? "+52";

        // 2. Validar que no haya campos vacíos
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPhone))
        {
            StatusLabel.Text = "Por favor, llena ambos campos.";
            return;
        }

        // 3. Validar estrictamente los 10 dígitos
        if (rawPhone.Length < 10)
        {
            StatusLabel.Text = "El número debe tener 10 dígitos exactos.";
            return;
        }

        SaveBtn.Text = "Guardando...";
        SaveBtn.IsEnabled = false;

        // 4. LA FUSIÓN: Armamos el número internacional
        string fullPhoneNumber = selectedCode + rawPhone;

        var currentUserId = Preferences.Default.Get("current_user_id", 0);

        // 5. Enviamos la variable fusionada (fullPhoneNumber) a la API
        var newContactData = new { user_id = currentUserId, name = name, phone_number = fullPhoneNumber };
        var json = JsonSerializer.Serialize(newContactData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/contacts", content);

            if (response.IsSuccessStatusCode)
            {
                // Éxito
                await Navigation.PopModalAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // 🚀 LEEMOS EL ERROR EXACTO QUE MANDA PYTHON 🚀
                var errorResponse = await response.Content.ReadAsStringAsync();

                // Extraemos el texto del JSON que nos manda FastAPI
                using (var doc = JsonDocument.Parse(errorResponse))
                {
                    string errorMessage = doc.RootElement.GetProperty("detail").GetString();
                    StatusLabel.Text = errorMessage;
                }

                SaveBtn.Text = "Guardar Protector";
                SaveBtn.IsEnabled = true;
            }
            else
            {
                StatusLabel.Text = "Error al guardar el contacto.";
                SaveBtn.Text = "Guardar Protector";
                SaveBtn.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            StatusLabel.Text = "Sin conexión con el servidor.";
            SaveBtn.Text = "Guardar Protector";
            SaveBtn.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}