# Files.Core prototype

This project prototypes the new UI-agnostic model graph alongside the existing implementation.

The implemented vertical slice is:

1. `IStorageSource` owns provider-specific resolution and returns OwlCore.Storage core models.
2. `IStorableModelFactory` wraps core models as Files application models.
3. `IFilesDataRoot` composes configured sources and forms the root of the application model graph.
4. `IBrowseLocationHandler` maps typed locations to items.
5. `IBrowseSessionModel` owns the state of one browser pane.

`IThumbnailSource` is an independent optional capability. It deliberately does not inherit from `IStorable`.

The prototype now includes a Windows Shell provider with file-system and virtual item resolution, streaming folder enumeration, file-system streams, and virtual read streams.

Architecture documents are available in [`docs/architecture`](../../docs/architecture/README.md).

## Prototype boundaries

- The project targets Windows so it can eventually absorb `Files.App.CsWin32`, while the prototype code does not reference WinUI.
- Existing storage implementations remain untouched. `Files.Core` temporarily references `Files.App.CsWin32`, and adds only the `BHID_Stream` generator input needed by the new provider.
- Existing projects do not reference this prototype yet.
- Home, search, and tag locations are typed, but need separate handlers before they can be browsed.
- Selection, history, operations, actions, and ViewModels are intentionally outside this first vertical slice.
