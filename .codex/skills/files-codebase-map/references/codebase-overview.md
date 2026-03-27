# Files Codebase Overview

This repository is a multi-project Windows application centered on a WinUI 3 app, but it also includes:

- reusable custom controls
- storage abstraction and storage implementations
- Roslyn source generators and analyzers
- native C++ helper projects
- out-of-process WinRT integration
- UI and interaction test projects
- CI/CD workflows for MSIX packaging and signing

Use this document as a structural map. It is intentionally optimized for fast orientation rather than line-by-line completeness.

## Top-Level Layout

```text
D:\Source\Files
├─ .codex/                      Codex-local skills and references
├─ .github/                     CI/CD workflows, scripts, issue templates, assets
├─ src/                         Source projects
├─ tests/                       Test projects
├─ Directory.Build.props        Shared target framework and Windows settings
├─ Directory.Packages.props     Central package version management
├─ Files.slnx                   Solution definition
├─ global.json                  .NET SDK pinning
└─ Settings.XamlStyler          XAML formatting configuration
```

## Source Projects

```text
src
├─ Files.App/                   Main WinUI application
├─ Files.App.BackgroundTasks/   In-proc WinRT server for background-task integration
├─ Files.App.Controls/          Reusable custom WinUI controls
├─ Files.App.CsWin32/           Win32 interop generation project
├─ Files.App.Launcher/          Native C++ launcher helper EXE
├─ Files.App.OpenDialog/        Native C++ custom open dialog component
├─ Files.App.SaveDialog/        Native C++ custom save dialog component
├─ Files.App.Server/            Out-of-proc WinRT server with CsWinRT metadata
├─ Files.App.Storage/           App-facing storage implementations
├─ Files.Core.SourceGenerator/  Roslyn generators, analyzers, and code fixes
├─ Files.Core.Storage/          Core storage abstractions
├─ Files.Shared/                Shared attributes, helpers, and extensions
└─ Satori.targets               Runtime override build integration
```

## Tests

```text
tests
├─ Files.App.UITests/           Test host app for custom controls and UI samples
├─ Files.App.UnitTests/         Unit test placeholder / lightweight logic tests
└─ Files.InteractionTests/      WinAppDriver + accessibility interaction tests
```

## Project Guide

### Files.App

The main WinUI 3 app. This is where most feature work lands.

Key responsibilities:

- application startup and activation
- dependency injection setup
- shell UI, pages, dialogs, settings, view models
- command execution and command presentation
- localization, styles, and app-specific controls
- integration with storage, Windows shell, and helper projects

Major directories:

```text
Files.App
├─ Actions/         User-invokable actions grouped by domain
├─ Assets/          Images, package resources, deployment assets
├─ Converters/      XAML binding converters
├─ Data/            Commands, contexts, contracts, models, parameters
├─ Dialogs/         ContentDialog and dialog UI
├─ Extensions/      App-specific extension methods
├─ Helpers/         App-level helper classes
├─ Properties/      Publish profiles and project metadata
├─ Services/        Implementations behind app contracts
├─ Strings/         en-US source resources and other locales
├─ Styles/          Shared XAML resource dictionaries
├─ UserControls/    App-specific reusable UI pieces
├─ Utils/           Miscellaneous utilities
├─ ViewModels/      MVVM view models
└─ Views/           XAML pages and page code-behind
```

Placement guidance:

- add pages under `Views/`
- add page or dialog state under `ViewModels/`
- add business or system integration under `Services/`
- add user-facing actions under `Actions/`
- add app-only reusable UI under `UserControls/`

Cross-cutting notes:

- MVVM is the architectural default, but some legacy and view-driven areas are less pure
- command infrastructure is generator-backed rather than ad hoc
- user-facing strings should flow through `Strings/en-US/Resources.resw`

### Files.App.BackgroundTasks

In-proc WinRT server for background-task integration.

This project matters when:

- WinRT-activated background functionality changes
- app and background task contracts need to stay aligned
- packaging or activation behavior for in-proc background components changes

### Files.App.Controls

Reusable WinUI controls shared by the main app and test host app.

This is where a control belongs if it has:

- a reusable API surface
- custom dependency properties
- template parts or control templates
- automation peer requirements
- behavior that should not be app-page-specific

Major directories:

```text
Files.App.Controls
├─ AdaptiveGridView/  Adaptive grid presentation
├─ BladeView/         Multi-blade interface components
├─ BreadcrumbBar/     Path and breadcrumb UI
├─ GridSplitter/      Resizable layout splitter
├─ Omnibar/           Address / palette / suggestion control system
├─ SamplePanel/       Sample or showcase panel control
├─ Sidebar/           Sidebar controls and related models
├─ Storage/           Storage visualization controls
├─ ThemedIcon/        Files icon system and style dictionaries
├─ Themes/            Theme resources for controls
├─ Toolbar/           Toolbar-related controls
```

When changing app UI, check whether the behavior belongs here before putting it in `Files.App`.

### Files.App.CsWin32

Interop generation project for Win32 API access from managed code. Treat this as plumbing for P/Invoke boundaries rather than a feature surface.

### Files.App.Launcher

Native C++ helper EXE. Build output is copied into app assets and participates in packaged scenarios.

Use this area when a feature depends on:

- launching helper processes
- shell integration not comfortably handled in managed code
- deployment-time helper binaries

This is a fragile build-integrated area, not a generic utility project.

### Files.App.OpenDialog

Native C++ custom open dialog implementation.

Contains:

