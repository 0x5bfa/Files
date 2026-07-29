# `Files.App.Server` によるクラッシュ耐性のある操作

保証するのは、`Files.App` がクラッシュしても、別プロセスの `Files.App.Server` が実行中のファイル操作を継続することです。
サーバー、Windows、マシンの停止後に中断操作を自動再開しません。安全に再開できないコピーや移動を推測で再実行してはいけません。

1 項目の Core 契約は [ストレージ操作](operations.md) を参照してください。
class 内で本体を省略して `;` で終えている member は、追加するシグネチャを表します。

## 再考した構成

| 以前の案 | 採用する形 |
| --- | --- |
| サーバーにも完全な `FilesCoreRuntime` | 操作だけを持つ `StorageRuntime` |
| 永続 `OperationJob` とチェックポイント | メモリ内 `FileOperation` と再接続用 journal |
| 多数の public WinRT DTO | 1 つの `FileOperationServer` と JSON message |
| 切断で失われる WinRT event | 単調増加する `Revision` と long polling |
| `OperationSync` + `OperationCenterModel` | アプリケーションスコープの `FileOperationsModel` |
| Files プロセス数でサーバー終了 | active operation + active call + idle timeout |

```text
Files.App
  CommandHandler -> FileOperationsModel -> IFileOperationClient
                                      -> WinRtFileOperationClient
                                      -> FileOperationServer

Files.App.Server
  FileOperationServer -> FileOperationHost -> FileOperation
                                          -> OperationJournal
                                          -> StorageRuntime.Operations

Files.Core
  FileOperation message values
  StorageRuntime
  IStorageOperationService
  WindowsStorageOperationHandler
```

## Files.Core の message 値

WinRT runtime class は out-of-proc proxy です。数千個の DTO を public runtime class にすると、構築と property access が IPC になります。
ABI は JSON `string` にし、通常の C# record を両プロセスで共有します。

```csharp
namespace Files.Core.Storage.FileOperations;

public static class FileOperationSchema
{
	public const int Current = 1;
	public const int MaxItems = 4096;
	public const int MaxMessageBytes = 4 * 1024 * 1024;
	public const int MaxNameLength = 255;
	public const int MaxErrorDetailLength = 2048;
}

public enum FileOperationKind
{
	Create,
	Rename,
	Copy,
	Move,
	Delete,
}

public enum FileOperationState
{
	Queued,
	Running,
	Cancelling,
	Succeeded,
	CompletedWithErrors,
	Failed,
	Cancelled,
	Unknown,
}

public enum FileOperationItemState
{
	Queued,
	Running,
	Succeeded,
	Failed,
	Cancelled,
	Unknown,
}

public enum FileOperationErrorCode
{
	None,
	InvalidRequest,
	NotFound,
	AccessDenied,
	NameConflict,
	SourceUnavailable,
	NotSupported,
	Cancelled,
	ServerInterrupted,
	Unknown,
}
```

```csharp
public sealed record FileOperationReference(
	string SourceId,
	string ItemId,
	string? AddressScheme = null,
	string? AddressValue = null)
{
	public StorableReference ToReference()
	{
		var address = AddressScheme is not null
			&& AddressValue is not null
				? new StorageAddress(AddressScheme, AddressValue)
				: null;

		return new StorableReference(
			new StorageSourceId(SourceId),
			ItemId,
			address);
	}

	public static FileOperationReference FromReference(
		StorableReference reference) =>
		new(
			reference.SourceId.Value,
			reference.ItemId,
			reference.LastKnownAddress?.Scheme,
			reference.LastKnownAddress?.Value);
}

public sealed record FileOperationRequest(
	int SchemaVersion,
	string OperationId,
	FileOperationKind Kind,
	ImmutableArray<FileOperationReference> Items,
	FileOperationReference? DestinationFolder,
	string? Name,
	StorageItemKind? CreatedItemKind,
	StorageConflictBehavior ConflictBehavior,
	bool Permanently);
```

一覧取得では軽い summary、詳細画面では項目結果を含む snapshot を使います。

