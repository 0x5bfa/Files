// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

public interface IBrowseLocationResolver
{
	IAsyncEnumerable<IStorableModel> GetItemsAsync(BrowseLocation location, CancellationToken cancellationToken = default);
}
