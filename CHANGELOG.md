# Changelog

All notable changes to HeroWave will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `TargetFps` parameter (default 60, clamped 1–120) to cap animation frame rate and reduce CPU/GPU usage on less capable devices
- `IntersectionObserver` to automatically pause the animation loop when the canvas scrolls out of view
- Debounced window resize handling (100 ms) to avoid layout thrashing
- Live `update()` JS function so visual parameters can change at runtime without re-initialising the canvas
- Config hash tracking in `OnParametersSetAsync` to avoid redundant JS interop calls on every re-render
- Config key allowlist in JS `update()` to prevent arbitrary property injection
- XML documentation comments on all component parameters for IntelliSense support
- CHANGELOG.md tracking full release history

### Fixed
- `dispose()` crash at runtime: instance stored in `Map` was missing `animationFrameId`, `running`, and `stop()`. Fixed by storing an instance object with getter/bridge methods that access closure variables inside `init()`
- `update()` silently ignoring `waveWidth`/`opacity` changes: `rebuildLayers()` is now bridged through the instance object so config changes take effect
- `OnParametersSetAsync` firing on every render: added config hash tracking so JS `update()` is only called when parameter values actually change
- `frameInterval` stale after `update()`: `targetFps` changes via `update()` now take effect immediately since `frameInterval` is computed per-frame from `cfg.targetFps`
- Empty `Colors` array no longer crashes rendering (falls back to default palette)
- Stale blur reference and old repo URL in NuGet readiness spec
- E2E resize test now uses polling instead of flaky `Task.Delay`

### Changed
- `dispose()` now explicitly sets `running = false` via instance `stop()` bridge before cleanup
- Trimmed unused template imports from demo `_Imports.razor` and unused CSS from `app.css`

## [2.1.1] - 2026-03-21

### Fixed
- Remove benchmark link from demo home page

## [2.1.0] - 2026-03-21

### Added
- Benchmark page with FPS overlay and optimization toggles
- ToggleSwitch component for benchmark settings
- `wavy-background-bench.js` with optimization flags and FPS tracking

### Changed
- Extracted noise helpers to module scope to avoid closure allocation per noise call
- Used `Path2D` for path reuse across layers

### Fixed
- Call `StateHasChanged` after `ResetAll` to immediately update UI
- Use getter for `animationFrameId` in instance map to avoid stale `requestAnimationFrame` cancel
- Inlined `noise3D` variants to match production baseline (removed `skewAndLookup`)

## [2.0.3] - 2026-03-21

### Fixed
- Halved animation speed across all demo pages

## [2.0.2] - 2026-03-21

### Fixed
- Use relative paths in demo nav links for GitHub Pages compatibility

## [2.0.1] - 2026-03-21

### Added
- GitHub Pages deployment for live demo

### Fixed
- Removed `workflow_dispatch`, deploy only on stable tag releases
- Fixed copyright name in LICENSE file

## [2.0.0] - 2026-03-21

### ⚠️ Breaking Changes
- Removed `Blur` parameter (replaced by 10-layer alpha compositing)
- Changed `Speed` parameter from `string` to `double` (default `0.004`)

### Added
- 10-layer alpha-composited stroke rendering at 0.25x resolution
- Screenshots in README and fixed badge URLs

### Changed
- Updated README and demo pages for new Speed API (no Blur)

## [1.0.1-beta.2] - 2026-03-20

No changes from beta.1 — tag-only release.

## [1.0.1-beta.1] - 2026-03-20

### Changed
- Updated repo URLs from `LiamBoyleNie/hero-wave` to `cartsp/hero-wave`

## [1.0.0] - 2026-03-19

### Added
- Initial release of HeroWave Blazor component library
- WavyBackground component with canvas-based simplex noise animation
- JS interop with instance management for multiple components per page
- Demo Blazor WASM app with Home, FullPage, and Showcase pages
- bUnit component tests and Playwright E2E tests
- GitHub Actions CI/CD for build, test, and NuGet publishing
- MinVer for automatic versioning from git tags
- 6 color presets: Ocean Aurora, Sunset Fire, Neon Cyberpunk, Minimal Frost, Northern Lights, Molten Gold
