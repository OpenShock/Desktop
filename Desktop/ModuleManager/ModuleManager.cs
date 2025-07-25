﻿using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using dnlib.DotNet;
using OneOf;
using OneOf.Types;
using OpenShock.Desktop.Backend;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase;
using OpenShock.Desktop.ModuleManager.Implementation;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Services;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Models;
using Semver;

namespace OpenShock.Desktop.ModuleManager;

public sealed class ModuleManager : IAsyncDisposable
{
    private static readonly Type ModuleBaseType = typeof(DesktopModuleBase);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ConfigManager _configManager;
    private readonly IAsyncDisposable _repositoryUpdatedSubscription;
    
    public IMinimalEventObservable ModulesLoaded => _modulesLoaded;
    private readonly MinimalEvent _modulesLoaded = new();

    private static string ModuleDirectory => Path.Combine(Constants.AppdataFolder, "modules");

    private static readonly HttpClient HttpClient;
    
    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();

    public IEnumerable<PermissionType> RequiredPermissions =>
        Modules.SelectMany(x => x.Value.RequiredPermissions).Distinct();

    static ModuleManager()
    {
        HttpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = TimeSpan.FromHours(1)
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        HttpClient.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
    }

    public ModuleManager(IServiceProvider serviceProvider, ILogger<ModuleManager> logger,
        RepositoryManager repositoryManager, ConfigManager configManager)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _repositoryManager = repositoryManager;
        _configManager = configManager;

