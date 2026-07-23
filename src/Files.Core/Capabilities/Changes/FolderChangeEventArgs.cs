// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Changes;

/// <summary>
/// Provides a managed folder change to an event subscriber.
/// </summary>
public sealed class FolderChangeEventArgs : EventArgs
{
	public FolderChangeEventArgs(FolderChange change)
	{
		Change = change ?? throw new ArgumentNullException(nameof(change));
	}

	public FolderChange Change { get; }
}