```csharp
public sealed record FileOperationItemSnapshot(
	int Index,
	FileOperationReference Input,
	FileOperationItemState State,
	FileOperationReference? Result,
	FileOperationErrorCode ErrorCode,
	string? ErrorDetail);

public sealed record FileOperationSummary(
	string OperationId,
	FileOperationKind Kind,
	FileOperationState State,
	int CompletedItems,
	int FailedItems,
	int TotalItems,
	FileOperationReference? CurrentItem,
	FileOperationErrorCode ErrorCode,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt);

public sealed record FileOperationSnapshot(
	FileOperationSummary Summary,
	ImmutableArray<FileOperationItemSnapshot> Items);

public sealed record FileOperationList(
	long Revision,
	ImmutableArray<FileOperationSummary> Operations);
```

```csharp
[JsonSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	WriteIndented = false)]
[JsonSerializable(typeof(FileOperationRequest))]
[JsonSerializable(typeof(FileOperationSummary))]
[JsonSerializable(typeof(FileOperationSnapshot))]
[JsonSerializable(typeof(FileOperationList))]
public sealed partial class FileOperationJsonContext
	: JsonSerializerContext;

public static class FileOperationMessages
{
	public static string Write<T>(
		T value,
		JsonTypeInfo<T> typeInfo) =>
		JsonSerializer.Serialize(value, typeInfo);

	public static T Read<T>(
		string json,
		JsonTypeInfo<T> typeInfo)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(json);
		if (Encoding.UTF8.GetByteCount(json)
			> FileOperationSchema.MaxMessageBytes)
		{
			throw new InvalidDataException(
				"The operation message is too large.");
		}

		return JsonSerializer.Deserialize(json, typeInfo)
			?? throw new InvalidDataException(
				"The operation message is empty.");
	}
}
```

## 操作専用 `StorageRuntime`

`Files.App.Server` はウィンドウ、AppModel、項目機能、サムネイル、プレビュー、アーカイブを構築しません。

```csharp
namespace Files.Core.Storage.Runtime;

public sealed class StorageRuntime : IAsyncDisposable
{
	private readonly IReadOnlyList<IAsyncDisposable> ownedServices;

	internal StorageRuntime(
		IStorageOperationService operations,
		IReadOnlyList<IAsyncDisposable> ownedServices)
	{
		Operations = operations;
		this.ownedServices = ownedServices;
	}

	public IStorageOperationService Operations { get; }

	public async ValueTask DisposeAsync()
	{
		foreach (var service in ownedServices.Reverse())
		{
			await service.DisposeAsync().ConfigureAwait(false);
		}
	}
}

public sealed class StorageRuntimeBuilder : IAsyncDisposable
{
	public StorageRuntimeBuilder AddHandler(
		IStorageOperationHandler handler);

	internal void Own(IAsyncDisposable service);

	public StorageRuntime Build();

	public ValueTask DisposeAsync();
}

public static class WindowsStorageRuntimeBuilderExtensions
{
	public static StorageRuntimeBuilder AddWindowsOperations(
		this StorageRuntimeBuilder builder,
		WindowsStorageSource? source = null)
	{
		var windowsSource = source ?? new WindowsStorageSource();
		builder.AddHandler(
			new WindowsStorageOperationHandler(windowsSource));

		if (source is null)
		{
			builder.Own(windowsSource);
		}

		return builder;
	}
}
```

```csharp
await using var storage = new StorageRuntimeBuilder()
	.AddWindowsOperations()
	.Build();

var operations = storage.Operations;
```

`FilesCoreBuilder.AddWindowsStorage()` はブラウザー用です。操作サーバーでは使いません。

## WinRT ABI

public WinRT class は 1 つです。event と public DTO class は作りません。

```csharp
namespace Files.App.Server;

public sealed class FileOperationServer
{
	public IAsyncOperation<string> StartAsync(string requestJson);

	public IAsyncOperation<string> GetAsync(string operationId);

	public IAsyncOperation<string> ListAsync();

	public IAsyncOperation<string> WaitForChangeAsync(
		long knownRevision);

	public IAsyncAction CancelAsync(string operationId);

	public IAsyncAction ForgetAsync(string operationId);
}
```

