# WavyBackground Blazor Component — Design Spec

## Overview

A reusable Blazor component that recreates the [Aceternity UI Wavy Background](https://21st.dev/community/components/aceternity/wavy-background/default) effect. The component renders animated, noise-driven wave patterns on an HTML5 Canvas and supports overlaid content via parameters or custom Razor markup. It works as both a full-page background and a section/hero background.

## Constraints

- **Platform**: Blazor WebAssembly, .NET 10 LTS
- **No external dependencies**: Simplex noise is inlined in the JS module (~80 lines)
- **Zero JS knowledge required by consumers**: All interaction is through Blazor `[Parameter]` properties

## Project Structure

Razor Class Library + Demo App in a single solution.

```
HeroWave.sln
├── src/HeroWave/                          # Razor Class Library
│   ├── HeroWave.csproj
│   ├── Components/
│   │   ├── WavyBackground.razor           # Markup (canvas + overlay)
│   │   ├── WavyBackground.razor.cs        # Parameters, JS interop, lifecycle
│   │   └── WavyBackground.razor.css       # Scoped styles (container, overlay)
│   └── wwwroot/
│       └── wavy-background.js             # Canvas engine + simplex noise
│
└── demo/HeroWave.Demo/                    # Blazor WASM App
    ├── HeroWave.Demo.csproj               # References HeroWave
    ├── Pages/
    │   ├── Home.razor                     # Hero section demo
    │   └── FullPage.razor                 # Full-page background demo
    └── wwwroot/
        └── index.html
```

## Component API

### Usage

**Hero section with built-in helpers:**
```razor
<WavyBackground Title="Build Amazing Apps"
                Subtitle="Powered by Blazor"
                Height="60vh">
    <button class="cta">Get Started</button>
</WavyBackground>
```

**Full-page with custom content:**
```razor
<WavyBackground Height="100vh">
    <div class="my-layout">
        <nav>...</nav>
        <main>...</main>
    </div>
</WavyBackground>
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | `string?` | null | Large heading text centered over the waves |
| `Subtitle` | `string?` | null | Smaller text below the title |
| `ChildContent` | `RenderFragment?` | null | Custom Razor markup rendered below title/subtitle |
| `Height` | `string` | "100vh" | CSS height of the container (e.g., "60vh", "400px") |
| `Colors` | `string[]` | `["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"]` | Hex color array for wave lines |
| `BackgroundColor` | `string` | "#0c0c14" | Background fill color behind the waves |
| `WaveCount` | `int` | 5 | Number of wave layers |
| `WaveWidth` | `int` | 50 | Stroke width of each wave line (px) |
| `Blur` | `int` | 10 | Gaussian blur radius (px) |
| `Speed` | `string` | "slow" | Animation speed — "slow" (0.001) or "fast" (0.002) |
| `Opacity` | `double` | 0.5 | Wave opacity (0.0 – 1.0) |
| `CssClass` | `string?` | null | Additional CSS class on the overlay container |

## Architecture

### Data Flow

1. Blazor renders `<WavyBackground>` → outputs a `<div>` containing a `<canvas>` and an overlay `<div>`
2. `OnAfterRenderAsync` (first render only) → imports JS module via `IJSRuntime`
3. JS `init(canvasElement, config)` receives the canvas element reference and a config object built from the Blazor parameters
4. JS creates a simplex noise instance, sets up the canvas context, attaches a resize listener, and starts a `requestAnimationFrame` loop
5. Blazor content (`ChildContent`, `Title`, `Subtitle`) renders in the overlay div positioned absolutely on top of the canvas
6. On dispose → Blazor calls JS `dispose(instanceId)` which stops the animation, clears the canvas, and removes the resize listener

### HTML Structure (rendered output)

```html
<div class="wavy-background-container" style="height: {Height}">
    <canvas class="wavy-background-canvas"></canvas>
    <div class="wavy-background-overlay {CssClass}">
        <!-- Title if set -->
        <h1>...</h1>
        <!-- Subtitle if set -->
        <p>...</p>
        <!-- ChildContent -->
    </div>
</div>
```

### Scoped CSS (WavyBackground.razor.css)

- `.wavy-background-container`: `position: relative; overflow: hidden; width: 100%;`
- `.wavy-background-canvas`: `position: absolute; inset: 0; width: 100%; height: 100%;`
- `.wavy-background-overlay`: `position: relative; z-index: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%;`
- Title: large white text, bold
- Subtitle: smaller, muted text

## JavaScript Module (wavy-background.js)

### Exported Functions

**`init(canvasElement, config) → string`**
- Receives canvas element reference and config object
- Creates simplex noise instance
- Sets up canvas 2D context (size, blur filter, globalCompositeOperation)
- Attaches window resize listener to update canvas dimensions
- Starts `requestAnimationFrame` loop
- Returns a unique instance ID string for cleanup

**`dispose(instanceId) → void`**
- Cancels the animation frame via `cancelAnimationFrame`
- Clears the canvas
- Removes the resize event listener
- Deletes internal references for the instance

### Animation Loop (per frame)

1. Clear canvas and fill with `backgroundColor`
2. Set `globalAlpha` to `opacity`
3. Set CSS `filter` to `blur({blur}px)`
4. For each wave `i` (0 to `waveCount - 1`):
   - Set `strokeStyle` to `colors[i % colors.length]`
   - Set `lineWidth` to `waveWidth`
   - Begin path at x = 0
   - For x across canvas width (step ~5px):
     - `y = noise(x / 800, 0.3 * i, time) * 100 + (canvasHeight * 0.5)`
   - Stroke the path
5. Increment `time` by speed factor (0.001 for slow, 0.002 for fast)
6. Call `requestAnimationFrame` for next frame

### Simplex Noise

A lightweight 3D simplex noise implementation (~80 lines) is inlined directly in the module. No external npm packages or CDN dependencies. This matches the approach used by the original Aceternity component.

### Instance Management

The module maintains a `Map<string, instanceState>` internally to support multiple `<WavyBackground>` components on the same page. Each `init` call generates a unique ID and stores the animation frame handle, resize listener, and canvas reference. `dispose` cleans up by ID.

## Blazor Component Lifecycle (WavyBackground.razor.cs)

```
Implements: IAsyncDisposable

Fields:
  ElementReference _canvas
  IJSObjectReference? _module
  string? _instanceId

OnAfterRenderAsync(firstRender):
  if (!firstRender) return
  _module = import("./_content/HeroWave/wavy-background.js")
  _instanceId = _module.invoke("init", _canvas, BuildConfig())

DisposeAsync():
  if (_module != null && _instanceId != null)
      _module.invoke("dispose", _instanceId)
  if (_module != null)
      _module.DisposeAsync()
```

The JS module is loaded via the RCL static asset path convention (`_content/{LibraryName}/`), which Blazor resolves automatically when the demo app references the library project.

## Demo Pages

### Home.razor — Hero Section Demo
- WavyBackground at 60vh height with Title, Subtitle, and a CTA button
- Additional page content below the hero section

### FullPage.razor — Full Page Background Demo
- WavyBackground at 100vh with custom layout content passed via ChildContent
- Demonstrates the component working as a full-page backdrop

## Success Criteria

1. Component renders animated waves matching the visual style of the Aceternity original (colors, blur, noise-driven motion)
2. All 12 parameters work and the defaults produce the original look
3. Component works as both a hero section (partial height) and full-page background
4. Canvas resizes correctly on window resize
5. No memory leaks — animation and listeners are cleaned up on dispose
6. Multiple instances on the same page work independently
7. Zero external JS dependencies — fully self-contained RCL
