// This is a modified version of the EmbeddedFileProvider from Microsoft.Extensions.FileProviders.
// https://github.com/dotnet/aspnetcore/blob/main/src/FileProviders/Embedded/src/EmbeddedFileProvider.cs

using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace OpenShock.Desktop;

// TODO: Make a reverse function for the MakeValidEverettIdentifier method and reverse the lookup in GetFileInfo, also make it not case-sensitive
/// <summary>
/// Looks up files using embedded resources in the specified assembly.
/// This file provider is case-sensitive.
/// </summary>
public sealed class ModuleFileProvider : IFileProvider, IDisposable
{
    private readonly ModuleManager.ModuleManager _moduleManager;

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars()
        .Where(c => c != '/' && c != '\\').ToArray();
    
    private Assembly[] _assemblies = [];

    private FrozenDictionary<string, EmbeddedResourceFileInfo> _files = FrozenDictionary<string, EmbeddedResourceFileInfo>.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleFileProvider" />
    /// </summary>
    public ModuleFileProvider(ModuleManager.ModuleManager moduleManager)
    {
        _moduleManager = moduleManager;
        
        _modulesLoadedSubscription = _moduleManager.ModulesLoaded.Subscribe(UpdateAssemblies);
        UpdateAssemblies();
    }

    private void UpdateAssemblies()
    {
        _assemblies = _moduleManager.Modules.Select(x => x.Value.Assembly).Distinct().ToArray();
        SetupFileList();
    }
    
    private void SetupFileList()
    {
        var currentTime = DateTimeOffset.UtcNow;
        var files = new Dictionary<string, EmbeddedResourceFileInfo>();
        
        foreach (var assembly in _assemblies)
        {
            var resourcesInAssembly = assembly.GetManifestResourceNames();

            var lastModified = TryGetLastModified(assembly) ?? currentTime;
            
            foreach (var resourceName in resourcesInAssembly)
            {
                var fileInfo = new EmbeddedResourceFileInfo(assembly, resourceName, resourceName, lastModified);
                files[resourceName] = fileInfo;
            }
        }

        _files = files.ToFrozenDictionary();
    }
    

    /// <summary>
    /// Locates a file at the given path.
    /// </summary>
    /// <param name="subpath">The path that identifies the file. </param>
    /// <returns>
    /// The file information. Caller must check Exists property. A <see cref="NotFoundFileInfo" /> if the file could
    /// not be found.
    /// </returns>
    public IFileInfo GetFileInfo(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return new NotFoundFileInfo(subpath);
        }

        var builder = new StringBuilder(subpath.Length);

        // Relative paths starting with a leading slash okay
        if (subpath.StartsWith('/'))
        {
            subpath = subpath[1..];
        }

        // Make valid everett id from directory name
        // The call to this method also replaces directory separator chars to dots
        var everettId = MakeValidEverettIdentifier(Path.GetDirectoryName(subpath));

        // if directory name was empty, everett id is empty as well
        if (!string.IsNullOrEmpty(everettId))
        {
            builder.Append(everettId);
            builder.Append('.');
        }

        // Append file name of path
        builder.Append(Path.GetFileName(subpath));

        var resourcePath = builder.ToString();
        if (HasInvalidPathChars(resourcePath))
        {
            return new NotFoundFileInfo(resourcePath);
        }

        var name = Path.GetFileName(subpath);
        if (!_files.TryGetValue(resourcePath, out var fileInfo))
        {
            return new NotFoundFileInfo(name);
        }

