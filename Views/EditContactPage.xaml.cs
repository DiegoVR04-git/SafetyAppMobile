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

        // 1. Precargamos el nombre y extraemos la inicial para el avatar
        NameEntry.Text = currentName;
        if (!string.IsNullOrEmpty(currentName))
        {
            AvatarInitials.Text = currentName.Substring(0, 1).ToUpper();
        }

        // 2. Ingeniería inversa del teléfono (separar lada y número hacia el Label estilizado)
        if (!string.IsNullOrEmpty(currentPhone))
        {
            if (currentPhone.StartsWith("+52"))
            {
                SelectedCountryCodeLabel.Text = "+52";
                PhoneEntry.Text = currentPhone.Substring(3);
            }
            else if (currentPhone.StartsWith("+1"))
            {
                SelectedCountryCodeLabel.Text = "+1";
                PhoneEntry.Text = currentPhone.Substring(2);
            }
            else
            {
                PhoneEntry.Text = currentPhone;
            }
        }
    }

    // Actualizar la inicial del avatar dinámicamente si el usuario escribe otro nombre
    private void OnNameTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            AvatarInitials.Text = e.NewTextValue.Substring(0, 1).ToUpper();
        }
        else
        {
            AvatarInitials.Text = "?";
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
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
    // GUARDAR ACTUALIZACIÓN
    // ==========================================
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        var rawPhone = PhoneEntry.Text?.Trim();
        string selectedCode = SelectedCountryCodeLabel.Text ?? "+52";

        // Validaciones
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPhone))
        {
            await DisplayAlert("⚠️ Aviso", "No puedes dejar campos vacíos.", "OK");
            return;
        }

        if (rawPhone.Length < 10)
        {
            await DisplayAlert("⚠️ Aviso", "El número de teléfono debe tener 10 dígitos.", "OK");
            return;
        }

        // Armar el número completo
        string fullPhoneNumber = selectedCode + rawPhone;

        var updateData = new { name = name, phone_number = fullPhoneNumber };
        var json = JsonSerializer.Serialize(updateData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            SaveBtn.Opacity = 0.6;
            SaveBtn.IsEnabled = false;

            var response = await _httpClient.PutAsync($"{BaseUrl}/contacts/{_contactId}", content);

            if (response.IsSuccessStatusCode)
            {
                await Navigation.PopModalAsync();
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                await DisplayAlert("❌ Error", $"No se pudo actualizar: {errorText}", "OK");
                SaveBtn.Opacity = 1.0;
                SaveBtn.IsEnabled = true;
            }
        }
        catch (Exception)
        {
            await DisplayAlert("❌ Error de red", "Sin conexión con el servidor.", "OK");
            SaveBtn.Opacity = 1.0;
            SaveBtn.IsEnabled = true;
        }
    }

    // ==========================================
    // MÉTODOS DEL OVERLAY DE ELIMINACIÓN
    // ==========================================

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        CustomDeleteOverlay.IsVisible = true;
        CustomDeleteOverlay.Opacity = 0;
        await CustomDeleteOverlay.FadeTo(1, 250);
    }

    private async void OnCancelDeleteAlertClicked(object sender, EventArgs e)
    {
        await CustomDeleteOverlay.FadeTo(0, 200);
        CustomDeleteOverlay.IsVisible = false;
    }

    private async void OnConfirmDeleteClicked(object sender, EventArgs e)
    {
        await CustomDeleteOverlay.FadeTo(0, 200);
        CustomDeleteOverlay.IsVisible = false;

        try
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}/contacts/{_contactId}");

            if (response.IsSuccessStatusCode)
            {
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("❌ Error", "No se pudo eliminar el contacto. Intenta de nuevo.", "OK");
            }
        }
        catch (Exception)
        {
            await DisplayAlert("❌ Error de red", "Sin conexión con el servidor.", "OK");
        }
    }
}