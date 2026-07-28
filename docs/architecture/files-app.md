# New Files.App architecture

This document is the implementation blueprint for the new WinUI application.
Files.Core is the UI-independent model graph; Files.App is a thin,
window-scoped adaptation and rendering layer.

## Dependency direction

```mermaid
flowchart TB
    Views["WinUI Views"]
    ViewModels["Window-scoped ViewModels"]
    Adapters["UI adapters and presenters"]
    AppModels["Files.Core AppModels"]
    Core["Files.Core services"]

    Views --> ViewModels
    ViewModels --> Adapters
    ViewModels --> AppModels
    Adapters --> Core
    AppModels --> Core
```

Allowed dependencies:

| Layer | May depend on |
| --- | --- |
| Views | ViewModels and WinUI-only behaviors |
| ViewModels | AppModels, UI adapter interfaces, command adapters |
| WinUI adapters | Files.Core result contracts and WinUI/platform APIs |
| AppModels | CoreModels and capability contracts |
| Providers | Their backend APIs and Core contracts |

Files.Core never references Files.App. ViewModels never call provider or
Windows Shell APIs directly.

## Proposed source layout

```text
src/Files.App/
  Bootstrap/
    FilesAppHost.cs
    FilesCoreComposition.cs
    WindowFactory.cs
  ViewModels/
    Windows/WindowViewModel.cs
    Tabs/TabViewModel.cs
    Panes/PaneViewModel.cs
    Items/BrowseItemViewModel.cs
  Collections/
    BrowseItemCollectionAdapter.cs
  Commands/
    CommandRegistry.cs
    WindowCommandManager.cs
    CommandBindingViewModel.cs
    Adapters/NavigationCommandAdapter.cs
    Adapters/StorageCommandAdapter.cs
    Adapters/ClipboardCommandAdapter.cs
  Archives/
    ArchiveCredentialProvider.cs
    ArchiveCredentialDialogService.cs
  Connections/
    FtpConnectionProfileStore.cs
    FtpCredentialProvider.cs
    FtpConnectionDialogService.cs
  Previews/
    PreviewPresenter.cs
    StreamPreviewPresenter.cs
    WindowsShellPreviewPresenter.cs
  Imaging/
    ThumbnailImageFactory.cs
  Platform/
    WinUiDispatcher.cs
    PreviewHostWindow.cs
    Clipboard/OleClipboardService.cs
    DragDrop/DragDropService.cs
    Shell/ShellContextMenuService.cs
    Shell/ShellMenuMessageRouter.cs
  Settings/
    PersistedViewSettingsStore.cs
    WindowSessionStore.cs
  Views/
    Windows/MainWindow.xaml
    Windows/RootView.xaml
    Shell/NavigationToolbar.xaml
    Shell/SidebarView.xaml
    Tabs/TabStripView.xaml
    Tabs/TabContentView.xaml
    Panes/PaneView.xaml
    Browsing/FolderBrowserView.xaml
    Browsing/Layouts/DetailsFolderView.xaml
    Browsing/Layouts/GridFolderView.xaml
    Previews/PreviewView.xaml
```

Folder names are boundaries, not new projects. Keep them even after the
existing non-UI projects are physically merged into Files.Core.

## UI composition and state flow

Replace `Frame` and `Page` navigation with a retained tree of `UserControl`
instances and `ContentPresenter` hosts. The existing `Sidebar` may remain a
templated control, but it follows the same dependency-property boundary as
the composed user controls.

