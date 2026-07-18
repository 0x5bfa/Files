# Migration to Files.Core

The final physical layout and the logical architecture are separate decisions. Moving files between projects should not be mixed with replacing the model graph.

## Target project layout

```mermaid
flowchart TB
    Shared["Files.Shared"]
    CoreStorage["Files.Core.Storage"]
    AppStorage["Files.App.Storage"]
    CsWin32["Files.App.CsWin32"]
    Core["Files.Core"]

    Shared --> Core
    CoreStorage --> Core
    AppStorage --> Core
    CsWin32 --> Core
```

One assembly does not mean one layer. `Files.Core` should retain namespaces and folders for storage contracts, providers, AppModels, browsing, operations, and interop.

## Safe migration order

```mermaid
flowchart TD
    Contracts["1. Stabilize contracts"]
    Slice["2. Build vertical slices"]
    Adopt["3. Adopt from Files.App"]
    Move["4. Move remaining code"]
    Delete["5. Remove old projects"]

    Contracts --> Slice
    Slice --> Adopt
    Adopt --> Move
    Move --> Delete
```

1. Stabilize `IStorageSource`, CoreModel capabilities, AppModels, and browse-session ownership in the new project.
2. Implement one provider and one browser path end to end. Windows storage is the first slice.
3. Introduce composition in `Files.App` behind a feature boundary. Do not move unrelated source files during adoption.
4. Move WinUI-agnostic logic from the four existing projects after consumers use the new contracts.
5. Remove old projects and temporary references only after their dependency edges reach zero.

## Transitional dependency

The prototype currently has this temporary edge:

```mermaid
flowchart LR
    Core["Files.Core"] --> CsWin32["Files.App.CsWin32"]
```

It allows the Windows provider to use existing source-generated interop without copying generated code. When CsWin32 moves into `Files.Core`, this project reference disappears while namespaces and higher-level contracts remain stable.

## Conflict controls

- Do not rename or move the existing four projects while the architecture contracts are still changing.
- Do not make existing storage services implement new contracts through large compatibility classes.
- Prefer a vertical feature boundary over a repository-wide type replacement.
- Keep WinUI out of `Files.Core`, even though `Files.Core` targets Windows.
- Keep existing and new implementations side by side until a consumer is migrated and verified.
