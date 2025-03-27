using System.Diagnostics;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.Services;

public sealed class StartupService
{
    private readonly ILogger<StartupService> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly Updater _updater;
    private readonly ConfigManager _configManager;
    private readonly AuthService _authService;

    public IObservableVariable<bool> IsStarted => _isStartedObservable; 
    
    private readonly ObservableVariable<bool> _isStartedObservable = new(false);

    public StartupStatus Status { get; } = new();

    public StartupService(
        ILogger<StartupService> logger,
        RepositoryManager repositoryManager,
        ModuleManager.ModuleManager moduleManager,
        Updater updater,
        ConfigManager configManager,
        AuthService authService)
    {
        _logger = logger;
        _repositoryManager = repositoryManager;
        _moduleManager = moduleManager;
        _updater = updater;
        _configManager = configManager;
        _authService = authService;
    }
    
    private volatile bool _isStarted;
    
    public async Task StartupApp()
    {
        if (_isStarted) return;
        _isStarted = true;
        
        _logger.LogDebug("Checking for updates");
        await Status.Update("Checking for updates");
        
        try
        {
            await _updater.CheckUpdate().ConfigureAwait(false);
            // TODO: Check if auto update is enabled and update if so, probably want to shutdown the startup after this if so
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while checking for updates");
        }
        
        _logger.LogDebug("Fetching repositories");
        await Status.Update("Fetching repositories");

        await using (await _repositoryManager.FetcherState.ValueUpdated.SubscribeAsync(_ => UpdateStateForRepositories()))
        await using (await _repositoryManager.FetchedRepositories.ValueUpdated.SubscribeAsync(_ => UpdateStateForRepositories()))
        await using (await _repositoryManager.RepositoriesStateChanged.SubscribeAsync(_ => UpdateStateForRepositories()))
        {
            try
            {
                await _repositoryManager.FetchRepositories().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while fetching repositories");
            }

        }

        _logger.LogDebug("Processing module updates");
        await Status.Update("Processing module updates");

        try
        {
            _moduleManager.ProcessAutoUpdates();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while processing module updates");
        }
        
        _logger.LogDebug("Processing module tasks");
        await Status.Update("Processing module tasks");
        
        try
        {
            await _moduleManager.ProcessTaskList().ConfigureAwait(false);
        } catch (Exception e)
        {
            _logger.LogError(e, "Error while processing module tasks");
        }
        
        _logger.LogDebug("Loading modules");
        await Status.Update("Loading modules");
        
        try
        {
            _moduleManager.LoadAll(); // Progress tracking on this, maybe use IEnumerable and out value? Dont know if thats possible, otherwise just use events or something
        } catch (Exception e)
        {
            _logger.LogError(e, "Error while loading modules");
        }

        var totalModules = (uint)_moduleManager.Modules.Count;
        
        _logger.LogDebug("Setting up modules");
        await Status.Update("Setting up modules",0, totalModules);
        
        uint j = 0;
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await Status.Update("Setting up modules", ++j, totalModules);
            try
            {
                await moduleManagerModule.Value.Module.Setup();    
            } catch (Exception e)
            {
                _logger.LogError(e, "Error while setting up module {Module}", moduleManagerModule.Key);
            }
        }
        
        _logger.LogDebug("Starting modules");
        await Status.Update("Starting modules", 0, totalModules);
        
        uint i = 0;
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await Status.Update("Starting modules", ++i, totalModules);
            try
            {
                await moduleManagerModule.Value.Module.Start();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while starting module {Module}", moduleManagerModule.Key);;
            }
        }
        
        _isStartedObservable.Value = true;
        
        await Status.Update("Starting complete, redirecting to dashboard");

#pragma warning disable CS4014
        OsTask.Run(_authService.Authenticate);
#pragma warning restore CS4014
    }
    
    private Task UpdateStateForRepositories()
    {
        return Status.Update("Fetching repositories", _repositoryManager.FetchedRepositories.Value, (uint)_repositoryManager.Repositories.Count);
    }
}

public sealed class StartupStatus
{
    public IAsyncMinimalEventObservable<StartupStatus> Updated => _updatedObservable;
    
    private readonly AsyncMinimalEvent<StartupStatus> _updatedObservable = new();
    public string StepName { get; private set; } = "Starting";
    
    public uint ProgressCurrent { get; private set; } = 0;
    public uint? ProgressTotal { get; private set; } = null;

    internal async Task Update(string stepName, uint progressCurrent = 0, uint? progressTotal = null)
    {
        StepName = stepName;
        ProgressCurrent = progressCurrent;
        ProgressTotal = progressTotal;
        
        await _updatedObservable.InvokeAsyncParallel(this);
    }
}