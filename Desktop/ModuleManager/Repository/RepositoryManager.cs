using System.Text.Json;
using OpenShock.Desktop.Config;
using OpenShock.SDK.CSharp.Updatables;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class RepositoryManager
{
    public IAsyncUpdatable<uint> FetchedRepositories => _fetchedRepositories;
    private readonly AsyncUpdatableVariable<uint> _fetchedRepositories = new(0);
    
    public IAsyncUpdatable<FetcherState> FetcherState => _fetcherState;
    private readonly AsyncUpdatableVariable<FetcherState> _fetcherState = new(Desktop.ModuleManager.Repository.FetcherState.Idle);
    
    public event Action? RepositoriesStateChanged;
    
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
        
        foreach (var repositoryLoadContext in _repositories)
        {
            repositoryLoadContext.Value.State.OnValueChanged -= ReposOnStateChange;
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
            repoLoadContext.Value.State.OnValueChanged += ReposOnStateChange;
            
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

    private void ReposOnStateChange(RepositoryLoadContextState arg)
    {
        RepositoriesStateChanged?.Invoke();
    }
}

public enum FetcherState
{
    Idle,
    SettingUp,
    Fetching
}