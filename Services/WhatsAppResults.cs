using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafetyAppMobile;

// Instantánea de la solicitud inicial, separada del estado del rastreo GPS.
public sealed class WhatsAppContactResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Contacto";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    public string StatusText => Status switch
    {
        "accepted" => "Aceptado por WhatsApp",
        "pending" => "Pendiente en WhatsApp",
        "failed" => "No enviado",
        _ => "Sin confirmación"
    };

    public string StatusColor => Status switch
    {
        "accepted" => "#37246B",
        "failed" => "#B42318",
        _ => "#92400E"
    };
}

public static class WhatsAppResults
{
    private static string Key(int userId) => $"whatsapp_initial_result_{userId}";

    public static void Save(int userId, int alertId, JsonElement response)
    {
        try
        {
            if (response.TryGetProperty("whatsapp", out var value) && value.ValueKind == JsonValueKind.Object)
            {
                var snapshot = JsonSerializer.Serialize(new { alert_id = alertId, whatsapp = value });
                Preferences.Default.Set(Key(userId), snapshot);
            }
            else
            {
                // Compatibilidad con backend anterior: nunca inventar éxito.
                Preferences.Default.Remove(Key(userId));
            }
        }
        catch (Exception)
        {
            // El fallo de almacenamiento no debe impedir activar el rastreo.
            System.Diagnostics.Debug.WriteLine("No se pudo guardar el resultado inicial de WhatsApp.");
        }
    }

    public static List<WhatsAppContactResult>? Load(int userId, int alertId)
    {
        try
        {
            var raw = Preferences.Default.Get(Key(userId), "");
            if (string.IsNullOrEmpty(raw)) return null;
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.GetProperty("alert_id").GetInt32() != alertId) return null;
            var results = doc.RootElement.GetProperty("whatsapp").GetProperty("results");
            var items = JsonSerializer.Deserialize<List<WhatsAppContactResult>>(results.GetRawText());
            return items is not null && items.All(item => item is not null) ? items : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Clear(int userId) => Preferences.Default.Remove(Key(userId));
}
