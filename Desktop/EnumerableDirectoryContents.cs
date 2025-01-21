using System.Collections;
using Microsoft.Extensions.FileProviders;

namespace OpenShock.Desktop;

internal sealed class EnumerableDirectoryContents : IDirectoryContents
{
    private readonly IEnumerable<IFileInfo> _entries;

    public EnumerableDirectoryContents(IEnumerable<IFileInfo> entries)
    {
        _entries = entries;
    }

    public bool Exists => true;

    public IEnumerator<IFileInfo> GetEnumerator()
    {
        return _entries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _entries.GetEnumerator();
    }
}
