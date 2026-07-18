// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

/// <summary>
/// Handles one or more browse location types without adding UI navigation concepts.
/// </summary>
public interface IBrowseLocationHandler
{
	bool CanHandle(BrowseLocation location);

	IAsyncEnumerable<IStorableModel> GetItemsAsync(BrowseLocation location, CancellationToken cancellationToken = default);
}
