// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities.Changes;
using Files.Core.Storage;
using Windows.Win32.UI.Shell;

namespace Files.Core.Storage.Windows;

internal sealed class WindowsFolderChangeSource : IFolderChangeSource
{
	private readonly WindowsStorageSource source;
	private readonly WindowsItemLocator folderLocator;
	private readonly CancellationTokenSource lifetime = new();
	private int isDisposed;

	public WindowsFolderChangeSource(
		WindowsStorageSource source,
		WindowsItemLocator folderLocator)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(folderLocator);

		this.source = source;
		this.folderLocator = folderLocator;
	}

	public IAsyncEnumerable<FolderChange> WatchAsync(
		CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);
		return new ChangeEnumerable(this, cancellationToken);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) != 0)
		{
			return;
		}

		lifetime.Cancel();
		lifetime.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task<FolderChange> ConvertAsync(
		WindowsShellChange change,
		CancellationToken cancellationToken)
	{
		var kind = GetKind(change.EventId);
		WindowsStorable? first = null;
		WindowsStorable? second = null;

		if (kind is FolderChangeKind.Renamed)
		{
			first = await source
				.TryCreateFromAbsolutePidlAsync(
					change.FirstAbsolutePidl,
					cancellationToken)
				.ConfigureAwait(false);
			second = await source
				.TryCreateFromAbsolutePidlAsync(
					change.SecondAbsolutePidl,
					cancellationToken)
				.ConfigureAwait(false);
		}
		else if (kind is not FolderChangeKind.DirectoryUpdated)
		{
			first = await source
				.TryCreateFromAbsolutePidlAsync(
					change.FirstAbsolutePidl,
					cancellationToken)
				.ConfigureAwait(false);
		}

		var currentItem = kind is FolderChangeKind.Deleted
			? null
			: CreateReference(second ?? first);
		var previousItem = kind is FolderChangeKind.Deleted or FolderChangeKind.Renamed
			? CreateReference(first)
			: null;

		return new FolderChange(
			kind,
			currentItem,
			previousItem,
			kind is FolderChangeKind.DirectoryUpdated
				|| (kind is FolderChangeKind.Renamed
					? second is null
					: first is null));
	}

	private StorableReference? CreateReference(WindowsStorable? storable)
	{
		return storable is null
			? null
			: new StorableReference(source.SourceId, storable.Id, storable.Address);
	}

	private static FolderChangeKind GetKind(SHCNE_ID eventId)
	{
		if ((eventId & (SHCNE_ID.SHCNE_RENAMEITEM | SHCNE_ID.SHCNE_RENAMEFOLDER)) != 0)
		{
			return FolderChangeKind.Renamed;
		}

		if ((eventId & (SHCNE_ID.SHCNE_CREATE | SHCNE_ID.SHCNE_MKDIR)) != 0)
		{
			return FolderChangeKind.Created;
		}

		if ((eventId & (SHCNE_ID.SHCNE_DELETE | SHCNE_ID.SHCNE_RMDIR)) != 0)
		{
			return FolderChangeKind.Deleted;
		}

		if ((eventId & SHCNE_ID.SHCNE_UPDATEDIR) != 0)
		{
			return FolderChangeKind.DirectoryUpdated;
		}

		return FolderChangeKind.Updated;
	}

	private sealed class ChangeEnumerable : IAsyncEnumerable<FolderChange>
	{
		private readonly WindowsFolderChangeSource owner;
		private readonly CancellationToken cancellationToken;

		public ChangeEnumerable(
			WindowsFolderChangeSource owner,
			CancellationToken cancellationToken)
		{
			this.owner = owner;
			this.cancellationToken = cancellationToken;
		}

		public IAsyncEnumerator<FolderChange> GetAsyncEnumerator(
			CancellationToken cancellationToken = default)
		{
			var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken,
				owner.lifetime.Token,
				this.cancellationToken);

			return new ChangeEnumerator(owner, linkedCancellation);
		}
	}

	private sealed class ChangeEnumerator : IAsyncEnumerator<FolderChange>
	{
		private readonly WindowsFolderChangeSource owner;
		private readonly CancellationTokenSource cancellation;
		private WindowsShellChangeProvider.WindowsShellChangeSubscription? subscription;
		private int isDisposed;

		public ChangeEnumerator(
			WindowsFolderChangeSource owner,
			CancellationTokenSource cancellation)
		{
			this.owner = owner;
			this.cancellation = cancellation;
		}

		public FolderChange Current { get; private set; } = null!;

		public async ValueTask<bool> MoveNextAsync()
		{
			ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) != 0, this);

			subscription ??= await owner.source.ChangeProvider
				.SubscribeAsync(
					owner.folderLocator,
					recursive: false,
					cancellation.Token)
				.ConfigureAwait(false);

			while (await subscription
				.WaitToReadAsync(cancellation.Token)
				.ConfigureAwait(false))
			{
				while (subscription.TryRead(out var change))
				{
					Current = await owner
						.ConvertAsync(change, cancellation.Token)
						.ConfigureAwait(false);
					return true;
				}
			}

			return false;
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref isDisposed, 1) != 0)
			{
				return;
			}

			cancellation.Cancel();

			if (subscription is not null)
			{
				await subscription.DisposeAsync().ConfigureAwait(false);
			}

			cancellation.Dispose();
		}
	}
}
