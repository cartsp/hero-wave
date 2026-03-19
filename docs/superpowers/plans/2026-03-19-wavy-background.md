# WavyBackground Blazor Component — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable Blazor WASM component that renders animated, noise-driven wave backgrounds with customizable parameters, packaged as a Razor Class Library.

**Architecture:** Two-project solution — a Razor Class Library (`HeroWave`) containing the `WavyBackground` component with bundled JS interop, and a Blazor WASM demo app (`HeroWave.Demo`) that references it. The canvas animation runs in a JS ES module; Blazor manages lifecycle and exposes parameters.

**Tech Stack:** .NET 10 LTS, Blazor WebAssembly, Razor Class Library, HTML5 Canvas, JavaScript ES modules, Simplex noise (inlined)

**Spec:** `docs/superpowers/specs/2026-03-19-wavy-background-design.md`

---

## File Structure

```
HeroWave.sln
├── src/HeroWave/
│   ├── HeroWave.csproj                    # RCL targeting net10.0
│   ├── Components/
│   │   ├── WavyBackground.razor           # Component markup
│   │   ├── WavyBackground.razor.cs        # Code-behind: parameters, JS interop, IAsyncDisposable
│   │   └── WavyBackground.razor.css       # Scoped styles
│   └── wwwroot/
│       └── wavy-background.js             # Canvas engine: simplex noise, animation loop, init/dispose
│
└── demo/HeroWave.Demo/
    ├── HeroWave.Demo.csproj               # Blazor WASM app referencing HeroWave
    ├── Program.cs                          # WASM host bootstrap
    ├── _Imports.razor                      # Global usings including HeroWave.Components
    ├── App.razor                           # Router
    ├── Layout/
    │   ├── MainLayout.razor               # Main layout
    │   └── MainLayout.razor.css           # Layout styles
    ├── Pages/
    │   ├── Home.razor                     # Hero section demo
    │   └── FullPage.razor                 # Full-page background demo
    └── wwwroot/
        ├── index.html                     # WASM entry point
        └── css/
            └── app.css                    # Global styles (reset, body background)
```

---

## Task 1: Scaffold the Solution and Projects

**Files:**
- Create: `HeroWave.sln`
- Create: `src/HeroWave/HeroWave.csproj`
- Create: `demo/HeroWave.Demo/HeroWave.Demo.csproj`
- Create: `demo/HeroWave.Demo/Program.cs`
- Create: `demo/HeroWave.Demo/App.razor`
- Create: `demo/HeroWave.Demo/_Imports.razor`
- Create: `demo/HeroWave.Demo/wwwroot/index.html`

- [ ] **Step 1: Create the solution file**

```bash
cd C:/Users/nienetworks/code/hero-wave
dotnet new sln --name HeroWave
```

- [ ] **Step 2: Create the Razor Class Library project**

```bash
dotnet new razorclasslib -o src/HeroWave --name HeroWave
```

Then clean up the template-generated files we don't need:

```bash
rm -f src/HeroWave/Component1.razor
rm -f src/HeroWave/ExampleJsInterop.cs
rm -rf src/HeroWave/wwwroot/*
```

- [ ] **Step 3: Edit HeroWave.csproj to target net10.0**

Verify `src/HeroWave/HeroWave.csproj` contains:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

Remove any `SupportedPlatform`, `PackageReference` to `Microsoft.AspNetCore.Components.Web`, or other template bloat if present. The Razor SDK includes Components.Web transitively for RCLs.

- [ ] **Step 4: Create the Blazor WASM demo app**

```bash
dotnet new blazorwasm -o demo/HeroWave.Demo --name HeroWave.Demo
```

Then clean up template boilerplate pages we won't need:

```bash
rm -f demo/HeroWave.Demo/Pages/Counter.razor
rm -f demo/HeroWave.Demo/Pages/Weather.razor
rm -f demo/HeroWave.Demo/Layout/NavMenu.razor
rm -f demo/HeroWave.Demo/Layout/NavMenu.razor.css
```

- [ ] **Step 5: Add project reference from demo to library**

```bash
dotnet add demo/HeroWave.Demo/HeroWave.Demo.csproj reference src/HeroWave/HeroWave.csproj
```

- [ ] **Step 6: Add both projects to the solution**

```bash
dotnet sln HeroWave.sln add src/HeroWave/HeroWave.csproj
dotnet sln HeroWave.sln add demo/HeroWave.Demo/HeroWave.Demo.csproj
```

- [ ] **Step 7: Add HeroWave.Components to demo _Imports.razor**

Open `demo/HeroWave.Demo/_Imports.razor` and add at the bottom:

```razor
@using HeroWave.Components
```

- [ ] **Step 8: Build and verify**

