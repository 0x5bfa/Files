// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.ViewModels;
using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Data;
using Files.Core.Models;
using Files.Core.Storage;
using Microsoft.UI.Dispatching;

namespace Files.App2.Adapters;

internal sealed class CoreBrowseAdapter : IDisposable
{
	private readonly PaneModel pane;
	private readonly IFilesDataRoot dataRoot;
	private readonly DispatcherQueue dispatcherQueue;
	private readonly CancellationTokenSource lifetime = new();
	private long appliedGeneration = -1;
	private long appliedItemsVersion = -1;
	private int isDisposed;

	public CoreBrowseAdapter(
		PaneModel pane,
		IFilesDataRoot dataRoot,
		DispatcherQueue dispatcherQueue)
	{
		ArgumentNullException.ThrowIfNull(pane);
		ArgumentNullException.ThrowIfNull(dataRoot);
		ArgumentNullException.ThrowIfNull(dispatcherQueue);

		this.pane = pane;
		this.dataRoot = dataRoot;
		this.dispatcherQueue = dispatcherQueue;

		Items = Array.Empty<BrowseItemViewModel>();
		SelectedKeys = Array.Empty<StorableKey>();

		pane.StateChanged += Pane_StateChanged;
		pane.BrowseSession.ItemsChanged += BrowseSession_ItemsChanged;
		pane.BrowseSession.SelectionChanged += BrowseSession_SelectionChanged;

		QueueSnapshot();
	}

	public IReadOnlyList<BrowseItemViewModel> Items { get; private set; }

	public IReadOnlyList<StorableKey> SelectedKeys { get; private set; }

	public string LocationText { get; private set; } = "Home";

	public string? ErrorMessage { get; private set; }

	public bool IsLoading { get; private set; }

	public bool CanGoBack => pane.CanGoBack;

	public bool CanGoForward => pane.CanGoForward;

	public bool CanGoUp => pane.CanGoUp;

	public string StatusText =>
		ErrorMessage
		?? (IsLoading
			? "Loading..."
			: $"{Items.Count} item{(Items.Count == 1 ? string.Empty : "s")}");

	public event EventHandler? Updated;

	public async Task InitializeAsync(CancellationToken cancellationToken = default)
	{
		EnsureActive();
		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.NavigateAsync(
			HomeLocation.Instance,
			cancellationToken: linkedCancellation.Token).ConfigureAwait(false);
	}

	public async Task NavigateToPathAsync(
		string path,
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (string.Equals(path, "Home", StringComparison.OrdinalIgnoreCase))
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		var model = await dataRoot.ResolveAsync(
			new StorageAddress("file", path),
			linkedCancellation.Token).ConfigureAwait(false);
		try
		{
			if (model is not IFolderModel)
			{
				throw new InvalidOperationException(
					$"The location '{path}' is not a folder.");
			}

			await pane.NavigateAsync(
				new FolderLocation(model.Reference),
				cancellationToken: linkedCancellation.Token).ConfigureAwait(false);
		}
		finally
		{
			await model.DisposeAsync().ConfigureAwait(false);
		}
	}

	public async Task NavigateToItemAsync(
		BrowseItemViewModel item,
		CancellationToken cancellationToken = default)
	{
		EnsureActive();
		ArgumentNullException.ThrowIfNull(item);
		if (!item.IsFolder)
		{
			return;
		}

		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.NavigateAsync(
			new FolderLocation(item.Reference),
			cancellationToken: linkedCancellation.Token).ConfigureAwait(false);
	}

	public async Task GoBackAsync(CancellationToken cancellationToken = default)
	{
		EnsureActive();
		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.GoBackAsync(linkedCancellation.Token).ConfigureAwait(false);
	}

	public async Task GoForwardAsync(CancellationToken cancellationToken = default)
	{
		EnsureActive();
		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.GoForwardAsync(linkedCancellation.Token).ConfigureAwait(false);
	}