実装は薄い ABI adapter です。

```csharp
public IAsyncOperation<string> StartAsync(string requestJson)
{
	return AsyncInfo.Run(async _ =>
	{
		using var call = ServerProcess.Current.Lifetime.EnterCall();
		var request = FileOperationMessages.Read(
			requestJson,
			FileOperationJsonContext.Default.FileOperationRequest);
		var snapshot = await ServerProcess.Current.Operations.StartAsync(
			request,
			ServerProcess.Current.ShutdownToken);
		return FileOperationMessages.Write(
			snapshot,
			FileOperationJsonContext.Default.FileOperationSnapshot);
	});
}

public IAsyncOperation<string> WaitForChangeAsync(
	long knownRevision)
{
	return AsyncInfo.Run(async cancellationToken =>
	{
		using var call = ServerProcess.Current.Lifetime.EnterCall();
		var list = await ServerProcess.Current.Operations
			.WaitForChangeAsync(
				knownRevision,
				cancellationToken);
		return FileOperationMessages.Write(
			list,
			FileOperationJsonContext.Default.FileOperationList);
	});
}
```

`StartAsync` は client token を操作 token に使いません。受理後の処理は server-owned token で継続します。
`WaitForChangeAsync` のキャンセルは待機だけを止めます。

## サーバー合成ルート

WinRT activation は constructor injection を使えないため、`ServerProcess.Current` だけを明示的な process root とします。

```csharp
internal sealed class ServerProcess : IAsyncDisposable
{
	private static ServerProcess? current;
	private readonly CancellationTokenSource shutdown = new();
	private readonly StorageRuntime storage;

	public static ServerProcess Current =>
		current ?? throw new InvalidOperationException(
			"The server process has not been initialized.");

	public FileOperationHost Operations { get; }

	public ServerLifetime Lifetime { get; }

	public CancellationToken ShutdownToken => shutdown.Token;

	public static async Task<ServerProcess> CreateAsync(
		string dataPath,
		CancellationToken cancellationToken);

	public static void SetCurrent(ServerProcess process);

	public async ValueTask DisposeAsync()
	{
		shutdown.Cancel();
		await Operations.DisposeAsync();
		await storage.DisposeAsync();
		shutdown.Dispose();
		current = null;
	}
}
```

```csharp
static async Task Main()
{
	using var shutdown = new CancellationTokenSource();
	var dataPath = ApplicationData.Current.LocalFolder.Path;

	await using var process = await ServerProcess.CreateAsync(
		dataPath,
		shutdown.Token);
	ServerProcess.SetCurrent(process);

	using var registration = RegisterActivationFactories(
		[typeof(FileOperationServer)]);

	AppDomain.CurrentDomain.ProcessExit +=
		(_, _) => shutdown.Cancel();

	await process.Lifetime.WaitForExitAsync(shutdown.Token);
}
```

現在の public sealed class 全走査を explicit allowlist に置き換えます。
`AppInstanceMonitor` と `Files.App/Program.cs` の server kill は削除します。

```xml
<OutOfProcessServer
	ServerName="Files.App.Server"
	uap5:IdentityType="activateAsPackage"
	uap5:RunFullTrust="true">
	<Path>Files.App.Server\Files.App.Server.exe</Path>
	<Instancing>singleInstance</Instancing>
	<ActivatableClass
		ActivatableClassId="Files.App.Server.FileOperationServer" />
</OutOfProcessServer>
```

## request から Core request への変換

1 回のユーザー操作を、順序付きの Core request へ展開します。

```csharp
internal sealed record FileOperationStep(
	FileOperationReference Input,
	StorageOperationRequest Request);

internal sealed record FileOperationPlan(
	string OperationId,
	string RequestHash,
	FileOperationKind Kind,
	ImmutableArray<FileOperationStep> Steps);

internal static class FileOperationRequestReader
{
	public static FileOperationPlan Read(
		FileOperationRequest request);
}
```

