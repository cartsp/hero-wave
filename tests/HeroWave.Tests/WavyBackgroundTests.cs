using Bunit;
using Bunit.JSInterop;
using HeroWave.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Xunit;

namespace HeroWave.Tests;

public class WavyBackgroundTests : BunitContext
{
    private readonly BunitJSModuleInterop _moduleInterop;

    public WavyBackgroundTests()
    {
        // Set up JSInterop to handle the module import and init/dispose calls
        _moduleInterop = JSInterop.SetupModule("./_content/HeroWave/wavy-background.js");
        _moduleInterop.Setup<string>("init", _ => true).SetResult("test-instance-0");
        _moduleInterop.SetupVoid("dispose", _ => true);
        _moduleInterop.SetupVoid("update", _ => true);
    }

    [Fact]
    public void Renders_Container_With_Default_Height()
    {
        var cut = Render<WavyBackground>();
        var container = cut.Find(".wavy-background-container");
        Assert.Contains("height: 100vh", container.GetAttribute("style"));
    }

    [Fact]
    public void Renders_Container_With_Custom_Height()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.Height, "60vh"));
        var container = cut.Find(".wavy-background-container");
        Assert.Contains("height: 60vh", container.GetAttribute("style"));
    }

    [Fact]
    public void Renders_Canvas_Element()
    {
        var cut = Render<WavyBackground>();
        var canvas = cut.Find("canvas.wavy-background-canvas");
        Assert.NotNull(canvas);
    }

    [Fact]
    public void Renders_Title_When_Set()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.Title, "Hello World"));
        var title = cut.Find("h1.wavy-background-title");
        Assert.Equal("Hello World", title.TextContent);
    }

    [Fact]
    public void Omits_Title_When_Null()
    {
        var cut = Render<WavyBackground>();
        Assert.Empty(cut.FindAll("h1.wavy-background-title"));
    }

    [Fact]
    public void Renders_Subtitle_When_Set()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.Subtitle, "Sub text"));
        var subtitle = cut.Find("p.wavy-background-subtitle");
        Assert.Equal("Sub text", subtitle.TextContent);
    }

    [Fact]
    public void Omits_Subtitle_When_Null()
    {
        var cut = Render<WavyBackground>();
        Assert.Empty(cut.FindAll("p.wavy-background-subtitle"));
    }

    [Fact]
    public void Renders_ChildContent()
    {
        var cut = Render<WavyBackground>(p =>
            p.AddChildContent("<button>Click me</button>"));
        var button = cut.Find("button");
        Assert.Equal("Click me", button.TextContent);
    }

    [Fact]
    public void Applies_CssClass_To_Overlay()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.CssClass, "my-custom"));
        var overlay = cut.Find(".wavy-background-overlay");
        Assert.Contains("my-custom", overlay.GetAttribute("class"));
    }

    [Fact]
    public void Calls_JsInterop_On_FirstRender()
    {
        Render<WavyBackground>();

        var importInvocation = JSInterop.Invocations["import"];
        Assert.Single(importInvocation);
        Assert.Equal("./_content/HeroWave/wavy-background.js", importInvocation[0].Arguments[0]);
    }

    [Fact]
    public void Passes_Config_To_JsInit()
    {
        var colors = new[] { "#ff0000", "#00ff00" };
        Render<WavyBackground>(p => p
            .Add(x => x.Colors, colors)
            .Add(x => x.BackgroundColor, "#111111")
            .Add(x => x.WaveCount, 3)
            .Add(x => x.WaveWidth, 40)
            .Add(x => x.Speed, 0.008)
            .Add(x => x.Opacity, 0.8));

        var initInvocations = _moduleInterop.Invocations["init"];
        Assert.Single(initInvocations);
    }

    [Fact]
    public async Task Calls_Dispose_On_Cleanup()
    {
        Render<WavyBackground>();
        await DisposeComponentsAsync();

        var disposeInvocations = _moduleInterop.Invocations["dispose"];
        Assert.Single(disposeInvocations);
        Assert.Equal("test-instance-0", disposeInvocations[0].Arguments[0]);
    }

    [Fact]
    public void Renders_Title_And_Subtitle_Together()
    {
        var cut = Render<WavyBackground>(p => p
            .Add(x => x.Title, "Main Title")
            .Add(x => x.Subtitle, "Sub Title"));

        Assert.Equal("Main Title", cut.Find("h1.wavy-background-title").TextContent);
        Assert.Equal("Sub Title", cut.Find("p.wavy-background-subtitle").TextContent);
    }

    [Fact]
    public async Task Handles_JSDisconnectedException_On_Dispose()
    {
        // Set up module where dispose throws JSDisconnectedException
        JSInterop.Mode = JSRuntimeMode.Strict;
        var moduleInterop = JSInterop.SetupModule("./_content/HeroWave/wavy-background.js");
        moduleInterop.Setup<string>("init", _ => true).SetResult("test-instance-err");
        moduleInterop.SetupVoid("dispose", _ => true)
            .SetException(new JSDisconnectedException("Circuit disconnected"));

        Render<WavyBackground>();

        // Should not throw
        await DisposeComponentsAsync();
    }

    [Fact]
    public void Default_TargetFps_Is_60()
    {
        var cut = Render<WavyBackground>();
        var component = cut.Instance;
        Assert.Equal(60, component.TargetFps);
    }

    [Fact]
    public void TargetFps_Is_Passed_In_JsConfig()
    {
        Render<WavyBackground>(p => p.Add(x => x.TargetFps, 30));

        var initInvocations = _moduleInterop.Invocations["init"];
        Assert.Single(initInvocations);
        Assert.Equal(2, initInvocations[0].Arguments.Count);

        var configType = initInvocations[0].Arguments[1]!.GetType();
        var targetFpsProp = configType.GetProperty("targetFps");
        Assert.NotNull(targetFpsProp);
        Assert.Equal(30, targetFpsProp!.GetValue(initInvocations[0].Arguments[1]));
    }

    [Fact]
    public void TargetFps_Is_Clamped_To_Minimum_1()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.TargetFps, 0));
        Assert.Equal(1, cut.Instance.TargetFps);
    }

    [Fact]
    public void TargetFps_Is_Clamped_To_Maximum_120()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.TargetFps, 999));
        Assert.Equal(120, cut.Instance.TargetFps);
    }

    [Fact]
    public void TargetFps_Negative_Is_Clamped_To_1()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.TargetFps, -10));
        Assert.Equal(1, cut.Instance.TargetFps);
    }

    // --- Reduced-motion and ARIA accessibility tests ---

    [Fact]
    public void Default_ReducedMotion_Is_RespectSystemPreference()
    {
        var cut = Render<WavyBackground>();
        Assert.Equal(ReducedMotionBehavior.RespectSystemPreference, cut.Instance.ReducedMotion);
    }

    [Fact]
    public void Canvas_Has_AriaHidden_Attribute()
    {
        var cut = Render<WavyBackground>();
        var canvas = cut.Find("canvas.wavy-background-canvas");
        Assert.Equal("true", canvas.GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Container_Does_Not_Have_Role_Attribute()
    {
        var cut = Render<WavyBackground>();
        var container = cut.Find(".wavy-background-container");
        Assert.Null(container.GetAttribute("role"));
    }

    /// <summary>
    /// Extracts a named property from the anonymous config object passed to JS init.
    /// Centralizes the reflection so config property tests have a single fragility point.
    /// </summary>
    private static string GetConfigProperty(object config, string propertyName)
    {
        var prop = config.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on config object");
        return (string)prop.GetValue(config)!;
    }

    [Fact]
    public void ReducedMotion_AlwaysStatic_Maps_In_Config()
    {
        Render<WavyBackground>(p => p
            .Add(x => x.ReducedMotion, ReducedMotionBehavior.AlwaysStatic));

        var config = _moduleInterop.Invocations["init"][0].Arguments[1]!;
        Assert.Equal("alwaysStatic", GetConfigProperty(config, "reducedMotion"));
    }

    [Fact]
    public void ReducedMotion_AlwaysAnimate_Maps_In_Config()
    {
        Render<WavyBackground>(p => p
            .Add(x => x.ReducedMotion, ReducedMotionBehavior.AlwaysAnimate));

        var config = _moduleInterop.Invocations["init"][0].Arguments[1]!;
        Assert.Equal("alwaysAnimate", GetConfigProperty(config, "reducedMotion"));
    }

    [Fact]
    public void ReducedMotion_RespectSystemPreference_Maps_In_Config()
    {
        Render<WavyBackground>(p => p
            .Add(x => x.ReducedMotion, ReducedMotionBehavior.RespectSystemPreference));

        var config = _moduleInterop.Invocations["init"][0].Arguments[1]!;
        Assert.Equal("respectSystemPreference", GetConfigProperty(config, "reducedMotion"));
    }

    /// <summary>
    /// Host component that renders WavyBackground and allows re-rendering
    /// with different parameters via StateHasChanged().
    /// Required because bunit v2's BunitContext.Render&lt;T&gt;() creates new instances
    /// each call, preventing OnParametersSetAsync testing on initialised components.
    /// </summary>
    private class WavyBackgroundHost : ComponentBase
    {
        internal double Speed { get; private set; } = 0.004;
        internal string? Title { get; private set; }
        internal ReducedMotionBehavior ReducedMotion { get; private set; } = ReducedMotionBehavior.RespectSystemPreference;

        public void ChangeSpeed(double speed) { Speed = speed; StateHasChanged(); }
        public void ChangeTitle(string? title) { Title = title; StateHasChanged(); }
        public void ChangeReducedMotion(ReducedMotionBehavior mode) { ReducedMotion = mode; StateHasChanged(); }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<WavyBackground>(0);
            builder.AddComponentParameter(1, nameof(WavyBackground.Speed), Speed);
            builder.AddComponentParameter(2, nameof(WavyBackground.ReducedMotion), ReducedMotion);
            if (Title is not null)
                builder.AddComponentParameter(3, nameof(WavyBackground.Title), Title);
            builder.CloseComponent();
        }
    }

    [Fact]
    public void OnParametersSetAsync_CallsUpdate_WhenReducedMotionChanges()
    {
        var host = Render<WavyBackgroundHost>();

        host.InvokeAsync(() => host.Instance.ChangeReducedMotion(ReducedMotionBehavior.AlwaysStatic));

        var updateInvocations = _moduleInterop.Invocations["update"];
        Assert.Single(updateInvocations);

        var configArg = updateInvocations[0].Arguments[1];
        var prop = configArg.GetType().GetProperty("reducedMotion");
        Assert.Equal("alwaysStatic", prop!.GetValue(configArg));
    }

    [Fact]
    public void OnParametersSetAsync_CallsUpdate_WhenConfigChanges()
    {
        // Render host which renders WavyBackground internally
        var host = Render<WavyBackgroundHost>();

        // Trigger re-render of the same WavyBackground instance with changed Speed
        host.InvokeAsync(() => host.Instance.ChangeSpeed(0.01));

        // update() should have been called because Speed changed
        var updateInvocations = _moduleInterop.Invocations["update"];
        Assert.Single(updateInvocations);
    }

    [Fact]
    public void OnParametersSetAsync_SkipsUpdate_WhenConfigUnchanged()
    {
        // Render host which renders WavyBackground internally
        var host = Render<WavyBackgroundHost>();

        // Trigger re-render with a non-config parameter change (Title)
        host.InvokeAsync(() => host.Instance.ChangeTitle("New Title"));

        // update() should NOT have been called since config values didn't change
        var updateInvocations = _moduleInterop.Invocations["update"];
        Assert.Empty(updateInvocations);
    }

    [Fact]
    public async Task OnParametersSetAsync_Handles_JSDisconnectedException()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var strictModule = JSInterop.SetupModule("./_content/HeroWave/wavy-background.js");
        strictModule.Setup<string>("init", _ => true).SetResult("test-instance-update-err");
        strictModule.SetupVoid("update", _ => true)
            .SetException(new JSDisconnectedException("Circuit disconnected"));

        // Render host which renders WavyBackground internally
        var host = Render<WavyBackgroundHost>();

        // Trigger re-render with changed config — update() throws JSDisconnectedException
        // which should be caught silently
        await host.InvokeAsync(() => host.Instance.ChangeSpeed(0.01));

        await DisposeComponentsAsync();
    }
}
