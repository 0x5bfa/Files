# Files Architecture Direction

This document describes the intended architectural direction of the Files codebase.

It is not a description of the current implementation. For the current repository layout and project responsibilities, read `codebase-overview.md`.

Use this document to answer these questions:

- What are we optimizing for?
- Which patterns should new code follow?
- Which existing patterns are legacy and should not spread further?
- When touching old code, what direction should the refactor move in?

## Purpose

Files has grown over time through organic feature work, platform transitions, and community contribution. As a result, the codebase contains several overlapping architectural styles and technology choices.

This document exists to make the preferred direction explicit.

## Primary Goal

The primary technical goal is to make the app safe for trimming and AOT so package size, startup characteristics, deployment shape, and runtime behavior can improve over time.

This goal affects architecture broadly. It is not limited to publishing settings.

It influences:

- interop strategy
- control architecture
- navigation composition
- storage implementation choices
- dependency choices
- server and process boundaries

## Root Causes

### UWP legacy

Large parts of the codebase were shaped by earlier UWP constraints and patterns. Some of those patterns were practical at the time but are now poor fits for a modern WinUI, trim-aware, AOT-oriented application.

### Organic growth

The app expanded across many features and contributors. This produced useful abstractions, but also duplicate mechanisms, mixed styles, and several competing ways to solve similar problems.

### Mixed abstraction levels

The codebase mixes:

- modern WinUI composition ideas
- page-oriented navigation patterns
- app-specific user controls
- reusable custom controls
- WinRT-era storage assumptions
- Shell-based and Win32-based implementations
- multiple native interop stacks

The result is not one single bad subsystem, but many partial systems that do not pull in the same direction.

## Architecture Priorities

### P0. AOT safety and trimming

This is the top priority. If two implementation options are otherwise comparable, prefer the one that is more compatible with trimming, NativeAOT-style constraints, and reduced runtime marshalling assumptions.

### P1. UI architecture evolution

Page-based navigation remains part of the app and will continue to be used. The long-term direction is to prefer more compositional UI patterns, especially `UserControl` plus `ContentPresenter`, when new work or major rework makes that practical and clearly beneficial.

### P2. Native interop modernization

Standardize on an AOT-safe interop approach centered on CsWin32 with source-generated interop patterns as a later step when practical.

### P3. Control architecture cleanup

Push reusable UI into proper custom controls in `Files.App.Controls` instead of accumulating app-local `UserControl` implementations for concepts that should be reusable and stylable.

### P4. Storage backend modernization

Move away from legacy UWP-era and older Win32 storage patterns toward `Files.App.Storage` and Shell-API-centered implementations.

### P5. Reliability and process boundaries

Where instability or isolation needs justify it, continue moving critical functionality toward cleaner WinRT server boundaries and better-separated execution models.

## Decision Framework

Each architectural area below uses the same structure:

- `Current`: what exists today
- `Target`: what direction new work should move toward
- `Why`: why the target is preferred
- `For new code`: what brand new implementation should do
- `For touched existing code`: how to migrate incrementally
- `Avoid`: patterns that should not expand further
- `Validation` or `Notes`: practical checks and caveats

## 1. AOT Safety And Trimming

### Current

The codebase already contains some trim-aware and AOT-aware settings, but several technology choices and legacy abstractions still work against that goal.

Examples include:

- dependencies that are not AOT-safe
- interop layers that depend on patterns unfriendly to trimming or runtime marshalling constraints
- architecture that spreads reflection-heavy or marshaling-heavy assumptions

### Target

The codebase should gradually converge on an architecture where the app can be confidently trimmed and where AOT-oriented publishing constraints are first-class design constraints rather than afterthoughts.

### Why

- reduce package size
- reduce unnecessary runtime surface
- improve future publish flexibility
- make dependency decisions more rigorous
- align the codebase with modern .NET native interop direction

### For new code

- prefer dependencies and APIs that are trim-safe or plausibly trim-safe
- avoid introducing libraries already known to work against AOT goals
- treat runtime marshalling assumptions as suspect by default
- choose designs that do not rely on dynamic or hidden behavior when a static alternative exists

### For touched existing code

- when touching a subsystem, bias refactors toward trim-safe and AOT-safe patterns
- remove known blockers incrementally when the local change already touches that area
- document temporary exceptions instead of pretending they are permanent design choices

### Avoid

- introducing new dependencies that are known to be incompatible with the long-term publish goal
- expanding legacy patterns just because they are already present
- treating AOT work as a final cleanup step after architecture decisions are already locked in

### Validation

Architectural choices should be evaluated against whether they move the app closer to, or further from, trim-safe and AOT-safe publishing.

## 2. UI Architecture: Page-Based Navigation And Composition-Based UI

### Current

The current codebase still relies heavily on page-based navigation patterns. Many view surfaces in `Files.App/Views` are implemented as pages even when they behave more like composable UI surfaces than independent navigation destinations.

### Target

Keep supporting page-based navigation while gradually adopting a composition model based on `UserControl`-hosted views rendered through `ContentPresenter` where that model provides clear architectural benefit.

