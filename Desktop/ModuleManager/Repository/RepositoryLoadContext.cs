using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using OneOf;
using OneOf.Types;
using OpenShock.Desktop.Utils;
using OpenShock.SDK.CSharp.Updatables;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OpenShock.Desktop.ModuleManager.Repository;

public sealed class RepositoryLoadContext
{
    private static readonly HttpClient HttpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly ILogger Logger = Log.ForContext<RepositoryLoadContext>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, AllowTrailingCommas = true,
        Converters = { new SemVersionConverter() }
    };

    public required Uri RepositoryUrl { get; init; }

    public IUpdatable<RepositoryLoadContextState> State => _state;
    
    private readonly UpdatableVariable<RepositoryLoadContextState> _state =
        new(RepositoryLoadContextState.Queued);
    
    public Repository? Repository { get; private set; } = null;
    
    public LoadError? LastError { get; private set; } = null;
    
    public struct LoadError
    {
        public required HttpStatusCode? HttpStatusCode { get; init; }
        public required Exception? Exception { get; init; }
        public required string? Response { get; init; }
    }


    [SetsRequiredMembers]
    public RepositoryLoadContext(Uri repositoryUrl)
    {
        RepositoryUrl = repositoryUrl;
    }

    /// <summary>
    /// Load the repository from the URL
    /// </summary>
    /// <returns>
    /// Success when successfully fetched repository
    /// <see cref="Error{HttpStatusCode}" /> when failed to fetch repository with HTTP status code not being 2xx
    /// <see cref="LastError" /> when failed to fetch repository with unknown error
    /// </returns>
    public async Task<OneOf<Success, LoadError>> Load()
    {
        var loadInternal = await LoadInternal().ConfigureAwait(false);
        if (loadInternal.IsT1)
        {
            var error = loadInternal.AsT1;
            LastError = error;
            _state.Value = RepositoryLoadContextState.Failed;
        }

        return loadInternal;
    }
    
    
    private async Task<OneOf<Success, LoadError>> LoadInternal()
    {
        _state.Value = RepositoryLoadContextState.Loading;
        HttpResponseMessage? response = null;
        
        try
        {
            response = await HttpClient.GetAsync(RepositoryUrl).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warning("Failed to fetch repository from [{Url}], failed with {StatusCode}", RepositoryUrl,
                    response.StatusCode);
                
                return new LoadError
                {
                    HttpStatusCode = response.StatusCode,
                    Response = await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                    Exception = null
                };
            }
            

            var repository =
                await JsonSerializer.DeserializeAsync<Repository>(await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                    JsonOptions).ConfigureAwait(false);
            if (repository == null)
            {
                Logger.Warning("Failed to deserialize repository from [{Url}]", RepositoryUrl);
                
                return new LoadError
                {
                    HttpStatusCode = response.StatusCode,
                    Response = await response.Content.ReadAsStringAsync(),
                    Exception = null
                };
            }

            Repository = repository;
            _state.Value = RepositoryLoadContextState.Loaded;
            return new Success();
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to fetch repository from [{Url}]. Exception thrown", RepositoryUrl);

            string? responseContent = null;
            
            try
            {
                if (response != null) responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to get response content from failed repository fetch from [{Url}]. Exception thrown", RepositoryUrl);
            }
            
            return new LoadError
            {
                HttpStatusCode = response?.StatusCode,
                Response = responseContent,
                Exception = e
            };
        }
    }
}

public enum RepositoryLoadContextState
{
    Queued,
    Loading,
    Loaded,
    Failed
}