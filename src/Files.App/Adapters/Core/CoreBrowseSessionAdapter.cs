// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.AppModels;
using Files.Core.Browsing;
using Files.Core.Composition;
using Files.Core.ItemFeatures.Thumbnails;
using Files.Core.Models;
using Files.Core.Storage;
using Files.Core.Storage.Windows;
using Files.Core.ViewSettings;
using System.IO;

namespace Files.App.Adapters.Core
{
	/// <summary>
	/// Adapts a Core pane to immutable snapshots consumed by the legacy WinUI list.
	/// </summary>
	internal sealed class CoreBrowseSessionAdapter : IDisposable
	{
		private const string ItemTypeProperty = "System.ItemTypeText";
		private const string SizeProperty = "System.Size";
		private const string DateModifiedProperty = "System.DateModified";
		private const string DateCreatedProperty = "System.DateCreated";
		private const int InitialViewportSize = 100;

		private static readonly BrowseViewSettings browseViewSettings = new(
			columns:
			[
				new ViewColumnSettings(ItemTypeProperty, 160, 0),
				new ViewColumnSettings(SizeProperty, 120, 1),
				new ViewColumnSettings(DateModifiedProperty, 160, 2),
				new ViewColumnSettings(DateCreatedProperty, 160, 3),
			],
			sortPropertyId: "System.ItemNameDisplay");

		private readonly PaneModel pane;
		private readonly FilesCoreRuntime runtime;
		private bool isDisposed;

		public CoreBrowseSessionAdapter(PaneModel pane, FilesCoreRuntime runtime)
		{
			ArgumentNullException.ThrowIfNull(pane);
			ArgumentNullException.ThrowIfNull(runtime);

			this.pane = pane;
			this.runtime = runtime;
			pane.BrowseSession.ItemsChanged += OnItemsChanged;
			pane.BrowseSession.ItemPresentationChanged += OnItemPresentationChanged;
			pane.BrowseSession.SelectionChanged += OnSelectionChanged;
			pane.BrowseSession.StateChanged += OnStateChanged;
		}

		public event EventHandler<CoreBrowseSnapshotEventArgs>? SnapshotChanged;

		public event EventHandler<CoreBrowsePresentationEventArgs>? PresentationChanged;

		public bool CanBrowse(string path)
		{
			return !string.IsNullOrWhiteSpace(path)
				&& Path.IsPathRooted(path)
				&& !path.EndsWith(".library-ms", StringComparison.OrdinalIgnoreCase)
				&& !path.StartsWith("tag:", StringComparison.OrdinalIgnoreCase)
				&& !path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase)
				&& !path.StartsWith("ftps:", StringComparison.OrdinalIgnoreCase)
				&& !path.StartsWith("ftpes:", StringComparison.OrdinalIgnoreCase);
		}

		public async ValueTask<CoreBrowseSnapshot> NavigateAsync(
			string path,
			CancellationToken cancellationToken = default)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			if (!CanBrowse(path))
			{
				throw new NotSupportedException(
					$"The location '{path}' is not handled by the Core Windows adapter.");
			}

			var address = new StorageAddress(
				WindowsStorageSource.FileAddressScheme,
				path);
			await using var resolved = await runtime.DataRoot
				.ResolveAsync(address, cancellationToken)
				.ConfigureAwait(false);
			if (resolved is not IFolderModel)
			{
				throw new IOException($"The location '{path}' is not a folder.");
			}

			await pane
				.NavigateAsync(
					new FolderLocation(resolved.Reference),
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			await pane.BrowseSession
				.UpdateViewSettingsAsync(browseViewSettings, cancellationToken)
				.ConfigureAwait(false);

			var itemCount = pane.BrowseSession.Items.Count;
			pane.UpdateViewport(
				new BrowseViewport(
					0,
					Math.Min(itemCount, InitialViewportSize),
					lookAheadCount: 20));
			return CreateSnapshot(path);
		}