This is a preferred long-term direction for suitable surfaces, not a blanket requirement to eliminate pages.

### Why

- pages are a poor fit for many highly composable surfaces
- dependency properties are more naturally leveraged with compositional view trees than frame-style page navigation
- composition-based UI is a better fit for advanced reusable shells and pane systems
- page-based patterns can make state flow and UI reuse harder than necessary

### Target composition model

The intended direction for suitable surfaces is:

- shell or host owns composition and active content selection
- view surfaces are increasingly represented as `UserControl` implementations
- `ContentPresenter` becomes the rendering switch point
- view models remain the state and behavior layer

This is not a claim that every page should disappear soon. It is the preferred direction when a surface is being designed or substantially reworked and composition is the better fit.

### For new code

- do not assume `Page` is the only valid UI primitive
- if the surface is fundamentally hosted content rather than navigation infrastructure, consider `UserControl` plus `ContentPresenter` first
- if the surrounding area is strongly page-based and composition brings little immediate value, staying page-based is acceptable
- keep state in view models and dependency properties where appropriate

### For touched existing code

- when working in existing `Page`-based areas, look for opportunities to extract the real content into a `UserControl` only when that refactor is proportionate and beneficial
- preserve behavior first, then reduce `Page`-specific assumptions where practical
- prefer migration patterns that allow page-based and composition-based hosting models to coexist temporarily

### Avoid

- rewriting stable page-based areas without a clear payoff
- assuming every page should be migrated immediately
- coupling feature views tightly to frame-navigation assumptions when they are really compositional content
- moving state into code-behind when the goal is richer dependency-property-driven composition

### Notes

This is an evolutionary direction, not an all-at-once rewrite mandate.

