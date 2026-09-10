namespace OpenShock.Desktop.Modules.ExampleModule;

/// <summary>
/// Deliberately unconstructable, to reproduce a module component whose injected service blows up.
/// </summary>
/// <remarks>
/// This is what a module built against a newer version of a host-provided assembly does in the
/// wild: the module loads, Setup and Start succeed, and the failure only surfaces when the
/// component activator resolves the service. The exception is shaped like the real one so the
/// substituted <c>ModuleComponentFailed</c> notice renders the same amount of text it would then.
/// </remarks>
public sealed class BrokenService
{
    private const string MissingAssembly =
        "Some.Missing.Library, Version=1.2.0.0, Culture=neutral, PublicKeyToken=null";

    public BrokenService()
    {
        throw new FileNotFoundException(
            $"Could not load file or assembly '{MissingAssembly}'. The system cannot find the file specified.",
            MissingAssembly);
    }

    public string GetBrokenString() => "unreachable";
}