```bash
dotnet build HeroWave.sln
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 9: Commit**

```bash
git init
git add -A
git commit -m "chore: scaffold HeroWave solution with RCL and demo WASM app"
```

---

## Task 2: Implement the JavaScript Wave Engine

**Files:**
- Create: `src/HeroWave/wwwroot/wavy-background.js`

- [ ] **Step 1: Create the JS module with simplex noise**

Write `src/HeroWave/wwwroot/wavy-background.js` with the following structure:

```javascript
// Simplex noise implementation (3D)
// Based on Stefan Gustavson's implementation
const F3 = 1.0 / 3.0;
const G3 = 1.0 / 6.0;

const grad3 = [
    [1,1,0],[-1,1,0],[1,-1,0],[-1,-1,0],
    [1,0,1],[-1,0,1],[1,0,-1],[-1,0,-1],
    [0,1,1],[0,-1,1],[0,1,-1],[0,-1,-1]
];

function createNoise() {
    const perm = new Uint8Array(512);
    const p = new Uint8Array(256);
    for (let i = 0; i < 256; i++) p[i] = i;
    // Fisher-Yates shuffle
    for (let i = 255; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [p[i], p[j]] = [p[j], p[i]];
    }
    for (let i = 0; i < 512; i++) perm[i] = p[i & 255];

    return function noise3D(x, y, z) {
        const s = (x + y + z) * F3;
        const i = Math.floor(x + s);
        const j = Math.floor(y + s);
        const k = Math.floor(z + s);
        const t = (i + j + k) * G3;

        const X0 = i - t, Y0 = j - t, Z0 = k - t;
        const x0 = x - X0, y0 = y - Y0, z0 = z - Z0;

        let i1, j1, k1, i2, j2, k2;
        if (x0 >= y0) {
            if (y0 >= z0) { i1=1;j1=0;k1=0;i2=1;j2=1;k2=0; }
            else if (x0 >= z0) { i1=1;j1=0;k1=0;i2=1;j2=0;k2=1; }
            else { i1=0;j1=0;k1=1;i2=1;j2=0;k2=1; }
        } else {
            if (y0 < z0) { i1=0;j1=0;k1=1;i2=0;j2=1;k2=1; }
            else if (x0 < z0) { i1=0;j1=1;k1=0;i2=0;j2=1;k2=1; }
            else { i1=0;j1=1;k1=0;i2=1;j2=1;k2=0; }
        }

        const x1 = x0-i1+G3, y1 = y0-j1+G3, z1 = z0-k1+G3;
        const x2 = x0-i2+2*G3, y2 = y0-j2+2*G3, z2 = z0-k2+2*G3;
        const x3 = x0-1+3*G3, y3 = y0-1+3*G3, z3 = z0-1+3*G3;

        const ii = i & 255, jj = j & 255, kk = k & 255;

        function dot(g, x, y, z) { return g[0]*x + g[1]*y + g[2]*z; }
        function contrib(g, x, y, z) {
            const t = 0.6 - x*x - y*y - z*z;
            return t < 0 ? 0 : t * t * t * t * dot(g, x, y, z);
        }

        const gi0 = perm[ii + perm[jj + perm[kk]]] % 12;
        const gi1 = perm[ii+i1 + perm[jj+j1 + perm[kk+k1]]] % 12;
        const gi2 = perm[ii+i2 + perm[jj+j2 + perm[kk+k2]]] % 12;
        const gi3 = perm[ii+1 + perm[jj+1 + perm[kk+1]]] % 12;

        return 32 * (
            contrib(grad3[gi0], x0, y0, z0) +
            contrib(grad3[gi1], x1, y1, z1) +
            contrib(grad3[gi2], x2, y2, z2) +
            contrib(grad3[gi3], x3, y3, z3)
        );
    };
}

// Instance management
const instances = new Map();
let nextId = 0;

export function init(canvas, config) {
    const id = String(nextId++);
    const ctx = canvas.getContext("2d");
    const noise = createNoise();
    let nt = 0;
    let animationFrameId = null;

    const speedFactor = config.speed === "fast" ? 0.002 : 0.001;
    let running = true;

    function resize() {
        canvas.width = canvas.offsetWidth;
        canvas.height = canvas.offsetHeight;
    }

    function draw() {
        if (!running) return;
        const w = canvas.width;
        const h = canvas.height;

        ctx.clearRect(0, 0, w, h);
        ctx.fillStyle = config.backgroundColor;
        ctx.fillRect(0, 0, w, h);
        ctx.globalAlpha = config.opacity;
        ctx.filter = `blur(${config.blur}px)`;

        for (let i = 0; i < config.waveCount; i++) {
            ctx.beginPath();
            ctx.strokeStyle = config.colors[i % config.colors.length];
            ctx.lineWidth = config.waveWidth;

            for (let x = 0; x < w; x += 5) {
                const y = noise(x / 800, 0.3 * i, nt) * 100 + h * 0.5;
                if (x === 0) {
                    ctx.moveTo(x, y);
                } else {
                    ctx.lineTo(x, y);
                }
            }
            ctx.stroke();
            ctx.closePath();
        }

        ctx.globalAlpha = 1;
        ctx.filter = "none";
        nt += speedFactor;
        animationFrameId = requestAnimationFrame(draw);
    }

    resize();
    window.addEventListener("resize", resize);
    animationFrameId = requestAnimationFrame(draw);

    instances.set(id, { animationFrameId, resize, canvas, stop: () => { running = false; } });
    return id;
}

