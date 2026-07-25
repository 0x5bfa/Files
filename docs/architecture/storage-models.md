# Storage model boundaries

## CoreModels and AppModels

CoreModels standardize one provider. For storage, the smallest CoreModels are the OwlCore.Storage interfaces such as `IStorable`, `IFile`, and `IFolder`.

AppModels wrap CoreModels and add Files-specific composition. They do not expose WinUI concepts.

```mermaid
classDiagram
    class IStorageSource {
        +SourceId
        +GetRootsAsync()
        +ResolveAsync()
    }
    class IStorable {
        +Id
        +Name
    }
    class IFile
    class IFolder
    class IStorableModel {
        +CoreModel
        +Reference
        +Capabilities
    }
    class ICapabilitySet {
        +Get~T~()
        +TryGet~T~()
    }

    IStorable <|-- IFile
    IStorable <|-- IFolder
    IStorageSource --> IStorable : resolves
    IStorableModel --> IStorable : wraps
    IStorableModel --> ICapabilitySet : owns
```

`IStorageSource` is not an `IStorable`. It represents a configured connection or namespace capable of producing storage items. A Windows Shell namespace, an FTP account, and an opened archive are storage sources. Their child files and folders are storables.

## Identity and addresses

Three values have different jobs:

| Type | Meaning |
| --- | --- |
| `StorageSourceId` | Stable identity of a configured source |
| `IStorable.Id` | Provider-defined identity within that source |
| `StorageAddress` | An address that a source may resolve |

`StorableReference` combines the source ID and item ID. Its equality and hash
code deliberately use only those two values. `LastKnownAddress` is an
optional recovery hint and never participates in identity.

Windows filesystem items use the versioned `winfs:v1:<volume>:<file-index>` identity when available. Their current `StorageAddress` uses the `file:` scheme and filesystem path, while their Shell parsing name and managed absolute PIDL remain separate locators. Items without a filesystem path use a `shell:` address. Virtual or inaccessible items use the encoded `winshell-address:v1:<address>` identity fallback when a filesystem ID is unavailable.

Resolution validates the resulting identity and rejects a different file that
has replaced a stale address. The Windows source can cold-resolve a
same-directory rename by scanning the previous parent. Cross-directory cold
lookup requires a volume file-ID index or `OpenFileById` strategy; live
operations return the updated reference and open sessions receive the move
through their watcher.

```mermaid
flowchart LR
    SourceId["StorageSourceId"]
    ItemId["IStorable.Id"]
    Address["LastKnownAddress"]
    Reference["StorableReference"]

    SourceId --> Reference
    ItemId --> Reference
    Address -. fallback .-> Reference
```

## Optional capabilities

Capabilities remain independent interfaces. A concrete CoreModel may directly implement both `IStorable` and a capability, but the capability does not inherit from `IStorable`.

```csharp
public interface IThumbnailSource
{
	ValueTask<ThumbnailResult?> GetThumbnailAsync(
		ThumbnailRequest request,
		CancellationToken cancellationToken = default);
}

public interface IPropertySource
{
	ValueTask<IReadOnlyDictionary<string, object?>> GetPropertiesAsync(
		PropertyRequest request,
		CancellationToken cancellationToken = default);
}
```

An implementation may instead come from a source adapter, a cache decorator, or a plugin. The `CapabilityPipeline` composes those candidates once and stores the result in the AppModel's `ICapabilitySet`.

```mermaid
flowchart LR
    Source["IStorageSource"]
    Core["IStorable CoreModel"]
    Factory["StorableModelFactory"]
    Pipeline["CapabilityPipeline"]
    Model["IStorableModel"]

    Source --> Core
    Source --> Factory
    Core --> Factory
    Factory --> Pipeline
    Pipeline --> Model
    Factory --> Model
```

See [Capability composition](capabilities.md) for resolution, multiple providers, decorators, and ownership.

## Ownership

`IStorableModelFactory` transfers ownership of a newly supplied CoreModel to
the returned AppModel. The AppModel asynchronously disposes its capability set
before disposing the CoreModel. If a capability or CoreModel supports only
`IDisposable`, that synchronous cleanup runs inside the same ordered
disposal.

Browse-session replacement, refresh, incremental delete/rename/update, failed
navigation, and session shutdown all await `IStorableModel.DisposeAsync`.
Cleanup attempts every owned item and aggregates failures instead of
abandoning the remaining models. Synchronous `Dispose` members are
compatibility bridges; Files.App must keep disposal asynchronous on the UI
thread.

Storage sources and shared services have a longer lifetime and are owned by `FilesDataRoot` or the application composition root. This keeps native resources bounded by the model graph rather than the visual tree.
