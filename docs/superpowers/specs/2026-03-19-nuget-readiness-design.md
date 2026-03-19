# HeroWave NuGet Readiness — Design Spec

## Context

HeroWave is a working Blazor WASM wavy background component packaged as a Razor Class Library. It's functional and demoed, but lacks everything needed to publish as a public open-source NuGet package: package metadata, license, tests, CI/CD, documentation, and branding. This spec covers the full roadmap to make it publish-ready on nuget.org.

**Repo:** https://github.com/LiamBoyleNie/hero-wave

## Decisions

- **Audience:** Public open source on nuget.org
- **Testing:** bUnit (component) + Playwright (E2E)
- **CI/CD:** GitHub Actions
- **License:** MIT
- **Versioning:** MinVer (git tag driven)
- **Icon:** Wave-inspired SVG/PNG
- **Docs:** Comprehensive README

## Implementation Order

Four layers, each building on the previous:

1. **Package Foundation** — metadata, license, MinVer, .gitignore
2. **Tests** — bUnit + Playwright projects (must exist before CI)
3. **CI/CD** — GitHub Actions for build/test/publish
4. **Docs & Branding** — README, icon, screenshots

---

## Section 1: Package Foundation

### Files to create/modify

- **Modify:** `src/HeroWave/HeroWave.csproj` — add package metadata + MinVer + asset includes
- **Create:** `LICENSE` — MIT license at repo root
- **Modify:** `.gitignore` — ensure `artifacts/` covered (already is)

### HeroWave.csproj changes

Add to existing `<PropertyGroup>`:
```xml
<PackageId>HeroWave</PackageId>
<Authors>LiamBoyleNie</Authors>
<Description>A reusable Blazor component for animated wavy background effects powered by canvas and simplex noise. Customizable colors, speed, blur, wave count, and more.</Description>
<PackageProjectUrl>https://github.com/LiamBoyleNie/hero-wave</PackageProjectUrl>
<RepositoryUrl>https://github.com/LiamBoyleNie/hero-wave</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<PackageTags>blazor;component;wavy;background;animation;canvas;wasm</PackageTags>
<PackageReadmeFile>README.md</PackageReadmeFile>
<PackageIcon>icon.png</PackageIcon>
```

Add `MinVer` package:
```xml
<PackageReference Include="MinVer" Version="6.*" PrivateAssets="All" />
```

Add file includes for pack:
```xml
<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="/" />
  <None Include="../../LICENSE" Pack="true" PackagePath="/" />
  <None Include="../../assets/icon.png" Pack="true" PackagePath="/" />
</ItemGroup>
```

### MinVer behavior

- No version in csproj needed — MinVer reads git tags
- Tag `v1.0.0` → package version `1.0.0`
- Commits after tag → `1.0.1-alpha.0.N` (prerelease)
- First release: tag `v1.0.0` on the commit you want to publish

### LICENSE

Standard MIT license text with `Copyright (c) 2026 LiamBoyleNie`.

---

## Section 2: Testing

### New project: `tests/HeroWave.Tests/`

**Type:** xUnit + bUnit test project

**Dependencies:**
- `bunit` — Blazor component testing
- `xunit` — test framework
- `Moq` — mock `IJSRuntime` and `IJSObjectReference`
- Project reference to `src/HeroWave/`

**Test cases:**

| Test | What it verifies |
|------|-----------------|
| `Renders_Container_With_Height` | Output has `wavy-background-container` div with correct `style="height: ..."` |
| `Renders_Canvas_Element` | Output contains a `<canvas>` with class `wavy-background-canvas` |
| `Renders_Title_When_Set` | `<h1>` with class `wavy-background-title` appears when `Title` is set |
| `Omits_Title_When_Null` | No `<h1>` when `Title` is null |
| `Renders_Subtitle_When_Set` | `<p>` with class `wavy-background-subtitle` appears |
| `Omits_Subtitle_When_Null` | No subtitle `<p>` when null |
| `Renders_ChildContent` | Custom markup passed as ChildContent appears in overlay |
| `Applies_CssClass_To_Overlay` | The overlay div includes the custom CSS class |
| `Default_Height_Is_100vh` | Without setting Height, container style is `height: 100vh` |
| `Calls_JsInterop_On_FirstRender` | After first render, `import` is called with correct module path |
| `Passes_Config_To_JsInit` | The `init` call receives config object with correct parameter values |
| `Calls_Dispose_On_Cleanup` | Disposing the component calls JS `dispose` with the instance ID |
| `Handles_JSDisconnectedException` | Dispose doesn't throw when JS circuit is already gone |

### New project: `tests/HeroWave.E2E/`

**Type:** xUnit + Playwright test project

**Dependencies:**
- `Microsoft.Playwright` — browser automation
- `xunit` — test framework

**Test fixture:** Starts `demo/HeroWave.Demo` as a process, waits for it to be ready, Playwright connects to it.

**Test cases:**