```csharp
var steps = request.Kind switch
{
	FileOperationKind.Create =>
	[
		new FileOperationStep(
			request.DestinationFolder!,
			new CreateItemOperationRequest(
				destination!,
				request.Name!,
				request.CreatedItemKind!.Value,
				request.ConflictBehavior)),
	],

	FileOperationKind.Rename =>
	[
		new FileOperationStep(
			request.Items[0],
			new RenameOperationRequest(
				items[0],
				request.Name!)),
	],

	FileOperationKind.Copy =>
		items.Select((item, index) =>
			new FileOperationStep(
				request.Items[index],
				new CopyOperationRequest(
					item,
					destination!,
					request.Name,
					request.ConflictBehavior)))
			.ToImmutableArray(),

	FileOperationKind.Move =>
		items.Select((item, index) =>
			new FileOperationStep(
				request.Items[index],
				new MoveOperationRequest(
					item,
					destination!,
					request.Name,
					request.ConflictBehavior)))
			.ToImmutableArray(),

	FileOperationKind.Delete =>
		items.Select((item, index) =>
			new FileOperationStep(
				request.Items[index],
				new DeleteOperationRequest(
					item,
					request.Permanently)))
			.ToImmutableArray(),

	_ => throw new InvalidDataException(
		"Unknown operation kind."),
};
```

`FileOperationRequestReader.Read` は Core request を作る前に次を検証します。

```csharp
request.SchemaVersion == FileOperationSchema.Current
Guid.TryParseExact(request.OperationId, "N", out _)
request.Items.Length <= FileOperationSchema.MaxItems
request.Name?.Length <= FileOperationSchema.MaxNameLength

Create: Items.Length == 0 && DestinationFolder != null
Rename: Items.Length == 1 && DestinationFolder == null
Copy:   Items.Length >= 1 && DestinationFolder != null
Move:   Items.Length >= 1 && DestinationFolder != null
Delete: Items.Length >= 1 && DestinationFolder == null

Name != null for Create/Rename
Name == null || Items.Length == 1 for Copy/Move
```

意味のない余分な field も拒否します。request hash は操作種別、`SourceId`、`ItemId`、宛先、名前、競合動作、完全削除を canonical order で hash します。
`LastKnownAddress` は識別情報ではないため hash に含めません。

```csharp
internal static class FileOperationRequestHasher
{
	public static string Hash(FileOperationRequest request);
}
```

## `FileOperationHost`

`FileOperationHost` はサーバープロセスに 1 つです。dictionary の値を WinRT 境界へ返さず、不変 snapshot を返します。

```csharp
internal sealed class FileOperationHost : IAsyncDisposable
{
	private readonly Dictionary<string, Entry> entries =
		new(StringComparer.Ordinal);
	private readonly SemaphoreSlim stateGate = new(1, 1);
	private readonly SemaphoreSlim windowsExecutionGate =
		new(1, 1);
	private readonly IStorageOperationService operations;
	private readonly IOperationJournal journal;
	private readonly RevisionSignal changes;
	private readonly ServerLifetime lifetime;

	public static Task<FileOperationHost> CreateAsync(
		IStorageOperationService operations,
		IOperationJournal journal,
		ServerLifetime lifetime,
		CancellationToken cancellationToken);

	public Task<FileOperationSnapshot> StartAsync(
		FileOperationRequest request,
		CancellationToken serverToken);

	public Task<FileOperationSnapshot> GetAsync(
		string operationId,
		CancellationToken cancellationToken);

	public Task<FileOperationList> ListAsync(
		CancellationToken cancellationToken);

	public Task<FileOperationList> WaitForChangeAsync(
		long knownRevision,
		CancellationToken cancellationToken);

	public Task CancelAsync(
		string operationId,
		CancellationToken cancellationToken);

	public Task ForgetAsync(
		string operationId,
		CancellationToken cancellationToken);

	public ValueTask DisposeAsync();

	private sealed class Entry
	{
		public required string RequestHash { get; init; }
		public required FileOperationSnapshot Snapshot { get; set; }
		public FileOperation? ActiveOperation { get; set; }
	}
}
```

