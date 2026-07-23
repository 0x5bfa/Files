// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;
using Files.Core.ViewSettings;

namespace Files.Core.Browsing;

/// <summary>
/// UI-agnostic state for one browser pane.
/// </summary>
public interface IBrowseSessionModel : IDisposable, IAsyncDisposable
{
	BrowseLocation? Location { get; }

	IBrowseLocationContext? Context { get; }

	long Generation { get; }

	IReadOnlyList<IStorableModel> Items { get; }

	long ItemsVersion { get; }

	BrowseSelectionState Selection { get; }

	BrowseViewSettings ViewSettings { get; }

	bool IsLoading { get; }

	Exception? Error { get; }

	event EventHandler? StateChanged;

	event EventHandler<BrowseItemsChangedEventArgs>? ItemsChanged;

	event EventHandler? SelectionChanged;

	ValueTask NavigateAsync(BrowseLocation location, CancellationToken cancellationToken = default);

	ValueTask RefreshAsync(CancellationToken cancellationToken = default);

	ValueTask UpdateViewSettingsAsync(
		BrowseViewSettings settings,
		CancellationToken cancellationToken = default);

	void SetSelection(
		IEnumerable<StorableKey> selectedKeys,
		StorableKey? focusedKey,
		StorableKey? anchorKey);
}
