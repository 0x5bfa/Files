// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Browsing;

/// <summary>
/// Resolves a logical parent when it cannot be represented by the current
/// location model's storage parent alone.
/// </summary>
public interface IBrowseLocationParentResolver
{
	bool CanGetParent { get; }

	ValueTask<BrowseLocation?> GetParentLocationAsync(
		CancellationToken cancellationToken = default);
}