`StartAsync` の順序を変えてはいけません。

```csharp
var plan = FileOperationRequestReader.Read(request);

await stateGate.WaitAsync(serverToken);
try
{
	if (entries.TryGetValue(plan.OperationId, out var existing))
	{
		if (existing.RequestHash != plan.RequestHash)
		{
			throw new InvalidDataException(
				"OperationId is already used.");
		}

		return existing.Snapshot;
	}

	var operation = new FileOperation(
		plan,
		operations,
		windowsExecutionGate,
		OnOperationChangedAsync,
		serverToken);

	await journal.WriteAsync(
		new OperationJournalEntry(
			plan.RequestHash,
			operation.Snapshot),
		serverToken);

	entries.Add(
		plan.OperationId,
		new Entry
		{
			RequestHash = plan.RequestHash,
			Snapshot = operation.Snapshot,
			ActiveOperation = operation,
		});
	PublishState();
	operation.Start();
	return operation.Snapshot;
}
finally
{
	stateGate.Release();
}
```

同じ ID と同じ hash は既存 snapshot を返します。異なる hash は拒否します。
journal へ `Queued` を書く前に副作用を開始しません。

```csharp
private async ValueTask OnOperationChangedAsync(
	FileOperation operation,
	FileOperationSnapshot snapshot,
	bool isTerminal)
{
	await stateGate.WaitAsync();
	try
	{
		var entry = entries[snapshot.Summary.OperationId];
		if (!ReferenceEquals(entry.ActiveOperation, operation))
		{
			return;
		}

		entry.Snapshot = snapshot;
		if (isTerminal)
		{
			entry.ActiveOperation = null;
			await journal.WriteAsync(
				new OperationJournalEntry(
					entry.RequestHash,
					snapshot),
				CancellationToken.None);
		}

		changes.Pulse();
		lifetime.SetActiveOperationCount(
			entries.Values.Count(
				static value =>
					value.ActiveOperation is not null));
	}
	finally
	{
		stateGate.Release();
	}
}
```

`ForgetAsync` は terminal 状態だけを削除できます。`CancelAsync` は `FileOperation` の token だけを signal し、client call の token を保存しません。

## `FileOperation`

`FileOperation` は server-owned cancellation と 1 logical operation の状態を所有します。

```csharp
internal sealed class FileOperation
{
	private readonly FileOperationPlan plan;
	private readonly IStorageOperationService operations;
	private readonly SemaphoreSlim executionGate;
	private readonly SemaphoreSlim snapshotGate = new(1, 1);
	private readonly CancellationTokenSource cancellation;
	private readonly Func<
		FileOperation,
		FileOperationSnapshot,
		bool,
		ValueTask> publish;
	private Task? execution;

	public FileOperationSnapshot Snapshot { get; private set; }

	public Task Completion =>
		execution ?? Task.CompletedTask;

	public void Start() =>
		execution = RunAsync();

	public async ValueTask CancelAsync()
	{
		await UpdateAsync(
			snapshot =>
				FileOperationSnapshots.RequestCancellation(
					snapshot),
			isTerminal: false);
		cancellation.Cancel();
	}

	private async Task RunAsync()
	{
		var ownsGate = false;
		try
		{
			await executionGate.WaitAsync(cancellation.Token);
			ownsGate = true;
			await UpdateAsync(
				FileOperationSnapshots.Start,
				isTerminal: false);

			for (var index = 0;
				index < plan.Steps.Length;
				index++)
			{
				cancellation.Token.ThrowIfCancellationRequested();
				await UpdateAsync(
					value => FileOperationSnapshots.StartItem(
						value,
						index),
					isTerminal: false);

				var result = await operations.ExecuteAsync(
					plan.Steps[index].Request,
					cancellationToken: cancellation.Token);

				await UpdateAsync(
					value => FileOperationSnapshots.ApplyResult(
						value,
						index,
						result),
					isTerminal: false);
			}

			await UpdateAsync(
				FileOperationSnapshots.Complete,
				isTerminal: true);
		}
		catch (OperationCanceledException)
			when (cancellation.IsCancellationRequested)
		{
			await UpdateAsync(
				FileOperationSnapshots.Cancel,
				isTerminal: true);
		}
		catch (Exception error)
		{
			await UpdateAsync(
				value => FileOperationSnapshots.Fail(
					value,
					error),
				isTerminal: true);
		}
		finally
		{
			if (ownsGate)
			{
				executionGate.Release();
			}

			cancellation.Dispose();
		}
	}

	private async ValueTask UpdateAsync(
		Func<FileOperationSnapshot, FileOperationSnapshot> update,
		bool isTerminal)
	{
		await snapshotGate.WaitAsync();
		try
		{
			Snapshot = update(Snapshot);
			await publish(this, Snapshot, isTerminal);
		}
		finally
		{
			snapshotGate.Release();
		}
	}
}
```

