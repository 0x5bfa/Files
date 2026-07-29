// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures.Changes;

/// <summary>
/// Provides an error raised by a folder change notification pump.
/// </summary>
public sealed class FolderChangeErrorEventArgs : EventArgs
{
	public FolderChangeErrorEventArgs(Exception error)
	{
		Error = error ?? throw new ArgumentNullException(nameof(error));
	}

	public Exception Error { get; }
}
