using System.Text.Json;
using Microsoft.Maui.Storage;

namespace SafetyAppMobile;

public partial class ContactPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;
    private int _currentUserId;

    // 1. NUEVO: Creamos una "Caja" principal que coincida con el diccionario de Python
    public class ContactListWrapper
    {
        public List<ContactResponse> contacts { get; set; }
    }

    // 2. El molde de tu contacto individual (ya lo tenías)
    public class ContactResponse
    {
        public int contact_id { get; set; }
        public string name { get; set; }
        public string phone_number { get; set; }
    }

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

                // 3. NUEVO: Desempaquetamos la caja grande primero
                var wrapper = JsonSerializer.Deserialize<ContactListWrapper>(json);

                // 4. NUEVO: Le entregamos a la pantalla solo la lista que venía adentro
                if (wrapper != null && wrapper.contacts != null)
                {
                    ContactsList.ItemsSource = wrapper.contacts;
                }
            }
        }
        catch (Exception ex)
        {
            // Cambié el mensaje para que, si vuelve a fallar, nos diga el error real
            await DisplayAlert("Error Técnico", ex.Message, "OK");
        }
    }


    // NUEVA FUNCIÓN: Responde al toque directo de la tarjeta
    private async void OnContactCardTapped(object sender, TappedEventArgs e)
    {
        // e.Parameter contiene exactamente el contacto que el usuario tocó
        if (e.Parameter is ContactResponse tappedContact)
        {
            // Abrimos la página de edición con los datos precisos
            var editPage = new EditContactPage(tappedContact.contact_id, tappedContact.name, tappedContact.phone_number);
            await Navigation.PushModalAsync(editPage);
        }
    }

    private async void OnAddContactClicked(object sender, EventArgs e)
    {
        // Abre la pantalla de agregar un nuevo contacto
        var addPage = new AddContactPage();

        // MAUI a veces no recarga OnAppearing al cerrar modales en Android, así que lo forzamos:
        addPage.Disappearing += async (s, args) => await LoadContacts();

        await Navigation.PushModalAsync(addPage);
    }
}