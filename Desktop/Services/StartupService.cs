using OpenShock.Desktop.ModuleManager.Repository;
using OpenShock.SDK.CSharp.Updatables;

namespace OpenShock.Desktop.Services;

public sealed class StartupService
{
    private readonly ILogger<StartupService> _logger;
    private readonly RepositoryManager _repositoryManager;
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly Updater _updater;
    
    public StartupStatus Status { get; } = new();

    public StartupService(ILogger<StartupService> logger, RepositoryManager repositoryManager, ModuleManager.ModuleManager moduleManager, Updater updater)
    {
        _logger = logger;
        _repositoryManager = repositoryManager;
        _moduleManager = moduleManager;
        _updater = updater;
    }
    
    private bool _isStarted = false;
    
    public async Task StartupApp()
    {
        if (_isStarted) return;
        _isStarted = true;
        
        _logger.LogDebug("Checking for updates");
        Status.Update("Checking for updates");
        
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
        Status.Update("Fetching repositories");

        _repositoryManager.FetcherState.OnValueChanged += FetcherStateOnOnValueChanged;
        _repositoryManager.FetchedRepositories.OnValueChanged += FetchedRepositoriesOnOnValueChanged;
        _repositoryManager.RepositoriesStateChanged += RepositoryManagerOnRepositoriesStateChanged;
        
        try
        {
            await _repositoryManager.FetchRepositories().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while fetching repositories");
        }
        
        _repositoryManager.FetcherState.OnValueChanged -= FetcherStateOnOnValueChanged;
        _repositoryManager.FetchedRepositories.OnValueChanged -= FetchedRepositoriesOnOnValueChanged;
        _repositoryManager.RepositoriesStateChanged -= RepositoryManagerOnRepositoriesStateChanged;

        _logger.LogDebug("Processing module updates");
        Status.Update("Processing module updates");
        
        try
        {
            await _moduleManager.ProcessTaskList().ConfigureAwait(false);
        } catch (Exception e)
        {
            _logger.LogError(e, "Error while processing module tasks");
        }
        
        _logger.LogDebug("Loading modules");
        Status.Update("Loading modules");
        
        try
        {
            _moduleManager.LoadAll();
        } catch (Exception e)
        {
            _logger.LogError(e, "Error while loading modules");
        }
        
        _logger.LogDebug("Setting up modules");
        Status.Update("Setting up modules");
        
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await moduleManagerModule.Value.Module.Setup();
        }
        
        _logger.LogDebug("Starting modules");
        Status.Update("Starting modules");
        
        foreach (var moduleManagerModule in _moduleManager.Modules)
        {
            await moduleManagerModule.Value.Module.Start();
        }
    }
    
    private void RepositoryManagerOnRepositoriesStateChanged()
    {
        UpdateStateForRepositories();
    }

    private void FetchedRepositoriesOnOnValueChanged(uint arg)
    {
        UpdateStateForRepositories();
    }

    private void FetcherStateOnOnValueChanged(FetcherState arg)
    {
        UpdateStateForRepositories();
    }
    
    private void UpdateStateForRepositories()
    {
        Status.Update("Fetching repositories", _repositoryManager.FetchedRepositories.Value, (uint)_repositoryManager.Repositories.Count);
    }
}

public sealed class StartupStatus
{
    public event Action? OnChanged;
    
    public string StepName { get; private set; } = "Starting";
    
    public uint ProgressCurrent { get; private set; } = 0;
    public uint? ProgressTotal { get; private set; } = null;

    protected internal void Update(string stepName, uint progressCurrent = 0, uint? progressTotal = null)
    {
        StepName = stepName;
        ProgressCurrent = progressCurrent;
        ProgressTotal = progressTotal;
    }
}