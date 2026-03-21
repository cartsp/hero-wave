# 10-Layer Alpha Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current `ctx.filter = "blur()"` Canvas 2D render with 10-layer alpha-composited strokes at 0.25x resolution, and clean up the `Blur` and `Speed` API parameters.

**Architecture:** The canvas renders at 25% of CSS pixel dimensions (cutting pixel count to ~6%), then CSS bilinear upscaling restores full display size. Each wave is drawn 10 times with decreasing width and increasing opacity, compositing a soft glow effect without any `ctx.filter` or `ctx.shadowBlur` calls. The C# component removes the now-meaningless `Blur` parameter and changes `Speed` from a discrete string enum to a continuous `double`.

**Tech Stack:** Canvas 2D API, Blazor WASM RCL (Razor Class Library), bUnit (unit tests), xUnit

---

## File Map

| File | Change |
|------|--------|
| `src/HeroWave/wwwroot/wavy-background.js` | Replace resize logic, draw loop; remove `ctx.filter`; use `config.speed` directly |
| `src/HeroWave/Components/WavyBackground.razor.cs` | Remove `Blur` parameter; change `Speed` from `string` to `double` (default `0.004`); remove `blur` from config object |
| `tests/HeroWave.Tests/WavyBackgroundTests.cs` | Remove `Blur` from `Passes_Config_To_JsInit`; change `Speed` to `double` |
| `demo/HeroWave.Demo/Pages/FullPage.razor` | Change `Speed="fast"` → `Speed="0.008"` |
| `demo/HeroWave.Demo/Pages/Showcase.razor` | Remove all `Blur="..."` attributes; change `Speed="slow"` → `Speed="0.004"`, `Speed="fast"` → `Speed="0.008"` |
| `README.md` | Remove `Blur` from params table and examples; update `Speed` type/default |

---

### Task 1: Update the test to use the new API (write failing test first)

The test `Passes_Config_To_JsInit` passes `Speed` as a string and sets `Blur`. Update it to use `double` for `Speed` and remove `Blur`. This will cause a compile error until the component is updated — that compile error IS the failing test.

**Files:**
- Modify: `tests/HeroWave.Tests/WavyBackgroundTests.cs:103-117`

- [ ] **Step 1: Update Passes_Config_To_JsInit to use new API**

In `tests/HeroWave.Tests/WavyBackgroundTests.cs`, replace the `Passes_Config_To_JsInit` test body:

```csharp
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
```

- [ ] **Step 2: Attempt to build — confirm it fails**

Run:
```
dotnet build tests/HeroWave.Tests/
```

Expected: Build error — `cannot convert from 'double' to 'string'` (for `Speed`) — confirms the test is driving a real change.

---

### Task 2: Update C# component — remove Blur, change Speed to double

**Files:**
- Modify: `src/HeroWave/Components/WavyBackground.razor.cs`

- [ ] **Step 1: Remove Blur parameter and change Speed to double**

In `WavyBackground.razor.cs`, make these two changes:

1. Delete the `Blur` parameter line:
   ```csharp
   [Parameter] public int Blur { get; set; } = 10;
   ```

2. Change `Speed` from `string` to `double`:
   ```csharp
   // Before:
   [Parameter] public string Speed { get; set; } = "slow";

   // After:
   [Parameter] public double Speed { get; set; } = 0.004;
   ```

3. In `OnAfterRenderAsync`, remove `blur = Blur` from the config anonymous object and rename `speed` to pass the double directly:
   ```csharp
   var config = new
   {
       colors = Colors,
       backgroundColor = BackgroundColor,
       waveCount = WaveCount,
       waveWidth = WaveWidth,
       speed = Speed,
       opacity = Opacity
   };
   ```

- [ ] **Step 2: Build to confirm it compiles**

Run:
```
dotnet build src/HeroWave/
```

Expected: Build succeeds.

- [ ] **Step 3: Run unit tests**

Run:
```
dotnet test tests/HeroWave.Tests/ --logger "console;verbosity=normal"
```

