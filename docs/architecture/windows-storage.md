# Windows storage provider

The Windows provider maps the Windows Shell namespace to OwlCore.Storage without introducing WinUI dependencies or leaking apartment-affine COM interfaces into ordinary models.

## Object model

```mermaid
classDiagram
    class IStorageSource
    class WindowsStorageSource {
        +Scheduler
    }
    class IWindowsShellScheduler
    class WindowsStorableSnapshot {
        +ItemId
        +ParsingName
    }
    class IWindowsStorable {
        +ParsingName
        +FileSystemPath
        +Address
    }
    class WindowsStorable
    class WindowsFile
    class WindowsFolder

    IStorageSource <|.. WindowsStorageSource
    WindowsStorageSource --> IWindowsShellScheduler : owns or receives
    IWindowsStorable <|.. WindowsStorable
    WindowsStorable <|-- WindowsFile
    WindowsStorable <|-- WindowsFolder
    WindowsStorable --> WindowsStorableSnapshot : contains
    WindowsStorageSource --> WindowsStorable : creates
```

`WindowsStorageSource` resolves both `shell` and `file` addresses. Its default root is the Shell `ComputerFolder` known folder.

```csharp
await using var dataRoot = new FilesDataRoot(
	[new WindowsStorageSource()],
	new StorableModelFactory(capabilities));

var windows = dataRoot.Sources.Single();

await foreach (var root in dataRoot.GetRootsAsync(windows.SourceId))
{
	using (root)
	{
		// Pass root.Reference into a FolderLocation or another AppModel.
	}
}
```

`WindowsStorableSnapshot` copies these values while still on the ordered Shell STA:

- `ItemId` is created by one `IWindowsItemIdentityProvider`. File-system items use the volume serial and file index; virtual or inaccessible items use an explicit `address:` fallback.
- `ParsingName` uses `SIGDN_DESKTOPABSOLUTEPARSING` as the Shell locator and is kept separate from `IStorable.Id`.
- `Name` uses a UI-friendly Shell display name with a normal-display fallback.
- `FileSystemPath` uses `SIGDN_FILESYSPATH` only when `SFGAO_FILESYSTEM` is present. It is nullable by design.
- `IsFolder` selects `WindowsFolder` or `WindowsFile` without retaining `IShellItem`.

Windows file IDs are stable across rename, but reverse lookup from a file ID is not implemented yet. Keep `LastKnownAddress` with references so the source can recover through the current address when identity lookup cannot resolve an opaque ID.

Using a filesystem path or parsing name as the identity would make virtual items such as This PC, libraries, Recycle Bin, and portable devices unidentifiable.

## Snapshot boundary

```mermaid
flowchart LR
    Request["Resolve address"]
    STA["Ordered Shell STA"]
    Item["IShellItem"]
    Copy["Copy identity and display data"]
    Snapshot["WindowsStorableSnapshot"]
    Model["WindowsFile / WindowsFolder"]

    Request --> STA
    STA --> Item
    Item --> Copy
    Copy --> Snapshot
    Snapshot --> Model
    Item -. never exposed .-> STA
```

Most CoreModels are therefore apartment-neutral and do not need disposal. The two exceptions are private wrappers around resources that must remain live:

- `ShellFolderEnumerator` owns `IEnumShellItems` and routes each bounded batch to the same ordered STA.
- `ShellReadStream` owns a virtual `IStream` and routes `Read`, `Seek`, `Stat`, and release to the same ordered STA.

Neither wrapper exposes its COM interface.

## Browse flow

```mermaid
sequenceDiagram
    participant Session as BrowseSession
    participant Source as WindowsStorageSource
    participant STA as Ordered Shell STA
    participant Shell as Windows Shell
    participant Enum as ShellFolderEnumerator

    Session->>Source: ResolveAsync(reference)
    Source->>STA: create Shell item
    STA->>Shell: SHCreateItemFromParsingName
    Shell-->>STA: IShellItem
    STA-->>Source: managed folder snapshot
    Source-->>Session: WindowsFolder
    Session->>STA: create enumerator
    STA->>Shell: BHID_EnumItems
    Shell-->>STA: IEnumShellItems
    STA-->>Session: private affine wrapper
    loop 32-item bounded batches
        Session->>Enum: ReadNextAsync(32)
        Enum->>STA: enumerate and copy snapshots
        STA-->>Enum: managed snapshots
        Enum-->>Session: Windows child models
    end
```

Enumeration does not buffer the entire folder. A bounded batch amortizes scheduler transitions while preserving streaming and cancellation between batches.

## File streams

```mermaid
flowchart TD
    Open["WindowsFile.OpenStreamAsync"]
    HasPath{"FileSystemPath available?"}
    FileStream["System.IO.FileStream"]
    ReadOnly{"Read access?"}
    Bind["Bind BHID_Stream on ordered STA"]
    ShellStream["ShellReadStream affine wrapper"]
    Denied["UnauthorizedAccessException"]

    Open --> HasPath
    HasPath -->|Yes| FileStream
    HasPath -->|No| ReadOnly
    ReadOnly -->|Yes| Bind
    Bind --> ShellStream
    ReadOnly -->|No| Denied
```

File-system items use `FileStream` with read/write/delete sharing. Virtual items request `IStream` through `BHID_Stream`; the prototype exposes that stream as read-only.

## Lifetime

- `FilesDataRoot` owns each `WindowsStorageSource`.
- A source created without an injected scheduler owns and disposes its `WindowsShellScheduler`.
- A source given an `IWindowsShellScheduler` borrows it; the composition root owns that shared scheduler.
- `WindowsStorable` contains only a managed snapshot and is not disposable.
- The affine enumerator and stream wrappers must finish before their source or shared scheduler is disposed.
- Source-generated COM projections are used through `Files.App.CsWin32`; incompatible `Marshal.ReleaseComObject` APIs are not used.

See [Windows Shell threading](threading.md) for lane selection, cancellation, reentrancy, and shutdown.

## Current scope

Implemented:

- Parsing file-system and virtual Shell items.
- Resolving known folders, addresses, and persisted references.
- Centralized filesystem identity from volume serial and file index, with an explicit address fallback for items that cannot expose a stable filesystem ID.
- Parent lookup.
- Streaming child enumeration in bounded batches.
- File-system streams and apartment-safe virtual read streams.
- Injectable message-pumped STA scheduling.
- Windows Shell thumbnail extraction through `IShellItemImageFactory`, with PNG materialization inside the concurrent Shell STA lane.

Generic capability composition now exists for thumbnails, previews, properties, and decorators. Windows-specific property extraction, watchers, search, mutations, bulk operations, context menus, and Shell verbs remain separate vertical slices.
