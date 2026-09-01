using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Text.Json; // Añadido para procesar los datos a la BD
using System.Text;      // Añadido para el formato de envío UTF8

namespace SafetyAppMobile;

public partial class OnboardingPage : ContentPage
{
    private int _currentIndex = 0;
    private string _userEmail = string.Empty;

    /// <summary>
    /// Modelo interno para los pasos del onboarding.
    /// </summary>
    public class OnboardingItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public OnboardingPage()
    {
        InitializeComponent();

        // Inicializar la colección de pasos del onboarding
        var onboardingItems = new ObservableCollection<OnboardingItem>
        {
            new OnboardingItem
            {
                Title = "Tu seguridad a un toque",
                Description = "Safety App te conecta con tu red de confianza al instante.",
                Image = "🛡️"
            },
            new OnboardingItem
            {
                Title = "Rastreo S.O.S Activo",
                Description = "Safety App recopila datos de ubicación para permitir el rastreo continuo durante una emergencia, incluso cuando la app está cerrada o no está en uso.",
                Image = "📍"
            },
            new OnboardingItem
            {
                Title = "Lista para usarse",
                Description = "Otorga los permisos necesarios y configura tus contactos para estar protegida.",
                Image = "✅"
            }
        };

        // Vincular el CarouselView a la colección
        OnboardingCarousel.ItemsSource = onboardingItems;
    }

    /// <summary>
    /// Valida si un correo tiene formato válido usando Regex.
    /// </summary>
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, emailPattern);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Se ejecuta cuando el CarouselView cambia de item.
    /// </summary>
    private void OnCarouselCurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        var carousel = sender as CarouselView;
        if (carousel != null)
        {
            _currentIndex = carousel.ItemsSource.Cast<object>().ToList().IndexOf(e.CurrentItem);
        }
    }

    /// <summary>
    /// Se ejecuta cuando presionan el botón "Continuar" o "Comenzar".
    /// </summary>
    private async void OnNextButtonClicked(object sender, EventArgs e)
    {
        try
        {
            var itemsCount = OnboardingCarousel.ItemsSource.Cast<object>().Count();

            // Verificar si estamos en el último slide
            if (_currentIndex < itemsCount - 1)
            {
                // Avanzar al siguiente slide
                _currentIndex++;
                OnboardingCarousel.ScrollTo(_currentIndex, animate: true);

                // Actualizar el texto del botón en el último slide
                if (_currentIndex == itemsCount - 1)
                {
                    NextButton.Text = "Comenzar";
                }
            }
            else
            {
                // ÚLTIMO SLIDE: Obtener email del modelo actual
                var currentItem = OnboardingCarousel.CurrentItem as OnboardingItem;
                _userEmail = currentItem?.Email?.Trim() ?? string.Empty;

                // Validar email
                if (!IsValidEmail(_userEmail))
                {
                    await DisplayAlert("⚠️ Correo Inválido",
                        "Por favor, ingresa un correo electrónico válido (ej: tu@correo.com)",
                        "OK");
                    return;
                }

                NextButton.Text = "Configurando...";
                NextButton.IsEnabled = false;

                // 1. Guardar el SOS Email en preferencias locales correctamente
                Preferences.Default.Set("SosEmail", _userEmail);

                // 2. ACTUALIZACIÓN A LA BASE DE DATOS PARA QUITAR EL NULL
                try
                {
                    int userId = Preferences.Default.Get("current_user_id", 0);
                    string nombreGuardado = Preferences.Default.Get("UserFullName", "");
                    string telefonoGuardado = Preferences.Default.Get("UserPhone", "");
                    string correoPersonal = Preferences.Default.Get("UserEmail", ""); // Recuperamos el que puso en el Registro

                    // SOLUCIÓN 2: Enviamos ambos correos para cumplir con el modelo de FastAPI
                    var updateData = new
                    {
                        full_name = nombreGuardado,
                        phone_number = telefonoGuardado,
                        email = correoPersonal,     // El personal
                        sos_email = _userEmail      // El de emergencia que acaba de escribir en el Onboarding
                    };

                    var json = JsonSerializer.Serialize(updateData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    using var client = new HttpClient();
                    var response = await client.PutAsync($"https://safety-app-api.onrender.com/users/{userId}/profile", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        await DisplayAlert("Error del Servidor", $"Código: {response.StatusCode}\n\nDetalle: {errorBody}", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error de Red", $"No se pudo conectar a la BD: {ex.Message}", "OK");
                }

                // 3. Marcar onboarding como completado PARA ESTE USUARIO
                int userIdForOnboarding = Preferences.Default.Get("current_user_id", 0);
                string onboardingKey = $"IsFirstLaunch_User_{userIdForOnboarding}";
                Preferences.Default.Set(onboardingKey, false);


                NextButton.Text = "Comenzar";
                NextButton.IsEnabled = true;

                // Navegar a DashboardPage (using absolute route with ///)
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("///dashboard");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[OnboardingPage] Shell.Current is null, cannot navigate to dashboard");
                    await DisplayAlert("Error", "No se pudo navegar al dashboard. Intenta de nuevo.", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OnboardingPage] Error in OnNextButtonClicked: {ex.Message}\n{ex.StackTrace}");
            await DisplayAlert("Error", "Ocurrió un error inesperado.", "OK");

            NextButton.Text = "Comenzar";
            NextButton.IsEnabled = true;
        }
    }
}