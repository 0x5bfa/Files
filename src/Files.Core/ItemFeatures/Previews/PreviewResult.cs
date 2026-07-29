// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures.Previews;

/// <summary>
/// Base type for UI-neutral preview content.
/// </summary>
public abstract class PreviewResult : IAsyncDisposable
{
	public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
