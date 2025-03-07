using System.Reactive.Subjects;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.Desktop.ReactiveExtensions;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Updatables;

namespace OpenShock.Desktop.Services;

public sealed class StartupService
{
    private readonly ILogger<StartupService> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly Updater _updater;
    private readonly ConfigManager _configManager;
    
    public IObservableVariable<bool> IsStarted => _isStartedObservable; 
    
    private readonly ObservableVariable<bool> _isStartedObservable = new(false);

    public StartupStatus Status { get; } = new();

    public StartupService(
        ILogger<StartupService> logger,
        RepositoryManager repositoryManager,
        ModuleManager.ModuleManager moduleManager,
        Updater updater,
        ConfigManager configManager)
    {
        _logger = logger;
        _repositoryManager = repositoryManager;
        _moduleManager = moduleManager;
        _updater = updater;
        _configManager = configManager;
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

        await using (await _repositoryManager.FetcherState.ValueUpdated.SubscribeConcurrentAsync(_ => UpdateStateForRepositories()))
        await using (await _repositoryManager.FetchedRepositories.ValueUpdated.SubscribeConcurrentAsync(_ => UpdateStateForRepositories()))
        await using (await _repositoryManager.RepositoriesStateChanged.SubscribeConcurrentAsync(_ => UpdateStateForRepositories()))
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
            _moduleManager.LoadAll();
        } catch (Exception e)
        {
            _logger.LogError(e, "Error while loading modules");
        }
        
        _logger.LogDebug("Setting up modules");
        await Status.Update("Setting up modules");
        
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await moduleManagerModule.Value.Module.Setup();
        }
        
        _logger.LogDebug("Starting modules");
        await Status.Update("Starting modules");
        
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await moduleManagerModule.Value.Module.Start();
        }
        
        _isStartedObservable.Value = true;
    }
    
    private Task UpdateStateForRepositories()
    {
        return Status.Update("Fetching repositories", _repositoryManager.FetchedRepositories.Value, (uint)_repositoryManager.Repositories.Count);
    }
}

public sealed class StartupStatus
{
    public IAsyncObservable<StartupStatus> Updated => _updatedObservable;
    
    private readonly ConcurrentSimpleAsyncSubject<StartupStatus> _updatedObservable = new();
    public string StepName { get; private set; } = "Starting";
    
    public uint ProgressCurrent { get; private set; } = 0;
    public uint? ProgressTotal { get; private set; } = null;

    internal async Task Update(string stepName, uint progressCurrent = 0, uint? progressTotal = null)
    {
        StepName = stepName;
        ProgressCurrent = progressCurrent;
        ProgressTotal = progressTotal;

        await _updatedObservable.OnNextAsync(this);
    }
}