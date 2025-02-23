using System.Reactive.Subjects;
using OpenShock.Desktop.Config;
using OpenShock.Desktop.ModuleBase.Utils;
using OpenShock.Desktop.Utils;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class RepositoryManager
{
    public IObservableVariable<uint> FetchedRepositories => _fetchedRepositories;
    private readonly ObservableVariable<uint> _fetchedRepositories = new(0);
    
    public IObservableVariable<FetcherState> FetcherState => _fetcherState;
    private readonly ObservableVariable<FetcherState> _fetcherState = new(Desktop.ModuleManager.Repository.FetcherState.Idle);
    
    public IAsyncObservable<Uri> RepositoriesStateChanged => _repositoriesStateChanged;
    private readonly ConcurrentSimpleAsyncSubject<Uri> _repositoriesStateChanged = new ConcurrentSimpleAsyncSubject<Uri>();
    
    public IReadOnlyDictionary<Uri, RepositoryLoadContext> Repositories => _repositories;
    private readonly Dictionary<Uri, RepositoryLoadContext> _repositories = new Dictionary<Uri, RepositoryLoadContext>();
    
    private readonly ConfigManager _config;
    private readonly ILogger<RepositoryManager> _logger;
    
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
        
        _repositories.Clear();
        _fetchedRepositories.Value = 0;
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.SettingUp;

        foreach (var repoUrl in Constants.BuiltInModuleRepositories)
        {
            _repositories.TryAdd(repoUrl, new RepositoryLoadContext(repoUrl));
        }
        
        foreach (var repoUrl in _config.Config.Modules.ModuleRepositories)
        {
            _repositories.TryAdd(repoUrl, new RepositoryLoadContext(repoUrl));
        }
        
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.Fetching;
        
        foreach (var repoLoadContext in _repositories)
        {
            await repoLoadContext.Value.State.ValueUpdated.SubscribeAsync(_ => ReposOnStateChange(repoLoadContext.Key));
            // We dont need to care about disposing this, we will just let it be garbage collected when we clear the repositories
            
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
        
        _fetcherState.Value = Desktop.ModuleManager.Repository.FetcherState.Idle;
    }

    private ValueTask ReposOnStateChange(Uri repoUri)
    {
        return _repositoriesStateChanged.OnNextAsync(repoUri);
    }
}

public enum FetcherState
{
    Idle,
    SettingUp,
    Fetching
}