Expected: All tests pass. (The updated `Passes_Config_To_JsInit` now compiles with `double` Speed and no Blur.)

- [ ] **Step 4: Commit**

```bash
git add src/HeroWave/Components/WavyBackground.razor.cs tests/HeroWave.Tests/WavyBackgroundTests.cs
git commit -m "feat!: remove Blur param, change Speed from string to double

Blur is now handled intrinsically by the 10-layer rendering approach.
Speed accepts a continuous double (e.g. 0.004) instead of 'slow'/'fast'."
```

---

### Task 3: Replace wavy-background.js draw loop

This is the core rendering change. Replace the entire `init` function body with 0.25x resolution scaling and 10-layer alpha compositing.

**Files:**
- Modify: `src/HeroWave/wwwroot/wavy-background.js`

- [ ] **Step 1: Replace the init function body**

Replace everything inside `export function init(canvas, config) { ... }` with:

```javascript
export function init(canvas, config) {
    const id = String(nextId++);
    const ctx = canvas.getContext("2d");
    const noise = createNoise();
    let nt = 0;
    let animationFrameId = null;
    let running = true;

    const scale = 0.25;

    const baseW = config.waveWidth;
    const opacityScale = config.opacity / 0.5;

    const layerDefs = [
        { widthMul: 2.4, baseAlpha: 0.02 },
        { widthMul: 2.2, baseAlpha: 0.03 },
        { widthMul: 2.0, baseAlpha: 0.04 },
        { widthMul: 1.8, baseAlpha: 0.05 },
        { widthMul: 1.6, baseAlpha: 0.06 },
        { widthMul: 1.4, baseAlpha: 0.07 },
        { widthMul: 1.2, baseAlpha: 0.08 },
        { widthMul: 1.0, baseAlpha: 0.10 },
        { widthMul: 0.8, baseAlpha: 0.12 },
        { widthMul: 0.6, baseAlpha: 0.14 },
    ];

    const layers = layerDefs.map(l => ({
        width: baseW * l.widthMul * scale,
        alpha: Math.min(1, l.baseAlpha * opacityScale),
    }));

    const step = Math.max(3, Math.round(5 * scale));

    function resize() {
        canvas.width = Math.round(canvas.offsetWidth * scale);
        canvas.height = Math.round(canvas.offsetHeight * scale);
    }

    function draw() {
        if (!running) return;
        const w = canvas.width;
        const h = canvas.height;

        ctx.globalAlpha = 1;
        ctx.fillStyle = config.backgroundColor;
        ctx.fillRect(0, 0, w, h);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        for (let i = 0; i < config.waveCount; i++) {
            const points = [];
            for (let x = 0; x < w; x += step) {
                const px = x / scale;
                const y = noise(px / 800, 0.3 * i, nt) * 100 * scale + h * 0.5;
                points.push([x, y]);
            }

            ctx.strokeStyle = config.colors[i % config.colors.length];
            for (const layer of layers) {
                ctx.globalAlpha = layer.alpha;
                ctx.lineWidth = layer.width;
                ctx.beginPath();
                let first = true;
                for (const [px, py] of points) {
                    if (first) { ctx.moveTo(px, py); first = false; }
                    else { ctx.lineTo(px, py); }
                }
                ctx.stroke();
            }
        }

        ctx.globalAlpha = 1;
        nt += config.speed;
        animationFrameId = requestAnimationFrame(draw);
    }

    resize();
    window.addEventListener("resize", resize);
    animationFrameId = requestAnimationFrame(draw);

    instances.set(id, { animationFrameId, resize, canvas, stop: () => { running = false; } });
    return id;
}
```

Key changes from the current code:
- `speedFactor` ternary is gone — `config.speed` (a double) is used directly as `nt += config.speed`
- `ctx.filter` and `ctx.globalAlpha = config.opacity` (single-stroke) are gone
- `ctx.closePath()` is removed (it would draw a line back to the path start, which is wrong for open wave curves)
- Background fill uses `ctx.globalAlpha = 1` (fix: old code incorrectly applied wave opacity to background)
- Noise x-coordinate uses `px / 800` where `px = x / scale` — maintains the same wave shapes at reduced pixel coords
- Amplitude uses `* 100 * scale` to stay in canvas-pixel space