snapshot の更新規則は 1 class に閉じ込めます。

```csharp
internal static class FileOperationSnapshots
{
	public static FileOperationSnapshot CreateQueued(
		FileOperationPlan plan);

	public static FileOperationSnapshot Start(
		FileOperationSnapshot snapshot);

	public static FileOperationSnapshot RequestCancellation(
		FileOperationSnapshot snapshot);

	public static FileOperationSnapshot StartItem(
		FileOperationSnapshot snapshot,
		int index);

	public static FileOperationSnapshot ApplyResult(
		FileOperationSnapshot snapshot,
		int index,
		StorageOperationResult result);

	public static FileOperationSnapshot Complete(
		FileOperationSnapshot snapshot);

	public static FileOperationSnapshot Cancel(
		FileOperationSnapshot snapshot);

	public static FileOperationSnapshot Fail(
		FileOperationSnapshot snapshot,
		Exception error);

	public static FileOperationSnapshot MarkUnknown(
		FileOperationSnapshot snapshot);
}
```

- 成功済み/失敗済みの項目結果を後のキャンセルで消さない。
- `Succeeded` は全項目成功、`CompletedWithErrors` は部分成功、`Failed` は成功なし。
- Shell が変更を確定した項目は、キャンセル要求後でも `Succeeded`。
- UI は `ErrorCode` をローカライズし、`ErrorDetail` を条件判定に使わない。

## revision、journal、サーバーライフタイム

```csharp
internal sealed class RevisionSignal
{
	public long Current { get; }

	public void Pulse();

	public Task<long> WaitAsync(
		long knownRevision,
		TimeSpan timeout,
		CancellationToken cancellationToken);
}
```

`Pulse` は revision を増やし、待機中の `TaskCompletionSource<long>` を完了させます。
`WaitForChangeAsync` は変更または 20 秒の timeout 後に完全な summary list を返します。

```csharp
internal sealed record OperationJournalEntry(
	string RequestHash,
	FileOperationSnapshot Snapshot);

internal interface IOperationJournal
{
	ValueTask<IReadOnlyList<OperationJournalEntry>> ReadAllAsync(
		CancellationToken cancellationToken);

	ValueTask WriteAsync(
		OperationJournalEntry entry,
		CancellationToken cancellationToken);

	ValueTask DeleteAsync(
		string operationId,
		CancellationToken cancellationToken);
}

internal sealed class JsonOperationJournal
	: IOperationJournal
{
	public JsonOperationJournal(string rootPath);

	public ValueTask<IReadOnlyList<OperationJournalEntry>>
		ReadAllAsync(CancellationToken cancellationToken);

	public ValueTask WriteAsync(
		OperationJournalEntry entry,
		CancellationToken cancellationToken);

	public ValueTask DeleteAsync(
		string operationId,
		CancellationToken cancellationToken);
}
```

保存先は `operations/v2/{operationId}.json` です。一時ファイルへ書き、同じ volume 上で atomic replace します。
書くのは受理時と terminal 状態だけです。実行 plan、資格情報、PIDL、stream、token は保存しません。

