using System.Text.Json;
using System.Text.Json.Serialization;
using OpenShock.Desktop.ModuleBase.Config;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Hub.Utils;

namespace OpenShock.Desktop.Config;

/// <summary>
/// Module config implementation
/// </summary>
/// <typeparam name="T"></typeparam>
public class ModuleConfig<T> : IModuleConfig<T> where T : new()
{
    private readonly ILogger _logger;
    private readonly Timer _saveTimer;
    private readonly string _configPath;

    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Module config constructor
    /// </summary>
    /// <param name="configPath"></param>
    /// <param name="logger"></param>
    /// <param name="additionalConverters"></param>
    private ModuleConfig(string configPath, ILogger logger, IEnumerable<JsonConverter> additionalConverters)
    {
        _logger = logger;
        _configPath = configPath;

        _saveTimer = new Timer(_ => { OsTask.Run(Save); });
        
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(), new SemVersionJsonConverter(), new OneOfConverterFactory() }
        };
        
        foreach (var converter in additionalConverters)
        {
            _options.Converters.Add(converter);
        }
    }

    public static async Task<ModuleConfig<T>> Create(string configPath, ILogger logger, params IEnumerable<JsonConverter> additionalConverters)
    {
        var config = new ModuleConfig<T>(configPath, logger, additionalConverters);
        await config.LoadConfig();
        return config;
    }

    private async Task LoadConfig()
    {
        // Load config
        var config = default(T);


        if (File.Exists(_configPath))
        {
            _logger.LogInformation("Config file found, trying to load config from {Path}", _configPath);
            var json = await File.ReadAllTextAsync(_configPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                _logger.LogTrace("Config file is not empty");
                try
                {
                    config = JsonSerializer.Deserialize<T>(json, _options);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Error during deserialization/loading of config");
                    _logger.LogWarning("Attempting to move old config and generate a new one");
                    var configName = Path.GetFileName(_configPath);
                    File.Move(configName, $"{configName}.old");
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
            "No config file found (does not exist or empty or invalid), generating new one at {Path}", _configPath);
        Config = new T();
        await Save();
        _logger.LogInformation("New configuration file generated!");
    }

    public T Config { get; private set; }

    public void SaveDeferred()
    {
        _saveTimer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
    }
    
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    
    public async Task Save()
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogTrace("Saving config");
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(Config, _options)).ConfigureAwait(false);
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
}