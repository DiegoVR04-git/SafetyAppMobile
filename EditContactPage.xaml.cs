using System.Text;
using System.Text.Json;

namespace SafetyAppMobile;

public partial class EditContactPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;
    private int _contactId;

    public EditContactPage(int contactId, string currentName, string currentPhone)
    {
        InitializeComponent();
        _httpClient = new HttpClient();
        _contactId = contactId;

        // Precargamos el nombre
        NameEntry.Text = currentName;

        // 1. LA INGENIERÍA INVERSA: Cortamos el prefijo para mostrarlo correctamente
        if (!string.IsNullOrEmpty(currentPhone))
        {
            if (currentPhone.StartsWith("+52"))
            {
                CountryCodePicker.SelectedItem = "+52";
                PhoneEntry.Text = currentPhone.Substring(3); // Corta los primeros 3 caracteres (+52)
            }
            else if (currentPhone.StartsWith("+1"))
            {
                CountryCodePicker.SelectedItem = "+1";
                PhoneEntry.Text = currentPhone.Substring(2); // Corta los primeros 2 caracteres (+1)
            }
            else
            {
                // Si por alguna razón es un número viejo sin código, lo mostramos tal cual
                PhoneEntry.Text = currentPhone;
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        var name = NameEntry.Text;
        var rawPhone = PhoneEntry.Text?.Trim();

        // 2. Extraer el código seleccionado
        string selectedCode = CountryCodePicker.SelectedItem?.ToString() ?? "+52";

        // 3. Validaciones
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPhone))
        {
            StatusLabel.Text = "No puedes dejar campos vacíos.";
            return;
        }

        if (rawPhone.Length < 10)
        {
            StatusLabel.Text = "El número debe tener 10 dígitos exactos.";
            return;
        }

        SaveBtn.Text = "Guardando...";
        SaveBtn.IsEnabled = false;

        // 4. LA FUSIÓN: Volvemos a armar el número para mandarlo a la nube
        string fullPhoneNumber = selectedCode + rawPhone;

        var updateData = new { name = name, phone_number = fullPhoneNumber };
        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PutAsync($"{BaseUrl}/contacts/{_contactId}", content);

            if (response.IsSuccessStatusCode)
            {
                await Navigation.PopModalAsync();
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                StatusLabel.Text = $"Error: {response.StatusCode} - {errorText}";
                SaveBtn.Text = "Guardar Cambios";
                SaveBtn.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            StatusLabel.Text = "Sin conexión con el servidor.";
            SaveBtn.Text = "Guardar Cambios";
            SaveBtn.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    // ==========================================
    // METODOS DEL OVERLAY DE ELIMINACIÓN
    // ==========================================

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        // En lugar del cuadro blanco genérico, mostramos nuestro diseño
        CustomDeleteOverlay.IsVisible = true;
        CustomDeleteOverlay.Opacity = 0;
        await CustomDeleteOverlay.FadeTo(1, 250);
    }

    private async void OnCancelDeleteAlertClicked(object sender, EventArgs e)
    {
        // Ocultar alerta si se arrepiente
        await CustomDeleteOverlay.FadeTo(0, 200);
        CustomDeleteOverlay.IsVisible = false;
    }

    private async void OnConfirmDeleteClicked(object sender, EventArgs e)
    {
        // 1. Ocultamos el overlay
        await CustomDeleteOverlay.FadeTo(0, 200);
        CustomDeleteOverlay.IsVisible = false;

        // 2. Ejecutamos la lógica de borrado original
        DeleteBtn.Text = "Eliminando...";
        DeleteBtn.IsEnabled = false;

        try
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}/contacts/{_contactId}");

            if (response.IsSuccessStatusCode)
            {
                await Navigation.PopModalAsync();
            }
            else
            {
                StatusLabel.Text = "Error al eliminar. Intenta de nuevo.";
                DeleteBtn.Text = "🗑️ Eliminar Contacto";
                DeleteBtn.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            StatusLabel.Text = "Sin conexión con el servidor.";
            DeleteBtn.Text = "🗑️ Eliminar Contacto";
            DeleteBtn.IsEnabled = true;
        }
    }
}