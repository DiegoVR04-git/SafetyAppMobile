using Microsoft.Maui.Storage;
using System.Text.Json;

namespace SafetyAppMobile;

public class ContactListWrapper
{
    public List<ContactResponse> contacts { get; set; }
}

public class ContactResponse
{
    public int contact_id { get; set; }
    public string name { get; set; }
    public string phone_number { get; set; }

    // Propiedad calculada para mostrar únicamente la inicial en mayúscula
    public string Initial => !string.IsNullOrEmpty(name) ? name.Substring(0, 1).ToUpper() : "?";
}

public partial class ContactPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;
    private int _currentUserId;

    public ContactPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadContacts();
    }

    private async Task LoadContacts()
    {
        _currentUserId = Preferences.Default.Get("current_user_id", 0);
        if (_currentUserId == 0) return;

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/contacts/{_currentUserId}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var wrapper = JsonSerializer.Deserialize<ContactListWrapper>(json);

                if (wrapper != null && wrapper.contacts != null)
                {
                    var lista = wrapper.contacts;
                    ContactsList.ItemsSource = lista;

                    int total = lista.Count;
                    ContactCountLabel.Text = $"{total} / 5 contactos";

                    InfoBox.IsVisible = total > 0;

                    StatusDot.Fill = total > 0
                        ? new SolidColorBrush(Color.FromArgb("#22c55e"))
                        : new SolidColorBrush(Color.FromArgb("#D8D3E2"));

                    ActualizarPuntitos(total);

                    if (total >= 5)
                    {
                        AddButton.IsEnabled = false;
                        AddButton.Opacity = 0.5;
                    }
                    else
                    {
                        AddButton.IsEnabled = true;
                        AddButton.Opacity = 1.0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error Técnico", ex.Message, "OK");
        }
    }

    private void ActualizarPuntitos(int count)
    {
        Color activo = Color.FromArgb("#4B2E83");
        Color inactivo = Color.FromArgb("#D8D3E2");

        Dot1.Fill = count >= 1 ? activo : inactivo;
        Dot2.Fill = count >= 2 ? activo : inactivo;
        Dot3.Fill = count >= 3 ? activo : inactivo;
        Dot4.Fill = count >= 4 ? activo : inactivo;
        Dot5.Fill = count >= 5 ? activo : inactivo;
    }

    private async void OnEditContactTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is ContactResponse contact)
        {
            var editPage = new EditContactPage(contact.contact_id, contact.name, contact.phone_number);
            editPage.Disappearing += async (s, args) => await LoadContacts();
            await Navigation.PushModalAsync(editPage);
        }
    }

    private async void OnAddContactClicked(object sender, EventArgs e)
    {
        var addPage = new AddContactPage();
        addPage.Disappearing += async (s, args) => await LoadContacts();
        await Navigation.PushModalAsync(addPage);
    }
}