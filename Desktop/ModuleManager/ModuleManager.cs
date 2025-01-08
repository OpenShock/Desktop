﻿using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.Loader;
using ModuleBase;
using OneOf;
using OneOf.Types;
using Semver;
using Module = OpenShock.Desktop.ModuleManager.Repository.Module;

namespace OpenShock.Desktop.ModuleManager;

public sealed class ModuleManager
{
    private static readonly Type ModuleType = typeof(IModule);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModuleManager> _logger;

    private static string ModuleDirectory => Path.Combine(Constants.AppdataFolder, "modules");

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public ModuleManager(IServiceProvider serviceProvider, ILogger<ModuleManager> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public readonly ConcurrentDictionary<string, LoadedModule> Modules = new();

    public async Task<OneOf<Success, Error, NotFound>> DownloadModule(KeyValuePair<string, Module> modulePair, SemVersion version)
    {
        if (!modulePair.Value.Versions.TryGetValue(version, out var moduleVersion))
        {
            return new NotFound();
        }

        var moduleDownload = await DownloadModule(modulePair.Key, moduleVersion.Url);
        if (moduleDownload.IsT0) return new Success();
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
            _logger.LogError("Failed to download module {ModuleId} from {ModuleUrl} as it is not a ZIP file (Content-Type: {ContentType})",
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

        var pluginTypes = assembly.GetTypes().Where(t => t.IsAssignableTo(ModuleType)).ToArray();
        switch (pluginTypes.Length)
        {
            case 0:
                _logger.LogWarning("No modules found in DLL!");
                return;
            case > 1:
                _logger.LogWarning("Expected 1 module, found {NumberOfModules}", pluginTypes.Length);
                return;
        }

        var module = (IModule?)ActivatorUtilities.CreateInstance(_serviceProvider, pluginTypes[0]);
        if (module is null) throw new Exception("Failed to instantiate module!");

        var loadedModule = new LoadedModule
        {
            LoadContext = assemblyLoadContext,
            Assembly = assembly,
            Module = module
        };

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
    }
}