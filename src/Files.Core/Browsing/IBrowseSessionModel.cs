// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

/// <summary>
/// UI-agnostic state for one browser pane.
/// </summary>
public interface IBrowseSessionModel : IDisposable
{
	BrowseLocation? Location { get; }

	IReadOnlyList<IStorableModel> Items { get; }

	bool IsLoading { get; }

	Exception? Error { get; }

	event EventHandler? StateChanged;

	ValueTask NavigateAsync(BrowseLocation location, CancellationToken cancellationToken = default);

	ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
