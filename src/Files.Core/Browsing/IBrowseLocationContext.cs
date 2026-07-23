// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

/// <summary>
/// Owns the model and enumeration lifetime for one active browse location.
/// </summary>
public interface IBrowseLocationContext : IAsyncDisposable
{
	BrowseLocation Location { get; }

	IStorableModel? LocationModel { get; }

	IAsyncEnumerable<IStorableModel> GetItemsAsync(CancellationToken cancellationToken = default);
}