		public void UpdateViewport(
			int firstVisibleIndex,
			int visibleCount,
			int lookAheadCount = 20)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			pane.UpdateViewport(
				new BrowseViewport(
					firstVisibleIndex,
					visibleCount,
					lookAheadCount));
		}

		public CoreBrowseSnapshot CaptureSnapshot(string? fallbackAddress = null)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			return CreateSnapshot(fallbackAddress);
		}

		public async ValueTask DeactivateAsync(
			CancellationToken cancellationToken = default)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			if (pane.Location is null or HomeLocation)
				return;

			await pane.NavigateAsync(
				HomeLocation.Instance,
				PaneNavigationMode.Replace,
				cancellationToken).ConfigureAwait(false);
		}

		public void SetSelection(
			IEnumerable<StorableKey> selectedKeys,
			StorableKey? focusedKey)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			ArgumentNullException.ThrowIfNull(selectedKeys);

			var keys = selectedKeys.Distinct().ToArray();
			pane.BrowseSession.SetSelection(
				keys,
				focusedKey,
				focusedKey);
		}

		public async ValueTask<bool> RenameAsync(
			StorableReference item,
			string newName,
			CancellationToken cancellationToken = default)
		{
			ObjectDisposedException.ThrowIf(isDisposed, this);
			ArgumentNullException.ThrowIfNull(item);
			ArgumentException.ThrowIfNullOrWhiteSpace(newName);

			var previousVersion = pane.BrowseSession.ItemsVersion;
			var result = await runtime.StorageOperations
				.ExecuteAsync(
					new RenameOperationRequest(item, newName),
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			if (!result.Succeeded)
			{
				return false;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken)
				.ConfigureAwait(false);
			if (pane.BrowseSession.ItemsVersion == previousVersion)
			{
				await pane.RefreshAsync(cancellationToken).ConfigureAwait(false);
			}

			return true;
		}

		public void Dispose()
		{
			if (isDisposed)
			{
				return;
			}

			isDisposed = true;
			pane.BrowseSession.ItemsChanged -= OnItemsChanged;
			pane.BrowseSession.ItemPresentationChanged -= OnItemPresentationChanged;
			pane.BrowseSession.SelectionChanged -= OnSelectionChanged;
			pane.BrowseSession.StateChanged -= OnStateChanged;
		}

		private CoreBrowseSnapshot CreateSnapshot(string? fallbackAddress = null)
		{
			var session = pane.BrowseSession;
			var address = GetLocationAddress(session.Location) ?? fallbackAddress ?? string.Empty;
			var items = session.Items
				.Select(item => CreateItemSnapshot(session, item))
				.ToArray();
			return new CoreBrowseSnapshot(
				address,
				session.Generation,
				session.ItemsVersion,
				Array.AsReadOnly(items),
				session.Selection,
				session.IsLoading,
				session.Error);
		}

		private static CoreBrowseItemSnapshot CreateItemSnapshot(
			IBrowseSessionModel session,
			IStorableModel item)
		{
			var key = item.Reference.GetKey();
			session.TryGetPresentation(key, out var presentation);
			return new CoreBrowseItemSnapshot(
				key,
				item.Reference,
				item.Name,
				GetItemAddress(item),
				item is IFolderModel,
				CloneProperties(presentation?.Properties),
				CloneThumbnail(presentation?.Thumbnail));
		}

		private static string GetItemAddress(IStorableModel item)
		{
			if (item.CoreModel is WindowsStorable windowsItem)
			{
				return windowsItem.FileSystemPath ?? windowsItem.ParsingName;
			}

			return item.Reference.LastKnownAddress?.Value ?? item.Name;
		}

		private static string? GetLocationAddress(BrowseLocation? location)
		{
			return location is FolderLocation folder
				? folder.Folder.LastKnownAddress?.Value
				: null;
		}

		private static ThumbnailResult? CloneThumbnail(ThumbnailResult? thumbnail)
		{
			return thumbnail is null
				? null
				: new ThumbnailResult(
					thumbnail.Content.ToArray(),
					thumbnail.ContentType,
					thumbnail.IsFallback);
		}

		private static IReadOnlyDictionary<string, object?> CloneProperties(
			IReadOnlyDictionary<string, object?>? properties)
		{
			return properties is null
				? new Dictionary<string, object?>()
				: properties.ToDictionary(entry => entry.Key, entry => entry.Value);
		}

		private void OnItemsChanged(object? sender, BrowseItemsChangedEventArgs args)
		{
			if (!isDisposed)
			{
				SnapshotChanged?.Invoke(
					this,
					new CoreBrowseSnapshotEventArgs(
						CreateSnapshot(),
						synchronizeSelection: true));
			}
		}

		private void OnItemPresentationChanged(
			object? sender,
			BrowseItemPresentationChangedEventArgs args)
		{
			if (!isDisposed)
			{
				PresentationChanged?.Invoke(
					this,
					new CoreBrowsePresentationEventArgs(
						pane.BrowseSession.Generation,
						args.Key,
						CloneProperties(args.Presentation.Properties),
						CloneThumbnail(args.Presentation.Thumbnail)));
			}
		}

		private void OnSelectionChanged(object? sender, EventArgs args)
		{
			if (!isDisposed)
			{
				SnapshotChanged?.Invoke(
					this,
					new CoreBrowseSnapshotEventArgs(
						CreateSnapshot(),
						synchronizeSelection: true));
			}
		}

		private void OnStateChanged(object? sender, EventArgs args)
		{
			if (!isDisposed)
			{
				SnapshotChanged?.Invoke(
					this,
					new CoreBrowseSnapshotEventArgs(
						CreateSnapshot(),
						synchronizeSelection: false));
			}
		}
	}
}