        _repositoryUpdatedSubscription = _repositoryManager.RepositoriesUpdated.SubscribeAsync(() =>
        {
            RefreshRepositoryInformationOnLoaded();
            return Task.CompletedTask;
        }).Result;
    }

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

        var moduleDownload = await DownloadModule(moduleId, moduleVersion.Url, moduleVersion.Sha256Hash);
        if (moduleDownload.IsT0)
        {
            return new Success();
        }

        return new Error();
    }

    private async Task<OneOf<Success, Error>> DownloadModule(string moduleId, Uri moduleUrl, byte[] remoteSha256)
    {
        _logger.LogDebug("Downloading module {ModuleId} from {ModuleUrl}", moduleId, moduleUrl);
        using var downloadResponse = await HttpClient.GetAsync(moduleUrl, HttpCompletionOption.ResponseHeadersRead);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to download module {ModuleId} from {ModuleUrl} with status code {StatusCode}",
                moduleId, moduleUrl, downloadResponse.StatusCode);
            return new Error();
        }

        var mediaType = downloadResponse.Content.Headers.ContentType?.MediaType;
        
        if (mediaType == null || !Constants.ModuleZipAllowedMediaTypeNames.Contains(mediaType))
        {
            _logger.LogError(
                "Failed to download module {ModuleId} from {ModuleUrl} as it is not a ZIP file (Content-Type: {ContentType})",
                moduleId, moduleUrl, downloadResponse.Content.Headers.ContentType?.MediaType);
            return new Error();
        }

        await using var fileBytes = await downloadResponse.Content.ReadAsStreamAsync();
        await using var fileMemoryStream = new MemoryStream();
        await fileBytes.CopyToAsync(fileMemoryStream);
        fileMemoryStream.Seek(0, SeekOrigin.Begin);
        var downloadedHash = await SHA256.HashDataAsync(fileMemoryStream);
        
        if (!downloadedHash.SequenceEqual(remoteSha256))
        {
            var downloadedHashString = Convert.ToHexString(downloadedHash);
            var remoteHashString = Convert.ToHexString(remoteSha256);
            _logger.LogError("Failed to download module {ModuleId} from {ModuleUrl} as the SHA256 hash does not match. {Downloaded Hash} - {Remote Hash}", moduleId, moduleUrl, downloadedHashString, remoteHashString);
            return new Error();
        }
        
        _logger.LogTrace("Hashes match for module {ModuleId} from {ModuleUrl}. {Downloaded Hash} - {Remote Hash}", moduleId, moduleUrl, downloadedHash, remoteSha256);

        var moduleFolderPath = Path.Combine(ModuleDirectory, moduleId);

        if (Directory.Exists(moduleFolderPath)) Directory.Delete(moduleFolderPath, true);

        Directory.CreateDirectory(moduleFolderPath);
        fileMemoryStream.Seek(0, SeekOrigin.Begin);
        ZipFile.ExtractToDirectory(fileMemoryStream, moduleFolderPath, true);

        return new Success();
    }

    private void LoadModule(string moduleFolderPath, string moduleDll, string moduleFolderName)
    {
        _logger.LogInformation("Loading module: {Path}", moduleDll);

        var assemblyLoadContext = new ModuleAssemblyLoadContext(moduleFolderPath);
        var assembly = assemblyLoadContext.LoadFromAssemblyPath(moduleDll);

        var moduleAttribute = assembly.GetCustomAttribute<DesktopModuleAttribute>();
        var requiredPermissionsAttribute = assembly.GetCustomAttributes<RequiredPermissionAttribute>();
        
        if (moduleAttribute is null)
        {
            _logger.LogError("Module Assembly does not have a module attribute!");
            return;
        }

        if (!moduleAttribute.ModuleType.IsAssignableTo(ModuleBaseType))
        {
            _logger.LogError("Module Attribute: Type does not inherit from DesktopModuleBase!");
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
            _logger.LogError("Failed to parse sem-version {Version} for module {ModuleId}. It needs to be a valid semantic version.", assemblyVersion, moduleAttribute.ModuleType.FullName);
            return;
        }
        
        var module = (DesktopModuleBase?)ActivatorUtilities.CreateInstance(_serviceProvider, moduleAttribute.ModuleType);
        if (module is null) throw new Exception("Failed to instantiate module!");
        
        var loadedModule = new LoadedModule
        {
            LoadContext = assemblyLoadContext,
            ModuleAttribute = moduleAttribute,
            Assembly = assembly,
            Module = module,
            Version = loadedModuleVersion,
            AvailableVersion = null,
            RepositoryModule = null,
            RequiredPermissions = requiredPermissionsAttribute.Select(x => x.Permission).Select(x => x.GetPermissionType()).ToArray()
        };
        
        module.SetContext(new ModuleInstanceManager(loadedModule, _serviceProvider.GetRequiredService<ILoggerFactory>(), _serviceProvider)
        {
            AppServiceProvider = _serviceProvider
        });
        
        if (moduleFolderName != loadedModule.Id)
        {
            _logger.LogError(
                "Module folder name does not match module ID! [{FolderName} != {ModuleName}]. This might cause issues and is not expected. Updating for sure wont work properly :3 Please fix this",
                moduleFolderName, loadedModule.Id);
            return;
        }

        if (!Modules.TryAdd(loadedModule.Id, loadedModule)) throw new Exception("Module already loaded!");
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
                
                var mostRecentRelease = repoModule.Versions.Keys.GetLatestReleaseVersion();
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
        _modulesLoaded.Invoke();
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
    
    private ImmutableDictionary<string, SemVersion> GetModuleVersions()
    {
        var modules = GetModules();
        
        var moduleVersions = new Dictionary<string, SemVersion>();
        
        foreach (var availableModule in modules)
        {
            _logger.LogTrace("Checking for updates for {ModuleId}", availableModule.FolderName);
            try
            {
                var assemblyDef = AssemblyDef.Load(availableModule.ModuleDll);
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
        
        return moduleVersions.ToImmutableDictionary();
    }

    public void ProcessAutoUpdates()
    {
        if(!_configManager.Config.Modules.AutoUpdate) return;
        
        var repoModules = _repositoryManager.Repositories
            .Where(x => x.Value.Repository != null)
            .SelectMany(x => x.Value.Repository!.Modules)
            .ToImmutableDictionary();
        
        var moduleVersions = GetModuleVersions();

        foreach (var moduleVersion in moduleVersions)
        {
            if (!repoModules.TryGetValue(moduleVersion.Key, out var repoModule))
            {
                _logger.LogDebug("Module {ModuleId} not found in repositories", moduleVersion.Key);
                continue;
            }

            var currentlyPrereleaseInstalled = moduleVersion.Value.IsPrerelease;
            
            var latestVersion = currentlyPrereleaseInstalled ? repoModule.Versions.Keys.GetLatestVersion() : repoModule.Versions.Keys.GetLatestReleaseVersion();
         
            if(latestVersion == null)
            {
                _logger.LogDebug("No releases found for module {ModuleId}", moduleVersion.Key);
                continue;
            }

            if (moduleVersion.Value.ComparePrecedenceTo(latestVersion) >= 0) continue;
            
            _logger.LogInformation("Queuing update for module {ModuleId} from {CurrentVersion} to {LatestVersion}", moduleVersion.Key, moduleVersion.Value, latestVersion);
                
            _configManager.Config.Modules.ModuleTasks[moduleVersion.Key] = new InstallModuleTask
            {
                Version = latestVersion
            };
        }
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _repositoryUpdatedSubscription.DisposeAsync();
    }
}

public struct AvailableModule
{
    public required string Path { get; set; }
    public required string ModuleDll { get; set; }
    public required string FolderName { get; set; }
}