export function dispose(id) {
    const instance = instances.get(id);
    if (!instance) return;
    instance.stop();
    cancelAnimationFrame(instance.animationFrameId);
    window.removeEventListener("resize", instance.resize);
    const ctx = instance.canvas.getContext("2d");
    if (ctx) ctx.clearRect(0, 0, instance.canvas.width, instance.canvas.height);
    instances.delete(id);
}
```

- [ ] **Step 2: Verify the file was created correctly**

```bash
ls -la src/HeroWave/wwwroot/wavy-background.js
```

Expected: File exists with reasonable size (~3-4KB).

- [ ] **Step 3: Build to verify no project issues**

```bash
dotnet build src/HeroWave/HeroWave.csproj
```

Expected: Build succeeded (JS files are static assets, not compiled, but this verifies the project is still healthy).

- [ ] **Step 4: Commit**

```bash
git add src/HeroWave/wwwroot/wavy-background.js
git commit -m "feat: add JS wave engine with simplex noise and instance management"
```

---

## Task 3: Implement the WavyBackground Blazor Component

**Files:**
- Create: `src/HeroWave/Components/WavyBackground.razor`
- Create: `src/HeroWave/Components/WavyBackground.razor.cs`
- Create: `src/HeroWave/Components/WavyBackground.razor.css`

- [ ] **Step 1: Create the component markup**

Write `src/HeroWave/Components/WavyBackground.razor`:

```razor
<div class="wavy-background-container" style="height: @Height">
    <canvas @ref="_canvas" class="wavy-background-canvas"></canvas>
    <div class="wavy-background-overlay @CssClass">
        @if (!string.IsNullOrEmpty(Title))
        {
            <h1 class="wavy-background-title">@Title</h1>
        }
        @if (!string.IsNullOrEmpty(Subtitle))
        {
            <p class="wavy-background-subtitle">@Subtitle</p>
        }
        @ChildContent
    </div>
</div>
```

- [ ] **Step 2: Create the code-behind**

Write `src/HeroWave/Components/WavyBackground.razor.cs`:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HeroWave.Components;

public partial class WavyBackground : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Subtitle { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Height { get; set; } = "100vh";

    [Parameter] public string[] Colors { get; set; } =
        ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"];

    [Parameter] public string BackgroundColor { get; set; } = "#0c0c14";
    [Parameter] public int WaveCount { get; set; } = 5;
    [Parameter] public int WaveWidth { get; set; } = 50;
    [Parameter] public int Blur { get; set; } = 10;
    [Parameter] public string Speed { get; set; } = "slow";
    [Parameter] public double Opacity { get; set; } = 0.5;
    [Parameter] public string? CssClass { get; set; }

    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private string? _instanceId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/HeroWave/wavy-background.js");

        var config = new
        {
            colors = Colors,
            backgroundColor = BackgroundColor,
            waveCount = WaveCount,
            waveWidth = WaveWidth,
            blur = Blur,
            speed = Speed,
            opacity = Opacity
        };

        _instanceId = await _module.InvokeAsync<string>("init", _canvas, config);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null && _instanceId is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _instanceId);
            }
            catch (JSDisconnectedException)
            {
                // Circuit may already be gone during app shutdown
            }
        }

        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
```

- [ ] **Step 3: Create the scoped CSS**

Write `src/HeroWave/Components/WavyBackground.razor.css`:

```css
.wavy-background-container {
    position: relative;
    overflow: hidden;
    width: 100%;
}

.wavy-background-canvas {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
}

.wavy-background-overlay {
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    height: 100%;
}

.wavy-background-title {
    font-size: 3rem;
    font-weight: 700;
    color: white;
    margin: 0 0 0.5rem 0;
    text-align: center;
}

.wavy-background-subtitle {
    font-size: 1.25rem;
    color: rgba(255, 255, 255, 0.7);
    margin: 0 0 1.5rem 0;
    text-align: center;
}
```

- [ ] **Step 4: Build the library**

```bash
dotnet build src/HeroWave/HeroWave.csproj
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/HeroWave/Components/
git commit -m "feat: add WavyBackground Blazor component with JS interop and scoped styles"
```

---

