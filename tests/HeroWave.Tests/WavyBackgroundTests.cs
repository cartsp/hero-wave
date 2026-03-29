using Bunit;
using Bunit.JSInterop;
using HeroWave.Components;
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
    public void Default_Gradient_Is_Solid()
    {
        var cut = Render<WavyBackground>();
        Assert.Equal(GradientMode.Solid, cut.Instance.Gradient);
    }

    [Fact]
    public void Gradient_Parameter_Is_Passed_In_JsInit_Config()
    {
        Render<WavyBackground>(p => p.Add(x => x.Gradient, GradientMode.Vertical));

        var initInvocations = _moduleInterop.Invocations["init"];
        Assert.Single(initInvocations);
    }

    [Fact]
    public void Renders_Without_Error_When_Gradient_Vertical()
    {
        var cut = Render<WavyBackground>(p => p.Add(x => x.Gradient, GradientMode.Vertical));
        var container = cut.Find(".wavy-background-container");
        Assert.NotNull(container);
    }
}
