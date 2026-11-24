using System.Text.Json.Serialization;

namespace OpenShock.Desktop.Models;

public class GithubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; set; }
    
    [JsonPropertyName("id")]
    public required ulong Id { get; set; }
    
    [JsonPropertyName("draft")]
    public required bool Draft { get; set; }
    
    [JsonPropertyName("prerelease")]
    public required bool Prerelease { get; set; }
    
    [JsonPropertyName("assets")]
    public required IReadOnlyList<Asset> Assets { get; set; }
    
    [JsonPropertyName("body")]
    public required string Body { get; set; }

    public class Asset
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("browser_download_url")]
        public required Uri BrowserDownloadUrl { get; set; }
    }
}