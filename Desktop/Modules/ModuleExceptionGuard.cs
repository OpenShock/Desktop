using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Threading;
using OpenShock.Desktop.ModuleManager;

namespace OpenShock.Desktop.Modules;

/// <summary>
/// Keeps an exception thrown by a module's native page from taking the app down.
/// </summary>
/// <remarks>
/// A Blazor page had an error boundary around it. A native page runs on the app's own UI thread, where an unhandled
/// exception ends the process. This catches those that came out of module code (judged by where they were thrown),
/// marks them handled and tells the module's page, which closes its view. Exceptions from the app itself are left
/// alone, so our own bugs still surface.
/// </remarks>
public sealed class ModuleExceptionGuard : IDisposable
{
    private readonly ModuleManager.ModuleManager _moduleManager;
    private readonly ILogger<ModuleExceptionGuard> _logger;

    public ModuleExceptionGuard(ModuleManager.ModuleManager moduleManager, ILogger<ModuleExceptionGuard> logger)
    {
        _moduleManager = moduleManager;
        _logger = logger;
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
    }

    /// <summary>Raised on the UI thread when a module threw there and the exception was contained.</summary>
    public event Action<LoadedModule, Exception>? ModuleFaulted;

    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (FindModule(e.Exception) is not { } module) return;

        e.Handled = true;
        _logger.LogError(e.Exception, "Module {ModuleId} threw on the UI thread, its page was closed", module.Id);

        try
        {
            ModuleFaulted?.Invoke(module, e.Exception);
        }
        catch (Exception handlerException)
        {
            _logger.LogError(handlerException, "Failed to close the page of module {ModuleId}", module.Id);
        }
    }

    /// <summary>The module whose code threw <paramref name="exception"/>, if any of it is a module's.</summary>
    public LoadedModule? FindModule(Exception exception)
    {
        var modules = _moduleManager.Modules.Values.ToArray();
        if (modules.Length == 0) return null;

        foreach (var current in Flatten(exception))
        {
            if (current.TargetSite?.DeclaringType?.Assembly is { } thrower && Owner(thrower, modules) is { } owner)
                return owner;

            foreach (var frame in new StackTrace(current, false).GetFrames())
                if (frame.GetMethod()?.DeclaringType?.Assembly is { } assembly && Owner(assembly, modules) is { } module)
                    return module;
        }

        return null;
    }

    private static LoadedModule? Owner(Assembly assembly, LoadedModule[] modules)
    {
        // The module's own assembly and everything it brought along in libs/ share its load context.
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        return modules.FirstOrDefault(x => x.Assembly == assembly || ReferenceEquals(x.LoadContext, context));
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        yield return exception;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            foreach (var nested in Flatten(inner))
                yield return nested;
        }
        else if (exception.InnerException is { } inner)
        {
            foreach (var nested in Flatten(inner)) yield return nested;
        }
    }

    public void Dispose() => Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
}