```mermaid
classDiagram
    direction TB

    class MainWindow {
        <<Window>>
    }
    class RootView {
        <<UserControl>>
        +WindowViewModel ViewModel
    }
    class TabStripView {
        <<UserControl>>
        +WindowViewModel ViewModel
    }
    class NavigationToolbar {
        <<UserControl>>
        +PaneViewModel Pane
    }
    class SidebarView {
        <<Control>>
        +PaneViewModel Pane
    }
    class Toolbar {
        <<UserControl>>
        +PaneViewModel Pane
    }
    class TabContentHost {
        <<ContentPresenter>>
    }
    class TabContentView {
        <<UserControl>>
        +TabViewModel ViewModel
    }
    class PaneView {
        <<UserControl>>
        +PaneViewModel ViewModel
    }
    class PaneContentHost {
        <<ContentPresenter>>
    }
    class FolderBrowserView {
        <<UserControl>>
        +PaneViewModel ViewModel
    }
    class FolderLayoutHost {
        <<ContentPresenter>>
    }
    class FolderLayoutView {
        <<UserControl>>
        +PaneViewModel ViewModel
    }
    class DetailsFolderView {
        <<UserControl>>
    }
    class GridFolderView {
        <<UserControl>>
    }
    class SettingsView {
        <<UserControl>>
    }
    class WebBrowserView {
        <<UserControl>>
    }
    class TerminalView {
        <<UserControl>>
        +PaneViewModel Pane
    }
    class InfoPaneView {
        <<UserControl>>
        +PaneViewModel Pane
    }
    class ShelfPaneView {
        <<UserControl>>
        +PaneViewModel Pane
    }

    MainWindow *-- RootView : Content
    RootView *-- TabStripView
    RootView *-- NavigationToolbar
    RootView *-- SidebarView
    SidebarView *-- Toolbar
    SidebarView *-- TabContentHost
    SidebarView *-- TerminalView
    SidebarView *-- InfoPaneView
    SidebarView *-- ShelfPaneView
    TabContentHost o-- TabContentView : active tab
    TabContentView *-- "1..2" PaneView : panes
    PaneView *-- PaneContentHost
    PaneContentHost o-- FolderBrowserView : browse
    PaneContentHost o-- SettingsView : settings
    PaneContentHost o-- WebBrowserView : web
    FolderBrowserView *-- FolderLayoutHost
    FolderLayoutHost o-- FolderLayoutView : active layout
    FolderLayoutView <|-- DetailsFolderView
    FolderLayoutView <|-- GridFolderView
```

`RootView` is the window-scoped composition view. It renders the tab
membership from `WindowViewModel` and updates shared shell controls from its
active pane. A `TabContentView` renders one or two retained `PaneView`
instances according to `TabViewModel`. Each browse pane owns one retained
`FolderBrowserView` for the lifetime of its `PaneViewModel`.

The toolbar, sidebar, terminal, info pane, and shelf pane are instantiated
once per window. Their pane dependency changes when focus moves between tabs
or split panes; changing that dependency must not recreate a Core model,
restart enumeration, or maintain another active-pane ID.

### Dependency property contracts

Pass the direct ViewModel down explicitly. Do not rely on a process-global
current window, implicit service lookup, or a serialized navigation
parameter.

ViewModels do not declare or consume dependency properties. They remain
UI-independent objects. A parent View reads its direct ViewModel and assigns
the corresponding child ViewModel to a dependency property on each child
control:

```mermaid
classDiagram
    direction TB

    class WindowViewModel {
        +Tabs
        +ActiveTab
        +ActivePane
    }
    class TabViewModel {
        +Panes
        +ActivePane
    }
    class PaneViewModel {
        +BrowseItemCollectionAdapter Items
        +BrowseViewSettings ViewSettings
        +BrowseSelectionState Selection
    }
    class RootView {
        +WindowViewModel ViewModel
    }
    class TabStripView {
        +WindowViewModel ViewModel
    }
    class TabContentView {
        +TabViewModel ViewModel
    }
    class SharedPaneView {
        <<DPContract>>
        +PaneViewModel Pane
    }
    class PaneView {
        +PaneViewModel ViewModel
    }
    class FolderBrowserView {
        +PaneViewModel ViewModel
    }
    class FolderLayoutView {
        +PaneViewModel ViewModel
    }

    WindowViewModel *-- TabViewModel : owns Tabs
    WindowViewModel --> TabViewModel : ActiveTab
    WindowViewModel --> PaneViewModel : ActivePane
    TabViewModel *-- "1..2" PaneViewModel : owns Panes
    TabViewModel --> PaneViewModel : ActivePane

    RootView --> WindowViewModel : ViewModel DP
    RootView ..> TabStripView : ViewModel = ViewModel
    RootView ..> TabContentView : ViewModel = ViewModel.ActiveTab
    RootView ..> SharedPaneView : Pane = ViewModel.ActivePane
    TabContentView --> TabViewModel : ViewModel DP
    TabContentView ..> PaneView : ViewModel = each Panes item
    PaneView --> PaneViewModel : ViewModel DP
    PaneView ..> FolderBrowserView : ViewModel = ViewModel
    FolderBrowserView ..> FolderLayoutView : ViewModel = ViewModel
```

