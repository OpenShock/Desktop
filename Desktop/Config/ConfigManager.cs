using System.Text.Json;
using System.Text.Json.Serialization;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Hub.Utils;

namespace OpenShock.Desktop.Config;

public sealed class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(Constants.AppdataFolder, "config.json");

    private readonly ILogger<ConfigManager> _logger;
    public OpenShockConfig Config { get; }

    private readonly Timer _saveTimer;

    public ConfigManager(ILogger<ConfigManager> logger)
    {
        _logger = logger;

        _saveTimer = new Timer(_ => { OsTask.Run(SaveInternally); });

        // Load config
        OpenShockConfig? config = null;


        if (File.Exists(ConfigPath))
        {
            _logger.LogInformation("Config file found, trying to load config from {Path}", ConfigPath);
            var json = File.ReadAllText(ConfigPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _logger.LogTrace("Config file is not empty");
                try
                {
                    config = JsonSerializer.Deserialize<OpenShockConfig>(json, Options);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Error during deserialization/loading of config");
                    _logger.LogWarning("Attempting to move old config and generate a new one");
                    File.Move(ConfigPath, ConfigPath + ".old");
                }
            }
        }

        if (config != null)
        {
            Config = config;
            _logger.LogInformation("Successfully loaded config");
            return;
        }

        _logger.LogInformation(
            "No config file found (does not exist or empty or invalid), generating new one at {Path}", ConfigPath);
        Config = new OpenShockConfig();
        SaveInternally().Wait();
        _logger.LogInformation("New configuration file generated!");
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new SemVersionJsonConverter(), new OneOfConverterFactory() }
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private async Task SaveInternally()
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