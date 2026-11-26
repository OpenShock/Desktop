using Microsoft.AspNetCore.Components;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace OpenShock.Desktop.ModuleManager.Tests
{
    /// <summary>
    /// Which module pages go to the webview (Blazor) and which are shown natively (Avalonia). Whatever is neither must
    /// be rejected at load time, not handed to a renderer.
    /// </summary>
    public sealed class ModulePageTypesTests
    {
        [Test]
        public async Task BlazorComponentIsBlazor() =>
            await Assert.That(ModulePageTypes.Classify(typeof(SomeComponent))).IsEqualTo(ModulePageKind.Blazor);

        [Test]
        public async Task AvaloniaControlIsNative() =>
            await Assert.That(ModulePageTypes.Classify(typeof(SomePage))).IsEqualTo(ModulePageKind.Native);

        [Test]
        public async Task IndirectAvaloniaControlIsNative() =>
            await Assert.That(ModulePageTypes.Classify(typeof(SomeDerivedPage))).IsEqualTo(ModulePageKind.Native);

        [Test]
        [Arguments(typeof(string))]
        [Arguments(typeof(AbstractComponent))]
        [Arguments(typeof(IComponent))]
        [Arguments(typeof(AbstractPage))]
        [Arguments(typeof(GenericPage<>))]
        public async Task AnythingElseIsRejected(Type type) =>
            await Assert.That(ModulePageTypes.Classify(type)).IsNull();

        // Public: the test generator refers to argument types from outside this class.
        public sealed class SomeComponent : ComponentBase;

        public abstract class AbstractComponent : ComponentBase;

        public class SomePage : Avalonia.Controls.Control;

        public sealed class SomeDerivedPage : SomePage;

        public abstract class AbstractPage : Avalonia.Controls.Control;

        public sealed class GenericPage<T> : Avalonia.Controls.Control;
    }
}

namespace Avalonia.Controls
{
    /// <summary>
    /// Stands in for Avalonia's control base. The classifier matches it by name, so Core can stay free of Avalonia,
    /// and so can this test.
    /// </summary>
    public class Control;
}