`SharedPaneView` represents the common dependency-property contract used by
`NavigationToolbar`, `Toolbar`, `SidebarView`, `TerminalView`,
`InfoPaneView`, and `ShelfPaneView`; it does not require a shared CLR base
class. Assignment labels describe data flow; Views never construct the child
ViewModels they receive.

`WindowViewModel.ActivePane` is a derived, observable projection of
`ActiveTab?.ActivePane`. It does not own another pane or store a competing
active-pane ID. Its purpose is to update every shared `Pane` dependency
property when either the active tab or that tab's active pane changes.

| View | Primary dependency property | Lifetime |
| --- | --- | --- |
| `RootView` | `WindowViewModel ViewModel` | One per WinUI window |
| `TabStripView` | `WindowViewModel ViewModel` | Shared by the window |
| `TabContentView` | `TabViewModel ViewModel` | One per model tab |
| `NavigationToolbar`, `Toolbar`, `SidebarView` | `PaneViewModel? Pane` | Shared; follows the focused pane |
| `TerminalView`, `InfoPaneView`, `ShelfPaneView` | `PaneViewModel? Pane` | Shared; follows the focused pane |
| `PaneView` | `PaneViewModel ViewModel` | One per model pane |
| `FolderBrowserView` | `PaneViewModel ViewModel` | One per browse pane |
| Folder layout views | `PaneViewModel ViewModel` | Owned by the folder browser view |

Controls treat these ViewModels as borrowed references. A property-change
callback detaches handlers from the previous value before attaching the new
value. The ViewModel owner remains the corresponding parent ViewModel or
`WindowFactory`; unloading a control does not dispose the model graph.

Use dependency properties and `x:Bind` at control boundaries. Ordinary
template elements may use bindings to the control's dependency properties,
but a nested control must receive its dependency explicitly. This keeps the
visual tree aligned with the AppModel and ViewModel ownership trees.

### Content presenters

Content selection is a View responsibility:

- `TabContentPresenter` selects the active retained `TabContentView`;
- `PaneContentPresenter` selects a `UserControl` for the pane content;
- `FolderBrowserView` selects a layout view from `BrowseViewSettings`;
- the ViewModel exposes state and commands, never a `UIElement`, `Type`, or
  `DataTemplate`;
- a keyed template or window-scoped view factory may create controls, but it
  must not resolve Core services or own AppModels.

The first adoption slice supports `FolderBrowserView`. Settings and web
content may be added later through Files.App content ViewModels without
changing `BrowseSessionModel`. `Frame.Navigate`, page-type routing, and
serialization of model objects for in-process navigation are prohibited.

## Process bootstrap

Create exactly one Core runtime per Files process:

```csharp
public sealed class FilesAppHost : IAsyncDisposable
{
	private readonly FilesCoreRuntime core;
	private readonly WindowFactory windowFactory;

	private FilesAppHost(
		FilesCoreRuntime core,
		WindowFactory windowFactory)
	{
		this.core = core;
		this.windowFactory = windowFactory;
	}

	public static FilesAppHost Create(AppServices services)
	{
		var core = new FilesCoreBuilder(
				services.ViewSettings,
				services.ThumbnailCache)
			.AddWindowsStorage(
				streamPreviewPolicy: services.StreamPreviewPolicy,
				shellPreviewPolicy: services.ShellPreviewPolicy,
				archiveCredentialProvider:
					services.ArchiveCredentials)
			.Build();

		return new FilesAppHost(
			core,
			new WindowFactory(core, services));
	}

	public async ValueTask DisposeAsync()
	{
		await windowFactory.DisposeAsync();
		await core.DisposeAsync();
	}
}
```

The bootstrap may use `Microsoft.Extensions.DependencyInjection` to construct
process services. That container ends at the composition root. Do not inject
`IServiceProvider` into models or ViewModels.

