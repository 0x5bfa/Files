// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Watches changes for the folder bound to a capability instance.
/// The synchronous disposal member is retained so the current model capability
/// ownership pipeline can release the source; callers with an async lifetime
/// should prefer <see cref="IAsyncDisposable.DisposeAsync"/>.
/// </summary>
public interface IFolderChangeSource : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Raised after a Shell change has been converted to a managed folder change.
	/// </summary>
	event EventHandler<FolderChangeEventArgs>? Changed;

	/// <summary>
	/// Raised when the background notification pump cannot continue.
	/// </summary>
	event EventHandler<FolderChangeErrorEventArgs>? Faulted;

	/// <summary>
	/// Starts the native folder subscription and its change pump.
	/// </summary>
	ValueTask StartAsync(
		CancellationToken cancellationToken = default);
}