- COM and dialog plumbing
- IDL definitions
- native Windows dialog behavior

Changes here should be treated as shell/interop work, not normal app UI work.

### Files.App.SaveDialog

Native C++ custom save dialog implementation. Same concerns as `Files.App.OpenDialog`, but for save flows and dialog event handling.

### Files.App.Server

Out-of-proc WinRT server built with CsWinRT metadata output. Its generated `.winmd` is consumed by the main app during build.

This project matters when:

- app-to-server contracts change
- COM activation behavior changes
- CsWinRT metadata generation is involved

It is small in source size but high in build importance.

### Files.App.Storage

Concrete storage implementations with strong Windows and protocol integration.

Major directories:

```text
Files.App.Storage
├─ Ftp/       FTP-backed storables and services
├─ Legacy/    Older or transitional storage implementations
└─ Windows/   Windows file system, shell, tray, and bulk operation logic
```

Use this project when feature work requires storage behavior rather than just UI.

Typical examples:

- file operations
- shell item handling
- recycle bin behavior
- jump list integration
- tray and taskbar integration

### Files.Core.SourceGenerator

Roslyn infrastructure project. This repo depends on generated code and analyzers more than a typical app.

Major directories:

```text
Files.Core.SourceGenerator
├─ Analyzers/         Diagnostics over source usage
├─ CodeFixProviders/  Suggested automatic fixes
├─ Data/              Generator model types
├─ Extensions/        Generator helper extensions
├─ Generators/        Source generators
├─ Parser/            Input parsers for generator data
├─ Properties/        Launch settings and metadata
└─ Utilities/         Shared generator utilities
```

Important generator-backed systems include:

- command manager generation
- strings constant generation
- registry serialization
- vtable helper generation

Never patch generated outputs when the source generator is the real source of truth.

### Files.Core.Storage

UI-independent storage abstraction layer.

Contains:

- storage service contracts
- watcher contracts
- base enums and event args
- core storage extensions
- direct storage capability interfaces

Use this project when introducing abstractions that should outlive any specific Windows or FTP implementation.

### Files.Shared

Lowest-friction shared utility project.

Contains:

- shared attributes used by generators
- extension methods
- general helpers
- file logger infrastructure
- lightweight utility interfaces

If code is broadly reusable and has no app-UI dependency, it may belong here.

### Satori.targets

MSBuild logic that injects runtime override behavior for certain builds.

This is a build-system concern. Touch it only when you understand the packaging and runtime consequences.

## Test Project Guide

### Files.App.UITests

A small WinUI app used to host and exercise custom controls.

Major directories:

```text
Files.App.UITests
├─ Assets/      Test app assets
├─ Data/        Sample data models for control pages
├─ Properties/  Project metadata
└─ Views/       Test host pages for controls
```

Use this when validating reusable control behavior in isolation.

### Files.App.UnitTests

Currently light-weight. This is where pure logic tests should go if they do not require a UI host or WinAppDriver.

### Files.InteractionTests

End-to-end interaction and accessibility tests using WinAppDriver and Axe.

Major directories:

```text
Files.InteractionTests
├─ Helper/   Shared test helpers and accessibility helpers
└─ Tests/    Interaction test cases
```

Important behavior:

- session startup is centralized
- WinAppDriver launch is handled in test infrastructure
- accessibility validation is part of the suite

Use this for real app interaction coverage rather than control-isolated behavior.

## .github Guide

```text
.github
├─ assets/          Workflow-related static assets
├─ ISSUE_TEMPLATE/  GitHub issue templates
├─ scripts/         PowerShell scripts used by workflows
└─ workflows/       CI and CD pipelines
```

Workflow responsibilities:

- `ci.yml`: build, formatting checks, packaging for automated test runs
- `format-xaml.yml`: PR-triggered XAML formatting flow
- `cd-sideload-*.yml`: sideload package production
- `cd-store-*.yml`: Store package production

Script responsibilities:

- `Configure-AppxManifest.ps1`: manifest and secret injection
- `Create-MsixBundle.ps1`: package bundle creation
- `Generate-SelfCertPfx.ps1`: local/CI certificate generation
- `Convert-TrxToMarkdown.ps1`: test output shaping

## Common Change Paths

Use this as a shortcut when deciding where to work.

- new page or settings screen: `Files.App/Views` + `Files.App/ViewModels`
- new dialog: `Files.App/Dialogs` + `Files.App/ViewModels/Dialogs`
- new reusable control: `Files.App.Controls`
- new command/action: `Files.App/Actions` plus command infrastructure awareness
- new localized text: `Files.App/Strings/en-US/Resources.resw`
- new app service or OS integration: `Files.App/Services`
- storage behavior change: `Files.App.Storage` or `Files.Core.Storage`
- generator-backed behavior change: `Files.Core.SourceGenerator`
- shell dialog or launcher behavior: native C++ projects
- control-hosted test page: `Files.App.UITests`
- end-to-end interaction validation: `Files.InteractionTests`

## Fragile Areas

These areas deserve extra caution because they affect build shape, generated code, packaging, or interop.

- command generation and command registration
- strings generation and localization analyzer behavior
- `Files.App.Server` metadata flow into the main app
- native helper projects and their packaged outputs
- MSIX workflows, signing, and manifest mutation
- `Satori.targets`

## Working Mental Model

The safest way to navigate this repository is:

1. Identify the owning project first
2. Follow the nearest existing pattern
3. Check whether generators, localization, packaging, or tests are involved
4. Only then make edits

This repository rewards local consistency more than abstract architectural purity.