- [ ] **Step 2: Run unit tests to confirm nothing broken**

Run:
```
dotnet test tests/HeroWave.Tests/ --logger "console;verbosity=normal"
```

Expected: All tests pass. (Tests mock JS interop — they don't execute the JS — so this verifies the C# layer is intact.)

- [ ] **Step 3: Run E2E tests**

Run:
```
dotnet test tests/HeroWave.E2E/ --logger "console;verbosity=normal"
```

Expected: All tests pass. (E2E tests verify canvas element exists and animation is running, not pixel output.)

- [ ] **Step 4: Commit**

```bash
git add src/HeroWave/wwwroot/wavy-background.js
git commit -m "feat: replace blur filter with 10-layer alpha compositing at 0.25x resolution

Renders canvas at 25% of CSS dimensions, upscaled via CSS bilinear
interpolation. 10 layered semi-transparent strokes replace ctx.filter blur.
Eliminates the single most expensive Canvas 2D operation."
```

---

### Task 4: Update demo pages

Remove all `Blur` attributes and convert `Speed` string values to doubles in the three demo pages that use them.

**Files:**
- Modify: `demo/HeroWave.Demo/Pages/FullPage.razor`
- Modify: `demo/HeroWave.Demo/Pages/Showcase.razor`

Note: Verify `Home.razor` has no `Blur` or string `Speed` attributes (check `demo/HeroWave.Demo/Pages/Home.razor`) — it currently uses only defaults so no edit should be needed, but confirm before skipping.

- [ ] **Step 1: Update FullPage.razor**

Change `Speed="fast"` to `Speed="0.008"`:

```razor
@page "/fullpage"

<WavyBackground Height="100vh"
                Speed="0.008"
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

- [ ] **Step 2: Update Showcase.razor**

For each `<WavyBackground>` in the file:
- Remove every `Blur="..."` attribute
- Change `Speed="slow"` → `Speed="0.004"` (appears in Ocean Aurora and Minimal Frost and Northern Lights)
- Change `Speed="fast"` → `Speed="0.008"` (appears in Sunset Fire, Neon Cyberpunk, Molten Gold)

The six presets after removing Blur and updating Speed:

```razor
<!-- Ocean Aurora -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#0ea5e9", "#06b6d4", "#14b8a6", "#10b981", "#34d399" })"
                BackgroundColor="#021a2b"
                WaveCount="6"
                WaveWidth="60"
                Speed="0.004"
                Opacity="0.6">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Deep sea calm</p>
</WavyBackground>

<!-- Sunset Fire -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#f97316", "#ef4444", "#ec4899", "#f59e0b", "#fb923c" })"
                BackgroundColor="#1a0a00"
                WaveCount="5"
                WaveWidth="45"
                Speed="0.008"
                Opacity="0.55">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Warm &amp; energetic</p>
</WavyBackground>

<!-- Neon Cyberpunk -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#ff00ff", "#00ffff", "#39ff14", "#ff3131" })"
                BackgroundColor="#0a0a0a"
                WaveCount="4"
                WaveWidth="35"
                Speed="0.008"
                Opacity="0.7">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Electric &amp; bold</p>
</WavyBackground>

<!-- Minimal Frost -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#e2e8f0", "#94a3b8", "#cbd5e1", "#f1f5f9" })"
                BackgroundColor="#0f172a"
                WaveCount="3"
                WaveWidth="70"
                Speed="0.004"
                Opacity="0.3">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Clean &amp; elegant</p>
</WavyBackground>

<!-- Northern Lights -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#a855f7", "#6366f1", "#22d3ee", "#4ade80", "#818cf8" })"
                BackgroundColor="#0c0720"
                WaveCount="7"
                WaveWidth="55"
                Speed="0.004"
                Opacity="0.5">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Ethereal glow</p>
</WavyBackground>

<!-- Molten Gold -->
<WavyBackground Height="50vh"
                Colors="@(new[] { "#fbbf24", "#f59e0b", "#d97706", "#b45309", "#fcd34d" })"
                BackgroundColor="#1c1208"
                WaveCount="5"
                WaveWidth="55"
                Speed="0.008"
                Opacity="0.45">
    <p style="color: white; font-size: 1.5rem; font-weight: 600;">Premium &amp; rich</p>
</WavyBackground>
```

- [ ] **Step 3: Build to confirm no compile errors**

Run:
```
dotnet build demo/HeroWave.Demo/
```

Expected: Build succeeds.

- [ ] **Step 4: Run all unit tests**

Run:
```
dotnet test tests/HeroWave.Tests/ --logger "console;verbosity=normal"
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add demo/HeroWave.Demo/Pages/FullPage.razor demo/HeroWave.Demo/Pages/Showcase.razor
git commit -m "chore: update demo pages for new Speed/Blur API

Remove Blur attributes (no longer a parameter).
Convert Speed string values to double (slow=0.004, fast=0.008)."
```

---

### Task 5: Update README parameter docs and examples

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update the Parameters table**

Replace the `Blur` and `Speed` rows in the parameters table:

```markdown
| `Speed` | `double` | `0.004` | Animation speed — time increment per frame (e.g. `0.004` slow, `0.008` fast) |
```

Remove the `Blur` row entirely.

- [ ] **Step 2: Update examples that use Blur or Speed**

In the Full Page Background example, change `Speed="fast"` to `Speed="0.008"`:
```razor
<WavyBackground Height="100vh"
                Speed="0.008"
                Colors="@(new[] { "#22d3ee", "#818cf8", "#e879f9" })"
                WaveCount="7"
                Opacity="0.6">
```

In the Color Presets section, remove all `Blur="..."` attributes and convert Speed strings:

```razor
**Ocean Aurora** - cool blues and greens
<WavyBackground Colors="@(new[] { "#0ea5e9", "#06b6d4", "#14b8a6", "#10b981", "#34d399" })"
                BackgroundColor="#021a2b" WaveCount="6" WaveWidth="60" Opacity="0.6" />

**Neon Cyberpunk** - electric, high contrast
<WavyBackground Colors="@(new[] { "#ff00ff", "#00ffff", "#39ff14", "#ff3131" })"
                BackgroundColor="#0a0a0a" WaveCount="4" WaveWidth="35" Speed="0.008" Opacity="0.7" />

**Minimal Frost** - white/silver on dark, subtle
<WavyBackground Colors="@(new[] { "#e2e8f0", "#94a3b8", "#cbd5e1", "#f1f5f9" })"
                BackgroundColor="#0f172a" WaveCount="3" WaveWidth="70" Opacity="0.3" />

**Northern Lights** - purples, greens, ethereal
<WavyBackground Colors="@(new[] { "#a855f7", "#6366f1", "#22d3ee", "#4ade80", "#818cf8" })"
                BackgroundColor="#0c0720" WaveCount="7" WaveWidth="55" />
```

For the remaining presets in the README: Sunset Fire has no `Speed` attribute — no change needed. Molten Gold has `Speed="fast"` — change it to `Speed="0.008"`.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: update README for removed Blur param and double Speed

Remove Blur from params table and examples.
Update Speed documentation to show continuous double values."
```

---

### Task 6: Final verification

- [ ] **Step 1: Run full test suite**

Run:
```
dotnet test
```

Expected: All test projects pass.

- [ ] **Step 2: Build all projects**

Run:
```
dotnet build
```

Expected: Zero errors, zero warnings about removed parameters.

- [ ] **Step 3: Run demo app and visually verify**

Run:
```
dotnet run --project demo/HeroWave.Demo
```

Open the app and check:
- Home page (`/`): waves animate with glow effect (no sharp edges)
- Full page (`/fullpage`): faster speed, custom colors
- Showcase (`/showcase`): all 6 presets render correctly with soft glow

Confirm: no `ctx.filter` errors in browser console, canvas renders at reduced resolution (visible in DevTools Elements panel as smaller `width`/`height` attributes than the CSS display size).