At startup, load every non-secret FTP profile and call `AddFtpStorage` before
`Build`. `FtpCredentialProvider` resolves its password from Windows
Credential Manager and may marshal an authentication request to the owning
window. It must never add the password to a `StorageAddress`, navigation
history, telemetry, or a ViewModel. Until Files.Core gains a mutable source
registry, adding or removing a connection requires rebuilding the
process-wide runtime or restarting the process.

## Window creation

One WinUI window adapts one `WindowModel`:

```mermaid
sequenceDiagram
    participant Activation as App activation
    participant Host as FilesAppHost
    participant Core as ApplicationModel
    participant Factory as WindowFactory
    participant UI as WinUI Window

    Activation->>Host: open location
    Host->>Core: CreateWindowAsync(location)
    Core-->>Host: WindowModel
    Host->>Factory: Create(WindowModel)
    Factory->>UI: Window + WindowViewModel
    UI-->>Activation: activate
```

`WindowFactory` owns the mapping between model IDs and WinUI windows. It
closes the ViewModel and WinUI resources before calling
`FilesApplicationModel.CloseWindowAsync`.

The model is authoritative for tab/pane membership and active IDs. WinUI
controls render that state; they do not maintain a competing tab graph.

## ViewModel hierarchy

Each ViewModel receives its direct model and explicit UI adapters:

```csharp
public sealed class PaneViewModel : IAsyncDisposable
{
	public PaneViewModel(
		PaneModel model,
		IUiDispatcher dispatcher,
		ThumbnailImageFactory thumbnails,
		PreviewPresenter previews,
		StorageCommandAdapter operations)
	{
		// Subscribe, capture an initial snapshot, and create commands.
	}
}
```

Recommended mapping:

| ViewModel | Model | Additional responsibility |
| --- | --- | --- |
| `WindowViewModel` | `WindowModel` | Window title/activation, tab VM lifetime |
| `TabViewModel` | `TabModel` | Split layout VM lifetime |
| `PaneViewModel` | `PaneModel` | Navigation commands, collection adapter |
| `BrowseItemViewModel` | `StorableReference` plus current presentation | Localized labels, image object, selection facade |

`BrowseItemViewModel` must not retain an old `IStorableModel` after a replace
change. It should update from the new snapshot or be replaced by the
collection adapter.

## Browse collection adapter

`BrowseSessionModel.Items` is an immutable snapshot.
`BrowseItemsChangedEventArgs` supplies a version and granular changes.
`BrowseItemCollectionAdapter` owns the WinUI-facing
`ObservableCollection<BrowseItemViewModel>`.

```mermaid
sequenceDiagram
    participant Session as BrowseSessionModel
    participant Adapter as CollectionAdapter
    participant Dispatcher as UI dispatcher
    participant Items as ObservableCollection

    Session-->>Adapter: ItemsChanged(version, changes)
    Adapter->>Adapter: capture model snapshot
    Adapter->>Dispatcher: enqueue update
    Dispatcher->>Adapter: verify version
    Adapter->>Items: add/remove/move/replace/reset
```

Rules:

- Never mutate an observable collection from a Core event thread.
- Apply changes only when their previous version matches the adapter version.
- On a gap, stale event, or unsupported change sequence, reset from
  `session.Items`.
- Key item VMs by `StorableKey`, not by path or list index.
- Keep selection synchronization guarded so UI selection changes do not echo
  indefinitely back into `SetSelection`.

## Thumbnails

Core returns encoded immutable bytes:

```csharp
ThumbnailResult result = presentation.Thumbnail;
ReadOnlyMemory<byte> encoded = result.Content;
```

`ThumbnailImageFactory` converts those bytes on the UI side. A WinUI
implementation can copy the memory into an `InMemoryRandomAccessStream`,
seek to zero, and call `BitmapImage.SetSourceAsync`. Cache the resulting
`ImageSource` only at the UI layer when it is dispatcher-affine; the shared
Core cache continues to store encoded bytes.

```mermaid
flowchart TD
    Core["ThumbnailResult bytes"]
    Factory["ThumbnailImageFactory"]
    Stream["RandomAccessStream"]
    Image["BitmapImage"]
    View["Image control"]

    Core --> Factory
    Factory --> Stream
    Stream --> Image
    Image --> View
```

