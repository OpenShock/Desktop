using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Net.Mime;
using System.Reactive.Subjects;
using System.Reflection;
using dnlib.DotNet;
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

public sealed class ModuleManager : IDisposable
{
    private static readonly Type ModuleBaseType = typeof(DesktopModuleBase);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ConfigManager _configManager;
    
    public IObservable<byte> ModulesLoaded => _modulesLoaded;
    private readonly Subject<byte> _modulesLoaded = new();

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

        _repositoryUpdatedSubscription = _repositoryManager.RepositoriesUpdated.Subscribe(b =>
        {
            RefreshRepositoryInformationOnLoaded();
        });
    }

    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();
    private readonly IDisposable _repositoryUpdatedSubscription;

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

    private void LoadModule(string moduleFolderPath, string moduleDll, string moduleFolderName)
    {
        _logger.LogDebug("Attempting to load module: {Path}", moduleDll);

        var assemblyLoadContext = new ModuleAssemblyLoadContext(moduleFolderPath);
        var assembly = assemblyLoadContext.LoadFromAssemblyPath(moduleDll);
        
        var moduleTypes = assembly.GetTypes();
        var pluginTypes = moduleTypes.Where(t => t.IsAssignableTo(ModuleBaseType)).ToImmutableArray();
        switch (pluginTypes.Length)
        {
            case 0:
                _logger.LogError("No modules found in DLL!");
                return;
            case > 1:
                _logger.LogError("Expected 1 module, found {NumberOfModules}", pluginTypes.Length);
                return;
        }

        var assemblyVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(assemblyVersion))
        {
            _logger.LogError("The module assembly does not have a version attribute! Please make sure you have added a <Version></Version> tag to your .csproj file");
            return;
        }

        if (!SemVersion.TryParse(assemblyVersion, SemVersionStyles.Strict, out var loadedModuleVersion))
        {
            _logger.LogError("Failed to parse sem-version {Version} for module {ModuleId}. It needs to be a valid semantic version.", assemblyVersion, pluginTypes[0].FullName);
            return;
        }
        
        var module = (DesktopModuleBase?)ActivatorUtilities.CreateInstance(_serviceProvider, pluginTypes[0]);
        if (module is null) throw new Exception("Failed to instantiate module!");
        
        
        var loadedModule = new LoadedModule
        {
            LoadContext = assemblyLoadContext,
            Assembly = assembly,
            Module = module,
            Version = loadedModuleVersion,
            AvailableVersion = null,
            RepositoryModule = null
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
        
        if (moduleFolderName != loadedModule.Module.Id)
        {
            _logger.LogError(
                "Module folder name does not match module ID! [{FolderName} != {ModuleName}]. This might cause issues and is not expected. Updating for sure wont work properly :3 Please fix this",
                moduleFolderName, loadedModule.Module.Id);
            return;
        }

        if (!Modules.TryAdd(module.Id, loadedModule)) throw new Exception("Module already loaded!");
    }

    private void RefreshRepositoryInformationOnLoaded()
    {
        var repoModules = _repositoryManager.Repositories
            .Where(x => x.Value.Repository != null)
            .SelectMany(x => x.Value.Repository!.Modules)
            .ToImmutableDictionary();

        foreach (var loadedModule in Modules)
        {
            if (repoModules.TryGetValue(loadedModule.Key, out var repoModule))
            {
                loadedModule.Value.RepositoryModule = repoModule;
                
                var mostRecentRelease = repoModule.Versions.Keys.Where(x => x.IsRelease).OrderByDescending(x => x, SemVersion.PrecedenceComparer).FirstOrDefault();
                if (mostRecentRelease != null && loadedModule.Value.Version.ComparePrecedenceTo(mostRecentRelease) < 0)
                {
                    loadedModule.Value.AvailableVersion = mostRecentRelease;
                }
            } else {
                loadedModule.Value.RepositoryModule = null;
                loadedModule.Value.AvailableVersion = null;
            }
        }
    }

    internal void LoadAll()
    {
        foreach (var modules in GetModules())
        {
            try
            {
                LoadModule(modules.Path, modules.ModuleDll, modules.FolderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin");
            }
        }

        RefreshRepositoryInformationOnLoaded();
        _modulesLoaded.OnNext(0);
    }

    private ImmutableArray<AvailableModule> GetModules()
    {
        if (!Directory.Exists(ModuleDirectory))
        {
            Directory.CreateDirectory(ModuleDirectory);
            return ImmutableArray<AvailableModule>.Empty;
        }

        var modules = new List<AvailableModule>();

        foreach (var moduleFolder in Directory.GetDirectories(ModuleDirectory))
        {
            try
            {
                _logger.LogTrace("Searching for module in {Path}", moduleFolder);
                var moduleFile = Directory.GetFiles(moduleFolder, "*.dll", SearchOption.TopDirectoryOnly)
                    .ToImmutableArray();
                if (moduleFile.Length <= 0)
                {
                    _logger.LogError("No DLLs found in root module folder!");
                    continue;
                }
                
                if(moduleFile.Length > 1)
                {
                    _logger.LogError("Expected 1 dll, found {NumberOfModules}", moduleFile.Length);
                    continue;
                }
                
                var moduleFolderName =
                    Path.GetFileName(moduleFolder);
                
                modules.Add(new AvailableModule
                {
                    Path = moduleFolder,
                    FolderName = moduleFolderName,
                    ModuleDll = moduleFile[0]
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin");
            }
        }
        
        return [..modules];
    }
    
    private static readonly string InformationalVersionTypeName = typeof(AssemblyInformationalVersionAttribute).FullName!;
    
    public Task<ImmutableDictionary<string, SemVersion>> GetModuleVersions()
    {
        var modules = GetModules();
        
        var moduleVersions = new Dictionary<string, SemVersion>();
        
        foreach (var availableModule in modules)
        {
            try
            {
                var assemblyDef = dnlib.DotNet.AssemblyDef.Load(availableModule.ModuleDll);
                var customAttributes = assemblyDef.CustomAttributes;
                var informationalVersion = customAttributes.FirstOrDefault(x => x.AttributeType.FullName == InformationalVersionTypeName);
                
                if (informationalVersion is null)
                {
                    _logger.LogError("No informational version found for {ModuleDll}", availableModule.ModuleDll);
                    continue;
                }
                
                if (informationalVersion.ConstructorArguments.Count <= 0)
                {
                    _logger.LogError("No version argument found for {ModuleDll}", availableModule.ModuleDll);
                    continue;
                }
                
                var version = informationalVersion.ConstructorArguments[0];
                if (version.Value is not UTF8String versionString)
                {
                    _logger.LogError("Version argument is not a string for {ModuleDll}", availableModule.ModuleDll);
                    continue;
                }
                
                if (!SemVersion.TryParse(versionString.String, SemVersionStyles.Strict, out var semVersion))
                {
                    _logger.LogError("Failed to parse sem-version {Version} for module {ModuleDll}. It needs to be a valid semantic version.", versionString.String, availableModule.ModuleDll);
                    continue;
                }
                
                moduleVersions[availableModule.FolderName] = semVersion;

            } catch (Exception e)
            {
                _logger.LogError(e, "Failed to get module version for {ModuleDll}", availableModule.ModuleDll);
            }
        }
        
        return Task.FromResult(moduleVersions.ToImmutableDictionary());
    }

    private bool _disposed;
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _repositoryUpdatedSubscription.Dispose();
    }
}

public struct AvailableModule
{
    public required string Path { get; set; }
    public required string ModuleDll { get; set; }
    public required string FolderName { get; set; }
}