using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SafetyAppMobile;

/// <summary>
/// Modelo para almacenar ubicaciones pendientes de envío al servidor.
/// Se utiliza cuando el dispositivo no tiene conexión a internet.
/// </summary>
[Table("PendingLocations")]
public class PendingLocation
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int AlertId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>
    /// Marca de tiempo en UTC cuando se capturó la ubicación.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Gestor estático de la base de datos offline para ubicaciones pendientes.
/// Maneja la persistencia local de coordenadas cuando no hay conexión a internet.
/// </summary>
public static class OfflineDatabase
{
    private static SQLiteAsyncConnection _connection;
    private static readonly string DbPath = Path.Combine(FileSystem.AppDataDirectory, "safetyapp.db3");

    /// <summary>
    /// Inicializa la conexión a la base de datos y crea la tabla de ubicaciones pendientes.
    /// Debe llamarse una sola vez al arrancar la aplicación.
    /// </summary>
    public static async Task Init()
    {
        if (_connection != null)
            return;

        _connection = new SQLiteAsyncConnection(DbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        await _connection.CreateTableAsync<PendingLocation>();
    }

    /// <summary>
    /// Guarda una ubicación pendiente en la base de datos local.
    /// Se utiliza cuando el dispositivo no tiene conexión a internet.
    /// </summary>
    public static async Task SaveLocationAsync(int alertId, double latitude, double longitude)
    {
        try
        {
            var location = new PendingLocation
            {
                AlertId = alertId,
                Latitude = latitude,
                Longitude = longitude,
                Timestamp = DateTime.UtcNow
            };

            await _connection.InsertAsync(location);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving offline location: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene todas las ubicaciones pendientes de envío al servidor.
    /// </summary>
    public static async Task<List<PendingLocation>> GetPendingLocationsAsync()
    {
        try
        {
            return await _connection.Table<PendingLocation>().ToListAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error retrieving pending locations: {ex.Message}");
            return new List<PendingLocation>();
        }
    }

    /// <summary>
    /// Elimina una ubicación pendiente de la base de datos local después de ser procesada.
    /// </summary>
    public static async Task DeleteLocationAsync(int id)
    {
        try
        {
            await _connection.DeleteAsync<PendingLocation>(id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting pending location: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene el conteo total de ubicaciones pendientes.
    /// Útil para monitoreo de la cola.
    /// </summary>
    public static async Task<int> GetPendingCountAsync()
    {
        try
        {
            return await _connection.Table<PendingLocation>().CountAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error counting pending locations: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Elimina múltiples ubicaciones pendientes de la base de datos local en un lote.
    /// Se utiliza después de procesar exitosamente un batch POST.
    /// </summary>
    public static async Task DeleteAllLocationsAsync(List<PendingLocation> locations)
    {
        try
        {
            if (locations == null || locations.Count == 0)
                return;

            // Extraer los IDs de las ubicaciones a borrar
            var ids = locations.Select(l => l.Id).ToList();

            // Borrar en lote usando DeleteAsync con filtro
            foreach (var id in ids)
            {
                await _connection.DeleteAsync<PendingLocation>(id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting batch of pending locations: {ex.Message}");
        }
    }
}
