// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Browsing;

/// <summary>
/// Describes the visible item range and the number of items to prefetch after it.
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

	public int LookAheadCount { get; }
}
