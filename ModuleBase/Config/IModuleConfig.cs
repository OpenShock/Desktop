using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenShock.Desktop.ModuleBase.Config;

public abstract class ModuleConfig<T>
{
    public T Config { get; }

    public void SaveDeferred()
    {
        
    }
    
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    
    public async Task SaveNow()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogTrace("Saving config");
            var directory = System.IO.Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(ConfigPath, JsonSerializer.Serialize(Config, Options)).ConfigureAwait(false);
            _logger.LogInformation("Config saved");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while saving new config file");
        }
        finally
        {
            _saveLock.Release();
        }
    }
    
    public void Save()
    {
        lock (_saveTimer)
        {
            _saveTimer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);            
        }
    }
}