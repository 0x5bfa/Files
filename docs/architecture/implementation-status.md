# Files.Core completion boundary

Files.Core is complete as the UI-independent foundation required to begin the
new Files.App. “Complete” here means the architectural contracts and the
Windows vertical slice are usable end to end; it does not mean every future
storage provider or Files feature is implemented.

## Ready for Files.App

| Area | Ready behavior |
| --- | --- |
| Model graph | Application, windows, tabs, split panes, browse sessions |
| Navigation | Home, folder, back, forward, up, refresh, bounded history |
| Items | Stable identity, immutable model replacement, selection |
| Presentation | View settings, sorting, item changes, viewport prefetch |
| Capabilities | Contributors, per-contract composers, decorators, ownership |
| Thumbnails | Windows Shell PNG bytes, shared cache, invalidation |
| Properties | Item-bound and batch-provider contracts, typed Windows values |
| Folder changes | Shared `SHChangeNotifyRegister` provider and incremental updates |
| Previews | Stream results and Windows Shell preview sessions |
| Operations | Create, rename, copy, move, delete, collision policy |
| Composition | One builder/runtime with deterministic ownership |
| Quality | Unit tests, Windows integration tests, benchmarks, Core CI |

## Deliberate extension boundaries

These do not block the new Files.App:

- Search and tag have typed `BrowseLocation` values but require a chosen
  index/backend and corresponding location handler.
- FTP, archive, cloud, MTP, and other sources implement the same source,
  capability, location, and operation contracts as later vertical slices.
- Cold recovery of a moved Windows file from only an old same-volume address
  needs a file-ID index or `OpenFileById` strategy. Live operations already
  return an updated reference and watchers update open sessions.
- Windows property extraction currently covers the typed values used by the
  first details view. Additional canonical properties can be added to
  `WindowsPropertyProvider` without changing AppModels.
- Context menus, Shell verbs, drag/drop data packages, sharing, and
  application activation are Files.App/platform adapters, not item storage
  capabilities.
- Durable view settings, window-session serialization, telemetry, and policy
  implementations belong to the application composition root.
- The physical merge of `Files.Shared`, `Files.Core.Storage`,
  `Files.App.Storage`, and `Files.App.CsWin32` remains a mechanical migration
  after consumers move to the new contracts.

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
6. ViewModels and preview sessions are disposed before the runtime.

The concrete Files.App blueprint is in [New Files.App architecture](files-app.md).
