# Files.Core completion boundary

Files.Core is complete as the UI-independent foundation required to begin the
new Files.App. “Complete” here means the architectural contracts plus the
Windows and FTP vertical slices are usable end to end; it does not mean every
future storage source or Files feature is implemented.

## Ready for Files.App

| Area | Ready behavior |
| --- | --- |
| Model graph | Application, windows, tabs, split panes, browse sessions |
| Navigation | Home, folder, back, forward, up, refresh, bounded history |
| Archives | Shell-first browsing, SevenZip fallback, encrypted credentials, read-only entry streams |
| Items | Stable identity, immutable model replacement, selection |
| Presentation | View settings, sorting, item changes, viewport prefetch |
| Item features | Factories, per-contract combiners, wrappers, ownership |
| Thumbnails | Windows Shell PNG bytes, shared cache, invalidation |
| Properties | Item-bound and batch-source contracts, typed Windows values |
| Folder changes | Shared `SHChangeNotifyRegister` source and incremental updates |
| Previews | Stream results and Windows Shell preview sessions |
| Operations | Create, case-preserving rename, copy, move, Recycle Bin/permanent delete, collision policy |
| FTP | FTP/FTPS resolution, streams, properties, same-source mutations, previews and archive reuse |
| Composition | One builder/runtime with deterministic ownership |
| Quality | Unit tests, Windows integration tests, benchmarks, Core CI |

## Deliberate extension boundaries

These do not block the new Files.App:

- Search and tag have typed `BrowseLocation` values but require a chosen
  index/backend and corresponding location handler.
- Cloud, MTP, SFTP, and other sources implement the same source, item feature,
  location, and operation contracts as later vertical slices.
- FTP profiles are composed before `Build`. Runtime add/remove needs a mutable
  source registry with explicit ownership semantics.
- Cross-source copies such as Windows-to-FTP need a generic stream-transfer
  coordinator; the FTP source deliberately owns only same-source requests.
- Archive browsing and read streams are implemented. Compression,
  extract-all, entry mutations, split volumes, and archive operation
  progress remain separate operation-source work.
- Cold recovery of a moved Windows file from only an old same-volume address
  needs a file-ID index or `OpenFileById` strategy. Live operations already
  return an updated reference and watchers update open sessions.
- Windows property extraction currently covers the typed values used by the
  first details view. Additional canonical properties can be added to
  `WindowsPropertyReader` without changing AppModels.
- Context menus, Shell verbs, drag/drop data packages, sharing, and
  application activation are Files.App/platform adapters, not item storage
  item features. Their command, OLE, transfer, threading, and ownership design
  is specified in [Files.App command execution](commands.md) and
  [Clipboard, drag/drop, and Shell integration](platform-interactions.md).
- Durable view settings, window-session serialization, telemetry, and policy
  implementations belong to the application composition root.
- `Files.Core.Storage` and `Files.App.Storage` have been retired, and CsWin32
  generation now lives directly in `Files.Core`. Moving the remaining
  Files.App consumers and deciding the future of `Files.Shared` are separate
  migrations.

## Definition of done for the next session

The next session can start Files.App when:

1. `FilesCoreRuntime` is created once at process startup;
2. a persisted `IViewSettingsStore` and production preview policies are
   selected;
3. `WindowViewModel`, `TabViewModel`, and `PaneViewModel` adapt the existing
   AppModels;
4. one item-collection adapter applies versioned browse changes on the UI
   dispatcher;
5. thumbnail and preview presenters convert Core results into WinUI objects;
6. one window-scoped command manager adapts navigation and storage requests;
7. clipboard, drag/drop, and Shell sessions remain window/platform adapters;
8. ViewModels, commands, and preview/platform sessions are disposed before
   the runtime.

The concrete Files.App blueprint is in [New Files.App architecture](files-app.md).
