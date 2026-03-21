# 10-Layer Alpha Rendering Design

## Summary

Replace the current Canvas 2D rendering approach in `wavy-background.js` with a 10-layer alpha-composited stroke technique at 0.25x resolution scaling. This eliminates the expensive `ctx.filter = "blur()"` post-process and renders at a quarter resolution with CSS upscaling, resulting in significantly better performance on low-end hardware while maintaining a visually equivalent look.

## Motivation

- `ctx.filter = "blur(10px)"` is the single most expensive operation in the current render loop, consuming roughly half the frame budget on constrained hardware (VMs, software-rendered environments, low-spec devices)
- Canvas 2D hardware acceleration is inconsistent — VMs and some configurations silently fall back to CPU rendering
- Resolution scaling at 0.25x cuts pixel count to ~6% while maintaining acceptable visual quality for a decorative background animation
- The 10-layer alpha compositing approach produces a visually equivalent soft-glow effect without any filter or shadow API calls

## Design

### What changes

**File**: `src/HeroWave/wwwroot/wavy-background.js`

#### 1. Resize logic — render at 0.25x

Current:
```javascript
canvas.width = canvas.offsetWidth;
canvas.height = canvas.offsetHeight;
```

New:
```javascript
const scale = 0.25;
canvas.width = Math.round(canvas.offsetWidth * scale);
canvas.height = Math.round(canvas.offsetHeight * scale);
```

The canvas CSS dimensions remain `width: 100%; height: 100%` via the scoped CSS, so the browser upscales the lower-resolution canvas via bilinear interpolation. The existing `image-rendering: auto` CSS default handles this.

#### 2. Draw loop — 10-layer alpha strokes

Current approach:
```javascript
ctx.globalAlpha = config.opacity;
ctx.filter = `blur(${config.blur}px)`;
ctx.lineWidth = config.waveWidth;
// single stroke per wave
```

New approach:
```javascript
const s = scale;
const layers = [
  { width: 120 * s, alpha: 0.02 },
  { width: 110 * s, alpha: 0.03 },
  { width: 100 * s, alpha: 0.04 },
  { width: 90 * s,  alpha: 0.05 },
  { width: 80 * s,  alpha: 0.06 },
  { width: 70 * s,  alpha: 0.07 },
  { width: 60 * s,  alpha: 0.08 },
  { width: 50 * s,  alpha: 0.10 },
  { width: 40 * s,  alpha: 0.12 },
  { width: 30 * s,  alpha: 0.14 },
];
```

Each wave is drawn 10 times — widest/faintest first, narrowest/brightest last. The overlapping semi-transparent strokes create a gradual falloff that approximates the gaussian blur effect. No `ctx.filter`, no `ctx.shadowBlur`.

#### 3. Wave path computation — compute once, draw 10 times

To avoid computing simplex noise 10x per wave, pre-compute the path points for each wave, then iterate the layers using the same points:

```javascript
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
    for (const [px, py] of points) { /* moveTo/lineTo */ }
    ctx.stroke();
  }
}
```

#### 4. Noise input scaling

The noise function input `x / 800` assumes pixel-space x coordinates at full resolution. At 0.25x, canvas x values are 4x smaller. To maintain identical wave shapes, divide by the scale factor: `(x / scale) / 800`.

#### 5. Wave amplitude scaling

Similarly, the `* 100` amplitude and `h * 0.5` center are in pixel space. At 0.25x, these need scaling: `noise(...) * 100 * scale + h * 0.5`.

#### 6. Step size

Current step is 5px at full resolution. At 0.25x the canvas is ~4x narrower, so fewer iterations naturally. Use `Math.max(3, Math.round(5 * scale))` to maintain similar curve smoothness.

#### 7. Line cap and join

Add `ctx.lineCap = 'round'` and `ctx.lineJoin = 'round'` for smoother visual appearance at the wider stroke widths.

### What stays the same

- **Public API**: All Blazor component parameters unchanged
- **C# code**: `WavyBackground.razor.cs` unchanged — same JS interop, same config object
- **Razor markup**: `WavyBackground.razor` unchanged
- **Scoped CSS**: `WavyBackground.razor.css` unchanged
- **Instance management**: `init()` / `dispose()` signatures and instance Map unchanged
- **Noise implementation**: Simplex noise code unchanged
- **Config handling**: Speed, colors, waveCount, backgroundColor all consumed the same way
- **Resize handler**: Still responds to `window.resize`, just applies the scale factor
- **Animation loop**: Still uses `requestAnimationFrame`

### Config parameter mapping

| Parameter | Current usage | New usage |
|-----------|--------------|-----------|
| `colors` | `ctx.strokeStyle` per wave | Same — `ctx.strokeStyle` per wave |
| `backgroundColor` | `ctx.fillStyle` background | Same |
| `waveCount` | Loop count | Same |
| `waveWidth` | `ctx.lineWidth` directly | Used as base for layer width calculations (layers scale relative to it) |
| `blur` | `ctx.filter = "blur(Npx)"` | Ignored — blur effect comes from layer spread |
| `speed` | `speedFactor` for time increment | Same |
| `opacity` | `ctx.globalAlpha` | Baked into layer alphas (layers are calibrated to produce ~0.5 effective center opacity) |

### Layer alpha math

The 10 layers produce a composited center opacity of approximately:

```
1 - (1-0.02)(1-0.03)(1-0.04)(1-0.05)(1-0.06)(1-0.07)(1-0.08)(1-0.10)(1-0.12)(1-0.14)
= 1 - 0.498
≈ 0.50
```

This matches the original's `globalAlpha = 0.5` at the wave center. At the outermost edge (60px scaled from center), only the widest layer contributes, giving alpha ≈ 0.02 — a gradual falloff.

## Testing

- **Existing bUnit tests**: Should pass unchanged — they test component rendering/parameters, not canvas visuals
- **Existing E2E tests**: Should pass — they verify the canvas element exists and animation runs, not pixel output
- **Manual verification**: Compare visual output in demo app against current version at various presets
- **Performance verification**: Measure frame times on constrained hardware to confirm improvement

## Files changed

| File | Change |
|------|--------|
| `src/HeroWave/wwwroot/wavy-background.js` | Replace resize logic, draw loop, remove filter blur |
