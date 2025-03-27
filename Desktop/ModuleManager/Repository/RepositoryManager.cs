using System.Collections.Immutable;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class RepositoryManager
{
    public IObservableVariable<uint> FetchedRepositories => _fetchedRepositories;
    private readonly ObservableVariable<uint> _fetchedRepositories = new(0);
    
    public IObservableVariable<FetcherState> FetcherState => _fetcherState;
    private readonly ObservableVariable<FetcherState> _fetcherState = new(Desktop.ModuleManager.Repository.FetcherState.Idle);
    
    public IAsyncMinimalEventObservable<Uri> RepositoriesStateChanged => _repositoriesStateChanged;
    private readonly AsyncMinimalEvent<Uri> _repositoriesStateChanged = new AsyncMinimalEvent<Uri>();
    
    public IAsyncMinimalEventObservable RepositoriesUpdated => _repositoriesUpdated;
    private readonly AsyncMinimalEvent _repositoriesUpdated = new AsyncMinimalEvent();
    
    public ImmutableDictionary<Uri, RepositoryLoadContext> Repositories { get; private set; } = new Dictionary<Uri, RepositoryLoadContext>().ToImmutableDictionary();

    private readonly ConfigManager _config;
    private readonly ILogger<RepositoryManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public RepositoryManager(ConfigManager config, ILogger<RepositoryManager> logger)
    {
        _config = config;
        _logger = logger;
    }
    
    
    public async Task FetchRepositories()
    {
        if (_fetcherState.Value != Desktop.ModuleManager.Repository.FetcherState.Idle)
        {
            _logger.LogWarning("Already fetching repositories, skipping");
            return;
        }

        if (!await _semaphore.WaitAsync(0))
        {
            _logger.LogWarning("Couldn't enter semaphore, already fetching repositories");
            return;
        }

        try
        {
            await FetchRepositoriesInternal();
        }
        finally
        {
            _semaphore.Release();
        }

    }
    
    private async Task FetchRepositoriesInternal()
    {
        var newRepositories = new Dictionary<Uri, RepositoryLoadContext>();

        _fetchedRepositories.Value = 0;
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.SettingUp;

        foreach (var repoUrl in Constants.BuiltInModuleRepositories)
        {
            newRepositories.TryAdd(repoUrl, new RepositoryLoadContext(repoUrl));
        }
        
        foreach (var repoUrl in _config.Config.Modules.ModuleRepositories)
        {
            newRepositories.TryAdd(repoUrl, new RepositoryLoadContext(repoUrl));
        }
        
        Repositories = newRepositories.ToImmutableDictionary();
        
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.Fetching;
        
        foreach (var repoLoadContext in newRepositories)
        {
            await repoLoadContext.Value.State.ValueUpdated.SubscribeAsync(_ => _repositoriesStateChanged.InvokeAsyncParallel(repoLoadContext.Key));
            // We don't need to care about disposing this, we will just let it be garbage collected when we clear the repositories
            
            try
            {
                await repoLoadContext.Value.Load().ConfigureAwait(false);
            }
            catch (Exception e) // Just in case there is something very wrong
            {
                _logger.LogError(e, "Error while fetching repository {Url}", repoLoadContext.Key);
            }

            _fetchedRepositories.Value++;
        }

#pragma warning disable CS4014
        OsTask.Run(_repositoriesUpdated.InvokeAsyncParallel);
#pragma warning restore CS4014
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.Idle;
    }
}

public enum FetcherState
{
    Idle,
    SettingUp,
    Fetching
}