Reference: [Outlook on the Files Codebase Toward vNext](https://raw.githubusercontent.com/0x5bfa/Website/refs/heads/5bfa/codebase-docs/src/routes/blog/posts/outlook-on-the-codebase-toward-vnext/%2Bpage.md)

## 3. Native Interop Direction

### Current

The codebase currently uses several native interop styles:

- Vanara
- current CsWin32-generated interop
- manual native definitions

Vanara is useful in places but does not align with the long-term AOT direction. Existing CsWin32 usage is more aligned with the goal, but in its current form it may still rely heavily on pointers and unsafe usage that is difficult to scale comfortably across a large codebase.

In this repository, the generated CsWin32 surface is also shaped by the shared generator configuration in `Files.App.CsWin32/NativeMethods.json`.

### Target

Standardize on CsWin32 as the native interop strategy.

Over time, opportunistically evolve CsWin32 usage toward source-generated-interop-friendly patterns that better align with trimming, AOT, and disabled runtime marshalling assumptions.

That longer-term direction includes:

- `LibraryImport`
- `GeneratedComInterface`
- marshalling-friendly signatures and call shapes
- configurations that cooperate with `DisableRuntimeMarshalling`
- CsWin32 generation settings and workflows that support this direction

### Why

- aligns with the primary AOT goal
- reduces long-term dependence on interop layers that do not fit the target runtime model
- creates a more consistent interop story
- improves maintainability versus multiple competing interop stacks

### For new code

- prefer CsWin32 over Vanara or fresh manual definitions unless there is a concrete blocker
- design interop with trim-safe and runtime-marshalling-light assumptions in mind
- when practical, prefer marshalling-friendly shapes that keep later migration to source-generated interop straightforward
- avoid creating new interop islands with a different style unless there is a strong technical reason

### For touched existing code

- migrate Vanara usage toward CsWin32 when the area is already under active modification and the migration is practical
- reduce manual definitions when equivalent generated interop is viable
- do not require every existing CsWin32 call site to be rewritten immediately
- where current CsWin32 usage is too unsafe or awkward, move toward more marshalling-friendly and source-generated-interop-friendly patterns when the refactor is proportionate
- large focused migrations are acceptable when they materially improve the long-term interop foundation

### Avoid

- adding new Vanara dependencies for convenience
- creating more bespoke interop definitions when CsWin32 can represent the API
- locking new CsWin32 code into pointer-heavy patterns when a more marshalling-friendly shape is practical
- assuming “already unsafe” means architectural consistency no longer matters

### Validation

The CsWin32 guidance explicitly documents support for trimming, AOT, and disabling the runtime marshaler, including build-task generation and `LibraryImport` / COM source generation paths.

### Notes

Moving existing CsWin32-generated interop toward source-generated interop often requires changing the shared generation settings in `Files.App.CsWin32/NativeMethods.json`, which in turn changes generated signatures across the codebase and requires corresponding usage rewrites. That makes this migration a coordinated configuration-and-callsite change rather than a purely local cleanup, though focused in-place refactor PRs can still be a valid strategy.

Reference: [CsWin32 Getting Started](https://raw.githubusercontent.com/microsoft/CsWin32/refs/heads/main/docfx/docs/getting-started.md)

## 4. Control Architecture

### Current

The codebase contains both:

- reusable custom controls in `Files.App.Controls`
- app-local `UserControl` implementations in `Files.App/UserControls`

Some UI pieces that conceptually want to be reusable controls still live as app-local user controls.

### Target

Reusable control concepts should increasingly become proper custom controls in `Files.App.Controls`, not `UserControl` implementations in `Files.App/UserControls`.

### Why

- custom controls are more flexible and stylable
- reusable behavior belongs in the controls project, not the app shell by default
- this keeps app-specific composition separate from reusable control surfaces

### For new code

- if the UI concept is reusable, stylable, templated, or API-like, implement it as a custom control in `Files.App.Controls`
- reserve `Files.App/UserControls` for app-specific composition that is not intended to become a reusable control primitive

### For touched existing code

- when an app-local user control is evolving into a reusable concept, plan a move into `Files.App.Controls`
- migrate behavior and API surface intentionally rather than cloning logic into both places

### Avoid

- adding more reusable control concepts to `Files.App/UserControls`
- using `UserControl` by default for reusable control design
- keeping reusable styling logic trapped in app-local composition code

## 5. Storage Architecture

### Current

The storage story still contains significant legacy. Some implementation paths stem from older UWP-era assumptions and from legacy Windows API usage that does not reflect the intended modern direction.

There is also architectural overlap between older `Files.App/Utils/Storage` lineage and the more explicit `Files.App.Storage` project direction.

### Target

Move storage implementation emphasis toward `Files.App.Storage` and toward modern Shell-API-centered behavior instead of relying on restricted WinRT file APIs or older legacy Win32 storage approaches.

### Why

- Shell APIs better match the app’s real file manager needs
- older UWP-era abstractions are often too constrained
- consolidating storage implementation direction reduces duplication and conceptual drift

### For new code

- prefer `Files.App.Storage` as the home for storage implementation work
- prefer Shell-oriented approaches when they better represent the desired behavior
- avoid building new storage features on top of legacy utility layers unless blocked

### For touched existing code

- when working in old storage code, migrate toward `Files.App.Storage` where feasible
- treat old utility-based storage layers as legacy, not as the preferred extension point
- reduce reliance on restricted WinRT-only assumptions where Shell-based behavior is the intended future

### Avoid

- adding new storage functionality into legacy `Files.App/Utils/Storage`-style areas
- expanding UWP-era abstractions solely because they are already present
- choosing older API layers when Shell APIs provide the better long-term fit

## 6. Reliability And Process Boundaries

### Current

The app already uses multiple process and server models, including in-proc and out-of-proc WinRT servers. However, architectural boundaries are still evolving, and some functionality remains closer to fragile UI or in-process surfaces than ideal.

### Target

Where reliability, isolation, or architectural cleanliness justify it, continue moving suitable functionality toward better-defined server boundaries and away from unnecessarily fragile UI-hosted execution paths.

This does not mean every feature should become out-of-proc. It means process boundaries should be treated as an architectural tool, not an accident of history.

### Why

- isolate critical operations
- reduce blast radius of unstable behavior
- create more explicit contracts between app UI and supporting functionality

### For new code

- think deliberately about whether the logic belongs in the UI process
- avoid coupling critical non-visual behavior too tightly to view lifetime when a server boundary is more appropriate

### For touched existing code

- where a subsystem is already strained by process, activation, or isolation issues, prefer refactors that clarify boundaries instead of deepening ad hoc coupling

### Avoid

- assuming in-process is always simpler just because it is immediate
- anchoring non-visual infrastructure too tightly to page or window lifetimes

## Migration Rules Of Thumb

- Do not expand legacy patterns just because they already exist.
- Prefer incremental migration when touching code instead of unrelated large rewrites.
- New code should follow the target direction even if old code nearby does not fully do so yet.
- If a subsystem cannot yet move to the target architecture, document the blocker rather than silently normalizing the old pattern.
- Local consistency matters, but long-term direction should still influence local choices.

## Blocked Features And Why

Some requested features are difficult not because they are individually unreasonable, but because they expose architectural limitations in the current codebase.

This document should eventually track those blocked requests and the architectural issues they depend on.

Examples mentioned in design discussions include:

- TreeView
- SFTP
- registered copy handler
- Files as open dialog
- column reordering
- WebDAV

This section should grow into a mapping from requested capability to the subsystem limitations blocking it.

## Non-Goals

- This document does not require rewriting the entire codebase at once.
- Temporary coexistence of old and new patterns is expected during migration.
- Existing code is not automatically wrong just because it predates the new direction.

## Glossary

### AOT-safe

Reasonably compatible with ahead-of-time compilation constraints and the long-term native publishing direction.

### Trim-safe

Reasonably compatible with linker trimming and reduced runtime surface assumptions.

### In-proc WinRT server

A WinRT component activated within the app process boundary.

### Out-of-proc WinRT server

A WinRT component activated in a separate process boundary.

### Custom control

A reusable, stylable, templated control that belongs in the controls layer rather than an app-local composition surface.

### UserControl-hosted view

A composable view surface intended to be hosted by another UI shell or presenter rather than treated only as a page-navigation endpoint.

### Shell API

Windows Shell-oriented APIs and integration points that better represent file-manager-grade behavior than more restricted WinRT storage APIs.
