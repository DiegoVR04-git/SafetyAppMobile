using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions; 

namespace SafetyAppMobile;

public partial class RegisterPage : ContentPage
{
    private const string BaseUrl = "https://safety-app-api.onrender.com";
    private readonly HttpClient _httpClient;

    public RegisterPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        StatusLabel.TextColor = Colors.Red;

        var name = NameEntry.Text;
        var rawPhone = PhoneEntry.Text?.Trim(); 
        var password = PasswordEntry.Text;
        var confirmPassword = ConfirmPasswordEntry.Text;

        // 1. Extraer el código de país seleccionado en el Picker (Por defecto +52 si falla)
        string selectedCode = CountryCodePicker.SelectedItem?.ToString() ?? "+52";

        // 2. Validar que no haya campos vacíos
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rawPhone) ||
            string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            StatusLabel.Text = "Por favor, llena todos los campos.";
            return;
        }

        // 3. Validar estrictamente que el número tenga 10 dígitos
        if (rawPhone.Length < 10)
        {
            StatusLabel.Text = "El número debe tener 10 dígitos exactos.";
            return;
        }

        // 4. NUEVO: Validación de contraseña segura usando Regex
        // Reglas: Min 8 chars, 1 mayúscula, 1 minúscula, 1 número, 1 carácter especial
        string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$";
        if (!Regex.IsMatch(password, passwordPattern))
        {
            StatusLabel.Text = "La contraseña debe tener mínimo 8 caracteres, 1 mayúscula, 1 minúscula, 1 número y 1 carácter especial.";
            return;
        }

        // 5. Validar que las contraseñas coincidan perfectamente
        if (password != confirmPassword)
        {
            StatusLabel.Text = "Las contraseñas no coinciden.";
            return;
        }

        RegisterBtn.Text = "Creando cuenta...";
        RegisterBtn.IsEnabled = false;

        // 6. LA FUSIÓN: Unimos el +52 (o +1) con los 10 dígitos (Ej: +525512345678)
        string fullPhoneNumber = selectedCode + rawPhone;

        // 7. ACTUALIZADO: Enviamos el 'fullPhoneNumber' a tu servidor de Python
        var registerData = new { full_name = name, phone_number = fullPhoneNumber, password = password };
        var json = JsonSerializer.Serialize(registerData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/register", content);

            if (response.IsSuccessStatusCode)
            {
                StatusLabel.TextColor = Colors.Green;
                StatusLabel.Text = "¡Cuenta creada con éxito!";

                Preferences.Default.Set("UserFullName", name.Trim());

                await Task.Delay(1500);
                Application.Current.MainPage = new LoginPage();
            }
            else
            {
                StatusLabel.Text = "Error: Es posible que el teléfono ya exista.";
            }
        }
        catch (Exception)
        {
            StatusLabel.Text = "No hay conexión con el servidor.";
        }
        finally
        {
            RegisterBtn.Text = "Unirse a la Red Segura";
            RegisterBtn.IsEnabled = true;
        }
    }

    private void OnBackToLoginTapped(object sender, TappedEventArgs e)
    {
        Application.Current.MainPage = new LoginPage();
    }
}