        return new EmbeddedResourceFileInfo(fileInfo.Assembly, resourcePath, name, fileInfo.LastModified);
    }

    /// <summary>
    /// This is not implemented
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IDirectoryContents GetDirectoryContents(string? subpath)
    {
        return NotFoundDirectoryContents.Singleton; // Not implemented
    }

    /// <summary>
    /// Embedded files do not change.
    /// </summary>
    /// <param name="pattern">This parameter is ignored</param>
    /// <returns>A <see cref="NullChangeToken" /></returns>
    public IChangeToken Watch(string pattern)
    {
        return NullChangeToken.Singleton;
    }

    private static bool HasInvalidPathChars(string path)
    {
        return path.IndexOfAny(InvalidFileNameChars) != -1;
    }

    #region Helper methods

    /// <summary>
    /// Try to get the last modified time of an assembly file on disk.
    /// </summary>
    /// <param name="assembly"></param>
    /// <returns></returns>
    private static DateTimeOffset? TryGetLastModified(Assembly assembly)
    {
        try
        {
            return File.GetLastWriteTimeUtc(assembly.Location);
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch (Exception)
        {
        }

        return null;
    }
    
    /// <summary>
    /// Is the character a valid first Everett identifier character?
    /// </summary>
    private static bool IsValidEverettIdFirstChar(char c)
    {
        return
            char.IsLetter(c) ||
            CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.ConnectorPunctuation;
    }

    /// <summary>
    /// Is the character a valid Everett identifier character?
    /// </summary>
    private static bool IsValidEverettIdChar(char c)
    {
        var cat = CharUnicodeInfo.GetUnicodeCategory(c);

        return
            char.IsLetterOrDigit(c) ||
            cat == UnicodeCategory.ConnectorPunctuation ||
            cat == UnicodeCategory.NonSpacingMark ||
            cat == UnicodeCategory.SpacingCombiningMark ||
            cat == UnicodeCategory.EnclosingMark;
    }

    /// <summary>
    /// Make a folder subname into an Everett-compatible identifier 
    /// </summary>
    private static void MakeValidEverettSubFolderIdentifier(StringBuilder builder, string subName)
    {
        if (string.IsNullOrEmpty(subName)) { return; }

        // the first character has stronger restrictions than the rest
        if (IsValidEverettIdFirstChar(subName[0]))
        {
            builder.Append(subName[0]);
        }
        else
        {
            builder.Append('_');
            if (IsValidEverettIdChar(subName[0]))
            {
                // if it is a valid subsequent character, prepend an underscore to it
                builder.Append(subName[0]);
            }
        }

        // process the rest of the subname
        for (var i = 1; i < subName.Length; i++)
        {
            if (!IsValidEverettIdChar(subName[i]))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(subName[i]);
            }
        }
    }

    /// <summary>
    /// Make a folder name into an Everett-compatible identifier
    /// </summary>
    private static void MakeValidEverettFolderIdentifier(StringBuilder builder, string name)
    {
        if (string.IsNullOrEmpty(name)) { return; }

        // store the original length for use later
        var length = builder.Length;

        // split folder name into subnames separated by '.', if any
        var subNames = name.Split('.');

        // convert each subname separately
        MakeValidEverettSubFolderIdentifier(builder, subNames[0]);

        for (var i = 1; i < subNames.Length; i++)
        {
            builder.Append('.');
            MakeValidEverettSubFolderIdentifier(builder, subNames[i]);
        }

        // folder name cannot be a single underscore - add another underscore to it
        if ((builder.Length - length) == 1 && builder[length] == '_')
        {
            builder.Append('_');
        }
    }

    /// <summary>
    /// This method is provided for compatibility with Everett which used to convert parts of resource names into
    /// valid identifiers
    /// </summary>
    private static string? MakeValidEverettIdentifier(string? name)
    {
        if (string.IsNullOrEmpty(name)) { return name; }

        var everettId = new StringBuilder(name.Length);

        // split the name into folder names
        var subNames = name.Split(new[] { '/', '\\' });

        // convert every folder name
        MakeValidEverettFolderIdentifier(everettId, subNames[0]);

        for (var i = 1; i < subNames.Length; i++)
        {
            everettId.Append('.');
            MakeValidEverettFolderIdentifier(everettId, subNames[i]);
        }

        return everettId.ToString();
    }

    #endregion

    private bool _disposed;
    private readonly IDisposable _modulesLoadedSubscription;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _modulesLoadedSubscription.Dispose();
    }
}