| Test | What it verifies |
|------|-----------------|
| `HomePage_Renders_WavyBackground` | Canvas element exists on `/`, has non-zero dimensions |
| `HomePage_Has_Title_And_Subtitle` | "Build Amazing Apps" heading and subtitle text visible |
| `FullPage_Renders_With_CustomColors` | `/fullpage` loads without errors, canvas is full viewport |
| `Navigation_Between_Pages_No_Errors` | Navigate `/` → `/fullpage` → `/`, no console errors (dispose works) |
| `Canvas_Resizes_On_Window_Resize` | Resize viewport, canvas dimensions update |
| `Showcase_Renders_Multiple_Instances` | `/showcase` has 6 canvas elements, all with non-zero size |

### Solution changes

Add both test projects to `HeroWave.sln`:
```
tests/HeroWave.Tests/HeroWave.Tests.csproj
tests/HeroWave.E2E/HeroWave.E2E.csproj
```

---

## Section 3: CI/CD (GitHub Actions)

### `.github/workflows/ci.yml` — Build & Test

**Triggers:** Push to `master`, pull requests to `master`

**Steps:**
1. Checkout
2. Setup .NET 10
3. `dotnet restore`
4. `dotnet build --no-restore`
5. `dotnet test tests/HeroWave.Tests/ --no-build` (bUnit)
6. Install Playwright browsers
7. Start demo app in background
8. `dotnet test tests/HeroWave.E2E/ --no-build` (Playwright)
9. Stop demo app

### `.github/workflows/publish.yml` — Pack & Publish

**Triggers:** Push tag matching `v*`

**Steps:**
1. Checkout with `fetch-depth: 0` (MinVer needs full history)
2. Setup .NET 10
3. `dotnet restore`
4. `dotnet build --no-restore -c Release`
5. `dotnet test --no-build -c Release` (both test projects)
6. `dotnet pack src/HeroWave/HeroWave.csproj -c Release -o ./artifacts --no-build`
7. `dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json`

### GitHub secrets needed

- `NUGET_API_KEY` — API key from nuget.org account

---

## Section 4: Documentation & Branding

### `README.md`

Structure:
1. **Header** — package name + one-line description
2. **Badges** — CI status, NuGet version, license
3. **Screenshot/GIF** — demo of the wave animation
4. **Installation** — `dotnet add package HeroWave`
5. **Quick start** — minimal `<WavyBackground>` example
6. **Parameters** — full table of all 12 parameters with types, defaults, descriptions
7. **Examples** — hero section, full page, custom colors (from Showcase presets)
8. **Presets** — Ocean Aurora, Sunset Fire, Neon Cyberpunk, Minimal Frost, Northern Lights, Molten Gold
9. **Contributing** — how to build, run tests, submit PRs
10. **License** — MIT link

### Package icon

- Create `assets/icon.png` — 128x128 PNG
- Wave-inspired design matching the component's visual style (colored wave lines on dark background)
- Referenced via `<PackageIcon>icon.png</PackageIcon>` in csproj

### Screenshots

- Capture screenshots of the demo app (Home hero, FullPage, Showcase presets)
- Store in `assets/screenshots/` directory
- Referenced from README via relative paths
- Add `assets/screenshots/` to the repo but NOT to the NuGet package

---

## File Summary

| Action | Path | Purpose |
|--------|------|---------|
| Modify | `src/HeroWave/HeroWave.csproj` | Package metadata, MinVer, asset includes |
| Modify | `HeroWave.sln` | Add test projects |
| Create | `LICENSE` | MIT license |
| Create | `README.md` | Package documentation |
| Create | `assets/icon.png` | Package icon (128x128 wave graphic) |
| Create | `assets/screenshots/*.png` | README screenshots |
| Create | `tests/HeroWave.Tests/` | bUnit component tests (13 tests) |
| Create | `tests/HeroWave.E2E/` | Playwright E2E tests (6 tests) |
| Create | `.github/workflows/ci.yml` | Build + test on PR |
| Create | `.github/workflows/publish.yml` | Pack + publish on tag |

## Success Criteria

1. `dotnet test` passes all bUnit tests
2. Playwright E2E tests pass against the demo app
3. `dotnet pack` produces a valid `.nupkg` with README, LICENSE, icon, static assets
4. CI workflow runs green on a PR
5. Publish workflow successfully pushes to nuget.org on a `v*` tag
6. `dotnet add package HeroWave` works in a fresh Blazor WASM project and the component renders correctly
7. README is comprehensive with working screenshots and examples

## Verification

1. Run `dotnet test` from solution root — all bUnit + Playwright tests pass
2. Run `dotnet pack src/HeroWave/HeroWave.csproj -c Release -o ./artifacts` — produces `.nupkg`
3. Inspect `.nupkg` (rename to `.zip`) — verify `staticwebassets/`, `lib/`, `README.md`, `LICENSE`, `icon.png` present
4. Push a PR — CI workflow triggers and passes
5. Tag `v1.0.0-beta.1` and push — publish workflow triggers, package appears on nuget.org
6. Create a fresh `dotnet new blazorwasm` project, `dotnet add package HeroWave`, add `<WavyBackground>`, run — it works
