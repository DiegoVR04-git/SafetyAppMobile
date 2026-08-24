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

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        // Limpiamos errores previos
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        var name = NameEntry.Text;
        var rawPhone = PhoneEntry.Text?.Trim();

        // 1. Extraer el código de país del Picker (Por defecto +52)
        string selectedCode = CountryCodePicker.SelectedItem?.ToString() ?? "+52";

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

                // Extraemos el texto del JSON que nos manda FastAPI (Ej: {"detail":"Este número ya..."})
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