Cancel decoding when an item VM is replaced or unrealized. Verify its
`StorableKey` and presentation version before assigning the image.

## Preview presenters

`PreviewPresenter` switches on `BrowsePreviewSnapshot.Result`:

| Result | Files.App presenter |
| --- | --- |
| `StreamPreviewResult` image | Image decoder/view |
| `StreamPreviewResult` audio/video | Media player adapter |
| `StreamPreviewResult` text | Bounded text reader/editor adapter |
| `StreamPreviewResult` PDF/HTML | Explicit safe renderer or web adapter |
| `WindowsShellPreviewResult` | `WindowsShellPreviewPresenter` |
| `BlockedPreviewResult` | Policy explanation and optional hydrate action |

The presenter owns the UI object; `BrowsePreviewModel` owns the
`PreviewResult`. A presenter must stop using the result when the snapshot
changes and must not dispose the model-owned result itself.

### Windows Shell preview host

The Shell presenter is the only WinUI boundary for `IPreviewHandler`:

```mermaid
flowchart TD
    Snapshot["WindowsShellPreviewResult"]
    Presenter["Shell preview presenter"]
    Host["Child HWND host"]
    Factory["Core session factory"]
    Session["Shell preview session"]
    Handler["Out-of-process handler"]

    Snapshot --> Presenter
    Presenter --> Host
    Presenter --> Factory
    Factory --> Session
    Session --> Handler
```

Implementation order:

1. create a dedicated child HWND owned by the pane's preview surface;
2. convert arranged logical pixels to physical-pixel
   `WindowsPreviewBounds`;
3. call `CreateAsync(result, host)`;
4. forward size, theme, focus, and accelerator updates;
5. dispose the session before destroying the child HWND;
6. dispose every session before `FilesCoreRuntime`.

Do not pass a XAML control pointer as an HWND. Do not activate a handler on
the UI thread. The Core factory owns the dedicated preview STA and defaults
to local-server activation.

## Navigation commands

The window-scoped registry, binding, context, cancellation, and execution
rules are defined in [Files.App command execution](commands.md). Command
surfaces invoke a stable command ID; they do not call the pane directly.
The navigation adapter then calls the pane:

| Command | Model call |
| --- | --- |
| Open | `NavigateAsync(location)` |
| Refresh | `RefreshAsync()` |
| Back | `GoBackAsync()` |
| Forward | `GoForwardAsync()` |
| Up | `GoUpAsync()` |
| Change layout/sort/columns | `BrowseSession.UpdateViewSettingsAsync()` |

Can-execute state comes from `CanGoBack`, `CanGoForward`, `CanGoUp`,
`IsLoading`, and model membership. Commands should hold a per-command
`CancellationTokenSource` and never block the dispatcher.

### Opening archives

The open command checks archive capabilities before ordinary folder shape.
This matters because Windows Shell can expose `.zip` or `.7z` as an
`IFolderModel`, while an encrypted archive must still be routed to the
SevenZip fallback.

```csharp
private static BrowseLocation GetOpenLocation(
	IStorableModel item)
{
	if (item is IFolderModel
		&& item.Get<IArchiveEntry>() is { } entry)
	{
		return new ArchiveLocation(entry);
	}

	if (item.Get<IArchiveSource>() is { } archive)
	{
		return new ArchiveLocation(archive.Archive);
	}

	if (item is IFolderModel folder)
	{
		return new FolderLocation(folder.Reference);
	}

	throw new InvalidOperationException(
		$"'{item.Name}' cannot be browsed.");
}
```

`ArchiveCredentialProvider` is window-aware application infrastructure. It
marshals to the owning window dispatcher, shows localized WinUI content, and
returns `ArchiveCredential` or `null` when canceled. It does not store the
password in navigation history or the item ViewModel.

Core's archive context resolves Up inside the archive with another
`ArchiveLocation`. Up from the archive root resolves the outer archive's
storage parent and returns a `FolderLocation`. Back and Forward retain the
outer reference and normalized entry path.

See [Archive browsing](archives.md) for backend selection and ownership.