## Task 4: Build the Demo App Pages

**Files:**
- Modify: `demo/HeroWave.Demo/Pages/Home.razor` (template-generated, replace content)
- Create: `demo/HeroWave.Demo/Pages/FullPage.razor`
- Modify: `demo/HeroWave.Demo/Layout/MainLayout.razor` (simplify)
- Modify: `demo/HeroWave.Demo/wwwroot/css/app.css` (add dark body background)

- [ ] **Step 1: Simplify MainLayout.razor**

Replace the content of `demo/HeroWave.Demo/Layout/MainLayout.razor` with:

```razor
@inherits LayoutComponentBase

@Body
```

This removes the default sidebar/nav chrome so the wavy background demos can be full-width.

- [ ] **Step 2: Add dark body background to app.css**

Open `demo/HeroWave.Demo/wwwroot/css/app.css` and add at the top (keep existing content below):

```css
body {
    margin: 0;
    padding: 0;
    background-color: #0c0c14;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}
```

- [ ] **Step 3: Create the hero section demo page**

Replace `demo/HeroWave.Demo/Pages/Home.razor` with:

```razor
@page "/"

<WavyBackground Title="Build Amazing Apps"
                Subtitle="A reusable wavy background component for Blazor"
                Height="60vh">
    <a href="/fullpage"
       style="padding: 0.75rem 2rem; background: white; color: #0c0c14;
              border-radius: 0.5rem; text-decoration: none; font-weight: 600;">
        View Full Page Demo
    </a>
</WavyBackground>

<div style="padding: 4rem 2rem; color: white; text-align: center;">
    <h2 style="margin-bottom: 1rem;">Below the Hero</h2>
    <p style="color: rgba(255,255,255,0.7); max-width: 600px; margin: 0 auto;">
        This content sits below the wavy hero section, demonstrating that
        the component works as a partial-height section background.
    </p>
</div>
```

- [ ] **Step 4: Create the full-page background demo**

Write `demo/HeroWave.Demo/Pages/FullPage.razor`:

```razor
@page "/fullpage"

<WavyBackground Height="100vh"
                Speed="fast"
                Colors="@(new[] { "#22d3ee", "#818cf8", "#e879f9" })"
                WaveCount="7"
                Opacity="0.6">
    <div style="text-align: center; color: white;">
        <h1 style="font-size: 3rem; font-weight: 700; margin-bottom: 0.5rem;">
            Full Page Background
        </h1>
        <p style="font-size: 1.25rem; color: rgba(255,255,255,0.7); margin-bottom: 2rem;">
            Custom colors, more waves, faster animation
        </p>
        <a href="/"
           style="padding: 0.75rem 2rem; background: rgba(255,255,255,0.15);
                  color: white; border-radius: 0.5rem; text-decoration: none;
                  border: 1px solid rgba(255,255,255,0.3);">
            Back to Hero Demo
        </a>
    </div>
</WavyBackground>
```

- [ ] **Step 5: Build the full solution**

```bash
dotnet build HeroWave.sln
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Run the demo app and visually verify**

```bash
dotnet run --project demo/HeroWave.Demo
```

Open the URL shown in the terminal (typically `https://localhost:5001` or `http://localhost:5000`).

**Verify:**
- Home page (`/`): Animated waves at 60vh with title, subtitle, and button. Content visible below.
- Full page (`/fullpage`): Animated waves at 100vh with custom colors (3 colors, 7 waves, fast speed).
- Waves animate smoothly with organic noise-driven motion.
- Resize the browser window — canvas should adapt.

- [ ] **Step 7: Commit**

```bash
git add demo/
git commit -m "feat: add demo app with hero section and full-page background examples"
```

---

## Task 5: Cleanup and Final Verification

**Files:**
- Create: `.gitignore`

- [ ] **Step 1: Add .gitignore**

Write `.gitignore`:

```
bin/
obj/
.vs/
*.user
.superpowers/
```

- [ ] **Step 2: Full clean build**

```bash
dotnet clean HeroWave.sln
dotnet build HeroWave.sln
```

Expected: Build succeeded with 0 errors, 0 warnings (or only framework warnings).

- [ ] **Step 3: Run and perform final visual check**

```bash
dotnet run --project demo/HeroWave.Demo
```

Verify all success criteria from the spec:
1. Waves match the Aceternity visual style (colors, blur, noise motion)
2. All parameters work — compare Home (defaults) vs FullPage (custom overrides)
3. Component works at both 60vh (hero) and 100vh (full page)
4. Canvas resizes on window resize
5. Navigate between pages — no console errors (dispose works)
6. Zero external JS dependencies confirmed (no CDN links, no npm)

- [ ] **Step 4: Commit .gitignore and any final tweaks**

```bash
git add .gitignore
git commit -m "chore: add .gitignore"
```
