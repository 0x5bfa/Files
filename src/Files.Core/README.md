# Files.Core prototype

This project prototypes the new UI-agnostic model graph alongside the existing implementation.

The implemented vertical slice is:

1. `IStorageSource` owns provider-specific resolution and returns OwlCore.Storage CoreModels.
2. `IStorableModelFactory` wraps CoreModels as Files AppModels.
3. `CapabilityPipeline` lazily composes item-bound capabilities from direct, provider, and plugin candidates.
4. `IFilesDataRoot` composes configured sources and forms the root of the storage-backed graph.
5. `IBrowseLocationHandler` maps typed locations to items.
6. `IBrowseSessionModel` owns one browser pane's items and view settings.

The capability prototype includes:

- `model.Get<TCapability>()` over an owned `ICapabilitySet`;
- explicit contributor origin, priority, and ownership;
- contract-specific composers for thumbnail fallback, preview routing, and property merging;
- decorators, including a bounded in-memory thumbnail cache;
- a batch-oriented `IPropertyProvider` adapter for item-bound `IPropertySource` access;
- an item-bound `IFolderChangeSource` over a source-owned Windows Shell notification provider.

The Windows Shell provider supports file-system and virtual item resolution, versioned provider-defined item identity, managed PIDL descriptors, strict persisted-reference validation, snapshot-based files and folders, bounded streaming enumeration, file-system streams, apartment-safe virtual read streams, requested typed property extraction, PNG thumbnail extraction, and folder change subscriptions. `WindowsShellItemResolver` keeps Shell materialization and COM affinity in one boundary. `IWindowsShellScheduler` supplies injectable, message-pumped STA lanes for ordered metadata, independent concurrent extraction, and long operations.

Architecture documents are available in [`docs/architecture`](../../docs/architecture/README.md).

## Prototype boundaries

- The project targets Windows so it can eventually absorb `Files.App.CsWin32`, while the prototype code does not reference WinUI.
- Existing storage implementations remain untouched. `Files.Core` temporarily references `Files.App.CsWin32` for source-generated interop.
- Existing projects do not reference this prototype yet.
- Home, search, and tag locations are typed, but need separate handlers before they can be browsed.
- Persisted Windows filesystem references recover same-directory renames, but not cross-directory moves; reverse lookup by file ID remains a future Windows storage slice.
- Additional Windows property types, selection, history, operations, actions, and ViewModels remain future vertical slices.
- The eventual merge of `Files.Shared`, `Files.Core.Storage`, `Files.App.Storage`, and `Files.App.CsWin32` is intentionally separate from adopting this architecture.
