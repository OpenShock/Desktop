using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Net.Mime;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.FileProviders;
using OneOf;
using OneOf.Types;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager.Implementation;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Services;
using Semver;

namespace OpenShock.Desktop.ModuleManager;

public sealed class ModuleManager
{
    private static readonly Type ModuleBaseType = typeof(DesktopModuleBase);
    private static readonly Type AspNetIComponentType = typeof(IComponent);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ConfigManager _configManager;

    public event Action? ModulesLoaded;

    private static string ModuleDirectory => Path.Combine(Constants.AppdataFolder, "modules");

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public ModuleManager(IServiceProvider serviceProvider, ILogger<ModuleManager> logger,
        RepositoryManager repositoryManager, ConfigManager configManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _repositoryManager = repositoryManager;
        _configManager = configManager;
    }

    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();

    #region Tasks

    public async Task ProcessTaskList()
    {
        foreach (var moduleTask in _configManager.Config.Modules.ModuleTasks)
        {
            try
            {
                await ProcessTask(moduleTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process task for module {ModuleId}", moduleTask.Key);
            }
        }

        _configManager.Config.Modules.ModuleTasks.Clear();
        _configManager.Save();
    }

    private async Task ProcessTask(KeyValuePair<string, ModuleTask> moduleTask)
    {
        await moduleTask.Value.Match(async install =>
            {
                _logger.LogInformation("Installing module {ModuleId} version {Version}", moduleTask.Key,
                    install.Version);
                await DownloadModule(moduleTask.Key, install.Version);
            },
            remove =>
            {
                _logger.LogInformation("Removing module {ModuleId}", moduleTask.Key);
                RemoveModule(moduleTask.Key);
                return Task.FromResult(Task.CompletedTask);
            });
    }

    #endregion

    private void RemoveModule(string moduleId)
    {
        var moduleFolderPath = Path.Combine(ModuleDirectory, moduleId);
        if (Directory.Exists(moduleFolderPath)) Directory.Delete(moduleFolderPath, true);
    }

    private async Task<OneOf<Success, Error, NotFound>> DownloadModule(string moduleId, SemVersion version)
    {
        var module = _repositoryManager.Repositories
            .Where(x => x.Value.Repository != null)
            .SelectMany(x => x.Value.Repository!.Modules)
            .Where(x => x.Key == moduleId).Select(x => x.Value).FirstOrDefault();

        if (module is null) return new NotFound();

        if (!module.Versions.TryGetValue(version, out var moduleVersion))
        {
            return new NotFound();
        }

        var moduleDownload = await DownloadModule(moduleId, moduleVersion.Url);
        if (moduleDownload.IsT0)
        {
            return new Success();
        }

        return new Error();
    }

    private async Task<OneOf<Success, Error>> DownloadModule(string moduleId, Uri moduleUrl)
    {
        using var downloadResponse = await HttpClient.GetAsync(moduleUrl, HttpCompletionOption.ResponseHeadersRead);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download module {ModuleId} from {ModuleUrl} with status code {StatusCode}",
                moduleId, moduleUrl, downloadResponse.StatusCode);
            return new Error();
        }

        if (downloadResponse.Content.Headers.ContentType?.MediaType != MediaTypeNames.Application.Zip)
        {
            _logger.LogError(
                "Failed to download module {ModuleId} from {ModuleUrl} as it is not a ZIP file (Content-Type: {ContentType})",
                moduleId, moduleUrl, downloadResponse.Content.Headers.ContentType?.MediaType);
            return new Error();
        }

        var moduleFolderPath = Path.Combine(ModuleDirectory, moduleId);

        if (Directory.Exists(moduleFolderPath)) Directory.Delete(moduleFolderPath, true);

        Directory.CreateDirectory(moduleFolderPath);
        ZipFile.ExtractToDirectory(await downloadResponse.Content.ReadAsStreamAsync(), moduleFolderPath, true);

        return new Success();
    }

    private void LoadModule(string moduleFolderPath)
    {
        _logger.LogTrace("Searching for module in {Path}", moduleFolderPath);
        var moduleFile = Directory.GetFiles(moduleFolderPath, "*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (moduleFile == null)
        {
            _logger.LogWarning("No DLLs found in root module folder!");
            return;
        }

        _logger.LogDebug("Attempting to load module: {Path}", moduleFile);

        var assemblyLoadContext = new ModuleAssemblyLoadContext(moduleFolderPath);
        var assembly = assemblyLoadContext.LoadFromAssemblyPath(moduleFile);

        var moduleTypes = assembly.GetTypes();
        var pluginTypes = moduleTypes.Where(t => t.IsAssignableTo(ModuleBaseType)).ToImmutableArray();
        switch (pluginTypes.Length)
        {
            case 0:
                _logger.LogWarning("No modules found in DLL!");
                return;
            case > 1:
                _logger.LogWarning("Expected 1 module, found {NumberOfModules}", pluginTypes.Length);
                return;
        }

        var module = (DesktopModuleBase?)ActivatorUtilities.CreateInstance(_serviceProvider, pluginTypes[0]);
        if (module is null) throw new Exception("Failed to instantiate module!");
        
        var loadedModule = new LoadedModule
        {
            LoadContext = assemblyLoadContext,
            Assembly = assembly,
            Module = module
        };
        
        module.SetContext(new ModuleInstanceManager(loadedModule, 
            _serviceProvider.GetRequiredService<ILoggerFactory>(), 
            _serviceProvider.GetRequiredService<BackendHubManager>(), 
            _serviceProvider.GetRequiredService<LiveControlManager>(),
                            _serviceProvider.GetRequiredService<OpenShockApi>()
            )
        {
            AppServiceProvider = _serviceProvider
        });

        var moduleFolder =
            Path.GetFileName(moduleFolderPath); // now this seems odd, but this gives me the modules folder name

        if (moduleFolder != loadedModule.Module.Id)
        {
            _logger.LogWarning(
                "Module folder name does not match module ID! [{FolderName} != {ModuleName}]. This might cause issues and is not expected. Updating for sure wont work properly :3 Please fix this",
                moduleFolder, loadedModule.Module.Id);
            return;
        }

        if (!Modules.TryAdd(module.Id, loadedModule)) throw new Exception("Module already loaded!");
    }

    internal void LoadAll()
    {
        if (!Directory.Exists(ModuleDirectory))
        {
            Directory.CreateDirectory(ModuleDirectory);
            return; // We don't have any modules to load, we just created the directory
        }

        foreach (var moduleFolders in Directory.GetDirectories(ModuleDirectory))
        {
            try
            {
                LoadModule(moduleFolders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin");
            }
        }

        ModulesLoaded?.Invoke();
    }
}