	public async Task GoUpAsync(CancellationToken cancellationToken = default)
	{
		EnsureActive();
		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.GoUpAsync(linkedCancellation.Token).ConfigureAwait(false);
	}

	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		EnsureActive();
		using var linkedCancellation = CreateLinkedCancellation(cancellationToken);
		await pane.RefreshAsync(linkedCancellation.Token).ConfigureAwait(false);
	}

	public void SetSelection(IEnumerable<BrowseItemViewModel> selectedItems)
	{
		EnsureActive();
		ArgumentNullException.ThrowIfNull(selectedItems);

		var selectedKeys = selectedItems
			.Select(static item => item.Reference.GetKey())
			.ToArray();
		var focusedKey = selectedKeys.FirstOrDefault();
		pane.BrowseSession.SetSelection(
			selectedKeys,
			selectedKeys.Length is 0 ? null : focusedKey,
			selectedKeys.Length is 0 ? null : focusedKey);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref isDisposed, 1) is not 0)
		{
			return;
		}

		pane.StateChanged -= Pane_StateChanged;
		pane.BrowseSession.ItemsChanged -= BrowseSession_ItemsChanged;
		pane.BrowseSession.SelectionChanged -= BrowseSession_SelectionChanged;
		lifetime.Cancel();
		lifetime.Dispose();
		Updated = null;
	}

	private void Pane_StateChanged(object? sender, EventArgs args) => QueueSnapshot();

	private void BrowseSession_ItemsChanged(
		object? sender,
		BrowseItemsChangedEventArgs args) => QueueSnapshot();

	private void BrowseSession_SelectionChanged(object? sender, EventArgs args) => QueueSnapshot();

	private void QueueSnapshot()
	{
		if (Volatile.Read(ref isDisposed) is not 0)
		{
			return;
		}

		var snapshot = CreateSnapshot();
		if (!dispatcherQueue.TryEnqueue(() => ApplySnapshot(snapshot)))
		{
			if (Volatile.Read(ref isDisposed) is 0)
			{
				throw new InvalidOperationException(
					"The Files.App2 UI dispatcher rejected a Core update.");
			}
		}
	}

	private CoreBrowseSnapshot CreateSnapshot()
	{
		var session = pane.BrowseSession;
		var items = session.Items
			.Select(static item => new BrowseItemViewModel(
				item.Name,
				item is IFolderModel,
				item.Reference))
			.ToArray();
		var selection = session.Selection;

		return new CoreBrowseSnapshot(
			session.Generation,
			session.ItemsVersion,
			session.IsLoading,
			session.Error?.Message,
			GetLocationText(session.Location),
			items,
			selection.SelectedKeys.ToArray());
	}

	private void ApplySnapshot(CoreBrowseSnapshot snapshot)
	{
		if (Volatile.Read(ref isDisposed) is not 0
			|| snapshot.Generation < appliedGeneration
			|| (snapshot.Generation == appliedGeneration
				&& snapshot.ItemsVersion < appliedItemsVersion))
		{
			return;
		}

		var shouldApplyItems =
			snapshot.Generation > appliedGeneration
			|| snapshot.ItemsVersion > appliedItemsVersion;
		if (shouldApplyItems)
		{
			Items = snapshot.Items;
			appliedGeneration = snapshot.Generation;
			appliedItemsVersion = snapshot.ItemsVersion;
		}

		SelectedKeys = snapshot.SelectedKeys;
		LocationText = snapshot.LocationText;
		ErrorMessage = snapshot.ErrorMessage;
		IsLoading = snapshot.IsLoading;
		Updated?.Invoke(this, EventArgs.Empty);
	}

	private CancellationTokenSource CreateLinkedCancellation(
		CancellationToken cancellationToken) =>
		CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			lifetime.Token);

	private static string GetLocationText(BrowseLocation? location)
	{
		return location switch
		{
			HomeLocation => "Home",
			FolderLocation folder when folder.Folder.LastKnownAddress is
				{ Scheme: var scheme, Value: var value }
				&& string.Equals(scheme, "file", StringComparison.OrdinalIgnoreCase)
				=> value,
			FolderLocation folder => folder.Folder.LastKnownAddress?.ToString()
				?? folder.Folder.ItemId,
			_ => location?.GetType().Name ?? "Home",
		};
	}

	private void EnsureActive() =>
		ObjectDisposedException.ThrowIf(Volatile.Read(ref isDisposed) is not 0, this);

	private sealed record CoreBrowseSnapshot(
		long Generation,
		long ItemsVersion,
		bool IsLoading,
		string? ErrorMessage,
		string LocationText,
		IReadOnlyList<BrowseItemViewModel> Items,
		IReadOnlyList<StorableKey> SelectedKeys);
}