## Storage commands

`StorageCommandAdapter` captures references from the pane, prompts for any UI
input, constructs a request, then calls `runtime.StorageOperations`.

```csharp
var request = new RenameOperationRequest(item.Reference, newName);
if (!operations.CanHandle(request))
{
	return RenameOutcome.Unsupported;
}

var result = await operations.ExecuteAsync(
	request,
	progress,
	cancellationToken);
```

The adapter displays `result.Error` according to application policy.
It does not directly edit the item collection. The folder watcher updates the
session; the returned result reference is useful for reveal/focus intent.

Drag/drop packages, clipboard formats, elevation, conflict prompts, and undo
UI live in this adapter layer. The storage request remains UI-independent.
The concrete command lifecycle is defined in
[Files.App command execution](commands.md). Native clipboard, drag/drop,
cross-source transfer, and Shell menu ownership are defined in
[Clipboard, drag/drop, and Shell integration](platform-interactions.md).

## View settings

Implement `PersistedViewSettingsStore` in Files.App over the existing settings
database. Serialize by an explicit location DTO:

- location kind;
- source ID and item ID for folders;
- outer source/item identity and normalized entry path for archives;
- current recovery address as non-key metadata;
- search query/scope;
- tag ID;
- schema version.

Column widths are logical pixels. Validate layout enum values, property IDs,
orders, visibility, and minimum/maximum widths when reading old data.
`InMemoryViewSettingsStore` remains the test and fallback implementation.

## UI dispatch and error policy

Define one small window-scoped abstraction:

```csharp
public interface IUiDispatcher
{
	bool HasThreadAccess { get; }

	ValueTask EnqueueAsync(
		Action action,
		CancellationToken cancellationToken = default);
}
```

Each window gets its own dispatcher. A ViewModel cannot assume the process has
one UI thread.

Core exceptions retain backend meaning. Files.App maps them to localized,
actionable UI:

- cancellation: no error dialog;
- not supported: disable or explain the command;
- access denied: permission guidance;
- missing identity: refresh/reveal failure;
- preview blocked: policy-specific UI;
- unexpected failure: log full exception and show a stable error code.

## Ownership and shutdown

```mermaid
flowchart TB
    Host["FilesAppHost"]
    Windows["WindowFactory"]
    VMs["ViewModels"]
    Presenters["Preview and image presenters"]
    Runtime["FilesCoreRuntime"]

    Host --> Windows
    Windows --> VMs
    VMs --> Presenters
    Host --> Runtime
```

Shutdown order:

1. stop activation and new window creation;
2. dispose ViewModels and collection adapters;
3. dispose Shell preview sessions and child HWNDs;
4. close model windows or dispose the application graph;
5. dispose `FilesCoreRuntime`;
6. dispose application-only telemetry/settings services.

Every event subscription must have an owner and deterministic unsubscription.
Avoid weak events as a substitute for correct lifetime.

## Adoption slices

The first slice is a walking skeleton, not the complete browser:

1. reference Files.Core behind a temporary Files.App feature boundary;
2. build `FilesAppHost` and production policies;
3. create one `MainWindow` and `RootView` from `HomeLocation`;
4. adapt one window, one tab, and one pane;
5. retain one `FolderBrowserView` through `PaneContentPresenter`;
6. display `BrowseSessionModel.Items` through the collection adapter;
7. implement selection and back/forward/up/refresh.

The slice is complete when Home and one filesystem folder render without a
`Frame`, the active pane reaches shared controls only through dependency
properties, and the Files.App x64 build succeeds.

Continue in independently buildable slices:

1. viewport reporting, details/grid settings, and thumbnail decoding;
2. stream previews, then the child-HWND Shell preview presenter;
3. rename/create/copy/move/delete command adapters;
4. split panes and multiple tabs;
5. persisted view and window-session state.

Do not begin by moving old ViewModels into the new folders. Build each slice
against Files.Core contracts, then migrate one existing user flow at a time.

## Related implementation blueprints

- [Files.App command execution](commands.md)
- [Clipboard, drag/drop, and Shell integration](platform-interactions.md)
- [Storage operations](operations.md)
- [New Files.Core composition](composition.md)
