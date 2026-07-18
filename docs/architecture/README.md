# Files architecture prototype

This directory describes the proposed Files architecture and the first implementation in `Files.Core`.

The architecture separates responsibilities logically even though the long-term plan places most non-UI code in one physical project.

```mermaid
flowchart TB
    App["Files.App composition root"]
    Views["WinUI views and ViewModels"]
    AppModels["Files application models"]
    CoreModels["Provider CoreModels"]
    Providers["OwlCore.Storage and platform APIs"]

    App --> Views
    App --> AppModels
    Views --> AppModels
    AppModels --> CoreModels
    CoreModels --> Providers
```

## Dependency rules

| Layer | Owns | May depend on |
| --- | --- | --- |
| Views | WinUI controls, visual state, input routing | ViewModels |
| ViewModels | Labels, glyphs, shortcuts, `ICommand` adapters | AppModels |
| AppModels | Browse sessions, navigation state, Files-specific item behavior | CoreModels |
| CoreModels | Standardized storage items and optional capabilities | OwlCore.Storage |
| Providers | Windows Shell, FTP, archives, cloud APIs | Platform APIs and CoreModel contracts |

The following dependencies are prohibited:

- AppModels depending on WinUI, `Window`, `Frame`, or `Page`.
- ViewModels locating dependencies through `Ioc.Default`.
- Storage providers depending on ViewModels.
- Views calling Windows Shell, FTP, or archive APIs directly.
- `IStorageSource` pretending to be an `IStorable`.

## Trickle-down composition

Objects are constructed at the application boundary and passed down through the model and visual trees.

```mermaid
flowchart TB
    Root["Files application root"]
    Workspace["Workspace model"]
    Tab["Tab model"]
    Pane["Pane / browse session"]
    Item["Storable AppModel"]

    Root --> Workspace
    Workspace --> Tab
    Tab --> Pane
    Pane --> Item
```

The prototype currently implements the storage-backed part of this graph: `IFilesDataRoot`, typed browse locations, location handlers, and `IBrowseSessionModel`. Workspace, tab, pane ownership, operations, and ViewModels remain future work.

## Documents

- [Storage model boundaries](storage-models.md)
- [Windows storage provider](windows-storage.md)
- [Migration to Files.Core](migration.md)
