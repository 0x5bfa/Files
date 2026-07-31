// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Browsing;
using Files.Core.ItemFeatures.Thumbnails;
using Files.Core.Storage;

namespace Files.App.Adapters.Core
{
	internal sealed record CoreBrowseItemSnapshot(
		StorableKey Key,
		StorableReference Reference,
		string Name,
		string Address,
		bool IsFolder,
		IReadOnlyDictionary<string, object?> Properties,
		ThumbnailResult? Thumbnail);

	internal sealed record CoreBrowseSnapshot(
		string Address,
		long Generation,
		long ItemsVersion,
		IReadOnlyList<CoreBrowseItemSnapshot> Items,
		BrowseSelectionState Selection,
		bool IsLoading,
		Exception? Error);

	internal sealed class CoreBrowseSnapshotEventArgs : EventArgs
	{
		public CoreBrowseSnapshotEventArgs(
			CoreBrowseSnapshot snapshot,
			bool synchronizeSelection)
		{
			Snapshot = snapshot;
			SynchronizeSelection = synchronizeSelection;
		}

		public CoreBrowseSnapshot Snapshot { get; }

		public bool SynchronizeSelection { get; }
	}

	internal sealed class CoreBrowsePresentationEventArgs : EventArgs
	{
		public CoreBrowsePresentationEventArgs(
			long generation,
			StorableKey key,
			IReadOnlyDictionary<string, object?> properties,
			ThumbnailResult? thumbnail)
		{
			Generation = generation;
			Key = key;
			Properties = properties;
			Thumbnail = thumbnail;
		}

		public long Generation { get; }

		public StorableKey Key { get; }

		public IReadOnlyDictionary<string, object?> Properties { get; }

		public ThumbnailResult? Thumbnail { get; }
	}
}
