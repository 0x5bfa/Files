// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Browsing;

/// <summary>
/// Describes the visible item range and the number of surrounding items to prefetch.
/// </summary>
public sealed record BrowseViewport
{
	public BrowseViewport(
		int firstVisibleIndex,
		int visibleCount,
		int lookAheadCount = 20)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(firstVisibleIndex);
		ArgumentOutOfRangeException.ThrowIfNegative(visibleCount);
		ArgumentOutOfRangeException.ThrowIfNegative(lookAheadCount);

		FirstVisibleIndex = firstVisibleIndex;
		VisibleCount = visibleCount;
		LookAheadCount = lookAheadCount;
	}

	public int FirstVisibleIndex { get; }

	public int VisibleCount { get; }

	/// <summary>
	/// Gets the maximum number of items prefetched on each side of the visible range.
	/// </summary>
	public int LookAheadCount { get; }
}
