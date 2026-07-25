# Storage operations

Storage mutation is request-based and provider-routed. Files.App constructs a
UI-independent request from stable references; `StorageOperationService`
selects the first provider that accepts it.

## Contracts

The Core operation set is:

| Request | Result |
| --- | --- |
| `CreateItemOperationRequest` | Reference to the new file or folder |
| `RenameOperationRequest` | New snapshot reference for the same logical item |
| `CopyOperationRequest` | Reference to the copy |
| `MoveOperationRequest` | Reference at the destination |
| `DeleteOperationRequest` | No result reference |

Create, copy, and move accept `StorageConflictBehavior.Fail` or
`GenerateUniqueName`. Delete defaults to the Recycle Bin; permanent deletion
must be explicit.

`StorageOperationResult` cannot represent contradictory state: success has no
error, while failure has an error and no result item. Progress values are
non-negative and `CompletedItems` cannot exceed a known `TotalItems`.

`IStorageOperationService.CanHandle` is a cheap command-enablement check for a
fully formed request. It does not guarantee later success: permissions,
connectivity, collisions, or identity may change before execution.

## Flow

```mermaid
sequenceDiagram
    participant VM as Command adapter
    participant Service as Operation service
    participant Provider as Storage provider
    participant Backend as Backend API
    participant Watcher as Folder watcher

    VM->>Service: ExecuteAsync(request)
    Service->>Provider: CanHandle(request)
    Service->>Provider: ExecuteAsync(request)
    Provider->>Backend: perform mutation
    Backend-->>Provider: completion
    Provider-->>Service: result reference
    Service-->>VM: StorageOperationResult
    Watcher-->>VM: browse-session update
```

`Succeeded == false` carries the error as data. Cancellation requested by the
caller is the exception: `OperationCanceledException` propagates so the
command can distinguish cancellation from a failed operation.

## Windows implementation

`WindowsStorageOperationProvider` uses `IFileOperation` on the dedicated
operation STA. It supports:

- filesystem create;
- filesystem rename;
- filesystem copy and move;
- Shell delete for filesystem or virtual items.

Name validation rejects path traversal, separators, trailing spaces or dots,
invalid characters, and reserved DOS device names. Collision checks happen
before queuing the Shell operation. `GenerateUniqueName` uses the familiar
`name (2).ext` sequence.

Windows path comparison deliberately has two meanings:

- identity/collision comparison is case-insensitive;
- exact path spelling comparison is ordinal and case-sensitive.

Consequently, renaming `report.txt` to `REPORT.TXT` is not treated as a no-op.
When the case-insensitive destination already exists, the provider resolves it
and permits the rename only if its stable `ItemId` matches the source item.
Copy and move derive their default name from `FileSystemPath`, not the Shell
display name, because display names may hide a file extension.

```mermaid
flowchart TD
    Request["Operation request"]
    Resolve["Resolve stable references"]
    Validate["Validate target and name"]
    STA["Operation STA"]
    Shell["IFileOperation"]
    Materialize["Resolve actual result"]

    Request --> Resolve
    Resolve --> Validate
    Validate --> STA
    STA --> Shell
    Shell --> Materialize
```

Rename rechecks the item ID immediately before mutation and verifies that the
result path still identifies the expected item. Create, copy, and move
materialize the actual destination through the source rather than returning a
guessed path.

Non-permanent delete configures `IFileOperation` with both `FOF_ALLOWUNDO` and
`FOFX_RECYCLEONDELETE` (`0x00080000`). `FOF_ALLOWUNDO` alone only requests
undo preservation when possible; the extended flag explicitly requests the
Recycle Bin. Permanent delete sets neither flag. Both modes still materialize
completion through `PerformOperations` and inspect
`GetAnyOperationsAborted`.

Cancellation can prevent work that has not started. It cannot interrupt a
synchronous Shell extension already executing. After a side effect commits,
result materialization intentionally completes without the caller token so a
successful mutation is not reported as canceled and retried unsafely.

## Identity after mutation

An operation never mutates an existing `IStorableModel`. Folder notifications
or refresh replace the old snapshot with a new model.

- A same-volume rename normally retains `ItemId`.
- A move may retain or change `ItemId`, depending on provider semantics.
- A copy always represents a new item.
- `LastKnownAddress` is updated in the returned reference but is excluded
  from reference equality.

Files.App should discard captured model instances after completion and use
the returned reference only for focus/reveal intent. The browse session
remains authoritative for the displayed item collection.

## Multi-item commands

The provider contract intentionally operates on one request. Files.App may
run a bounded sequence for multi-selection and aggregate progress, or a
backend may add a specialized batch request/provider later. Keeping the
single-item semantic stable avoids pretending that all providers support one
atomic bulk transaction.

For a Files.App batch adapter:

1. capture all selected `StorableReference` values;
2. execute with bounded concurrency appropriate to the provider;
3. stop scheduling new items when canceled;
4. retain per-item failures rather than losing partial success;
5. let folder notifications reconcile the visible session;
6. use result references only for final focus/reveal.
