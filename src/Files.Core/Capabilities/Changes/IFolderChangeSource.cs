// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Watches changes for the folder bound to a capability instance.
/// </summary>
public interface IFolderChangeSource : IDisposable
{
	/// <summary>
	/// Creates an independent subscription to folder changes.
	/// </summary>
	IAsyncEnumerable<FolderChange> WatchAsync(
		CancellationToken cancellationToken = default);
}