```csharp
private static FileOperationSnapshot Recover(
	FileOperationSnapshot snapshot) =>
	snapshot.Summary.State is
		FileOperationState.Succeeded
		or FileOperationState.CompletedWithErrors
		or FileOperationState.Failed
		or FileOperationState.Cancelled
		or FileOperationState.Unknown
			? snapshot
			: FileOperationSnapshots.MarkUnknown(snapshot);
```

`Unknown` を同じ ID で再送しても再実行しません。再試行には新しい `OperationId` が必要です。

```csharp
internal sealed class ServerLifetime
{
	public ServerLifetime(TimeSpan idleDelay);

	public IDisposable EnterCall();

	public void SetActiveOperationCount(int count);

	public Task WaitForExitAsync(
		CancellationToken cancellationToken);
}
```

終了条件:

```csharp
activeCalls == 0
	&& activeOperations == 0
	&& idleGenerationIsStillCurrent
```

新しい call または operation は idle timer の generation を無効化します。

## Files.App client と model

ViewModel は生成された WinRT class を直接保持しません。

```csharp
public interface IFileOperationClient : IAsyncDisposable
{
	ValueTask<FileOperationSnapshot> StartAsync(
		FileOperationRequest request,
		CancellationToken cancellationToken);

	ValueTask<FileOperationSnapshot> GetAsync(
		string operationId,
		CancellationToken cancellationToken);

	ValueTask<FileOperationList> ListAsync(
		CancellationToken cancellationToken);

	IAsyncEnumerable<FileOperationList> WatchAsync(
		long knownRevision,
		CancellationToken cancellationToken);

	ValueTask CancelAsync(
		string operationId,
		CancellationToken cancellationToken);

	ValueTask ForgetAsync(
		string operationId,
		CancellationToken cancellationToken);
}
```

```csharp
internal sealed class WinRtFileOperationClient
	: IFileOperationClient
{
	private Server.FileOperationServer server = new();

	public async ValueTask<FileOperationSnapshot> StartAsync(
		FileOperationRequest request,
		CancellationToken cancellationToken)
	{
		var requestJson = FileOperationMessages.Write(
			request,
			FileOperationJsonContext.Default.FileOperationRequest);
		var resultJson = await server
			.StartAsync(requestJson)
			.AsTask(cancellationToken);
		return FileOperationMessages.Read(
			resultJson,
			FileOperationJsonContext.Default.FileOperationSnapshot);
	}

	public async IAsyncEnumerable<FileOperationList> WatchAsync(
		long knownRevision,
		[EnumeratorCancellation]
		CancellationToken cancellationToken)
	{
		var revision = knownRevision;
		while (!cancellationToken.IsCancellationRequested)
		{
			var json = await server
				.WaitForChangeAsync(revision)
				.AsTask(cancellationToken);
			var list = FileOperationMessages.Read(
				json,
				FileOperationJsonContext.Default.FileOperationList);
			revision = list.Revision;
			yield return list;
		}
	}
}
```

実装では server disconnect を分類し、`new FileOperationServer()`、`ListAsync()`、上限付き backoff の順で再接続します。

```csharp
public sealed class FileOperationsModel : IAsyncDisposable
{
	private readonly IFileOperationClient client;
	private readonly CancellationTokenSource lifetime = new();
	private ImmutableDictionary<string, FileOperationSummary> items =
		ImmutableDictionary<string, FileOperationSummary>.Empty
			.WithComparers(StringComparer.Ordinal);
	private Task? watchTask;
	private long revision;

	public event EventHandler? Changed;

	public ImmutableArray<FileOperationSummary> Items { get; }

	public async Task StartAsync(
		CancellationToken cancellationToken)
	{
		Apply(await client.ListAsync(cancellationToken));
		watchTask ??= WatchAsync(lifetime.Token);
	}

	public async ValueTask<FileOperationSummary> SubmitAsync(
		FileOperationRequest request,
		CancellationToken cancellationToken)
	{
		var snapshot = await client.StartAsync(
			request,
			cancellationToken);
		Upsert(snapshot.Summary);
		return snapshot.Summary;
	}

	public ValueTask CancelAsync(
		string operationId,
		CancellationToken cancellationToken) =>
		client.CancelAsync(operationId, cancellationToken);

	private async Task WatchAsync(
		CancellationToken cancellationToken)
	{
		await foreach (var list in client.WatchAsync(
			revision,
			cancellationToken))
		{
			Apply(list);
		}
	}

	public ValueTask DisposeAsync();
}
```

`FileOperationsModel` は Files.App のアプリケーションスコープです。WinUI、observable collection、ローカライズ文字列を持ちません。
各ウィンドウの `FileOperationsViewModel` が dispatcher 上で observable collection へ適応し、Status Center へ依存関係プロパティで trickle down します。

```csharp
public sealed partial class FileOperationsViewModel
	: ObservableObject,
		IDisposable
{
	public FileOperationsViewModel(
		FileOperationsModel model,
		IUiDispatcher dispatcher);

	public ReadOnlyObservableCollection<FileOperationViewModel>
		Items { get; }

	public void Dispose();
}
```

## コマンドからの開始

削除確認、完全削除、競合動作、新しい名前、昇格、資格情報は Files.App で確定してから送信します。

```csharp
public async ValueTask ExecuteAsync(
	CommandContext context,
	CancellationToken cancellationToken)
{
	var destination = await folderPicker.PickAsync(
		context.WindowId,
		cancellationToken);
	if (destination is null)
	{
		return;
	}

	var request = new FileOperationRequest(
		SchemaVersion: FileOperationSchema.Current,
		OperationId: Guid.NewGuid().ToString("N"),
		Kind: FileOperationKind.Copy,
		Items: context.Selection
			.Select(FileOperationReference.FromReference)
			.ToImmutableArray(),
		DestinationFolder:
			FileOperationReference.FromReference(destination),
		Name: null,
		CreatedItemKind: null,
		ConflictBehavior:
			StorageConflictBehavior.GenerateUniqueName,
		Permanently: false);

	await operations.SubmitAsync(
		request,
		cancellationToken);
}
```

開始後にコマンドハンドラーが `BrowseSessionModel.Items` を変更してはいけません。
`IFolderChangeSource` と通常の参照セッション更新が表示を調整します。

## 最初の対応範囲

```text
Operation: copy, move, delete, create, rename
Source:    WindowsStorageSource
Queue:     Windows logical operation を 1 つずつ
Progress:  項目数。偽の byte percentage は出さない
History:   terminal snapshot を期限付きで保持
```

FTP はサーバーが保護された資格情報を自分で解決できるようになってから追加します。
アーカイブ変更、Quick Look、プレビュー、通常のファイルオープンはこのサーバーの責務ではありません。

## 実装順序

1. message record、JSON context、round-trip tests。
2. `StorageRuntimeBuilder.AddWindowsOperations()`。
3. request reader、hash、validation tests。
4. `FileOperationSnapshots`、`FileOperation`、`FileOperationHost`。
5. journal、revision、lifetime。
6. `FileOperationServer` と explicit activation allowlist。
7. WinRT client、`FileOperationsModel`、Status Center。
8. copy 1 本を end-to-end で移行。
9. move/delete/create/rename、複数選択。
10. `AppInstanceMonitor`、server kill、古い直接実行経路を削除。

## 受け入れ条件

```text
1. Files.App が StartAsync を呼ぶ。
2. server が Queued を journal へ書く。
3. server-owned token で Windows 操作を開始する。
4. Files.App を強制終了する。
5. Files.App.Server が操作を完了する。
6. 新しい Files.App が ListAsync で結果を取得する。
7. 表示中フォルダーは watcher から更新される。
```

```text
same OperationId + same hash      -> existing snapshot
same OperationId + different hash -> rejected
client disconnect                 -> operation continues
server restart + non-terminal     -> Unknown, never auto-resumed
partial failure                   -> per-item result remains
active operation                  -> idle exit is impossible
```
