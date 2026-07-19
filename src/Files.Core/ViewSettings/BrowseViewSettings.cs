// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ViewSettings;

/// <summary>
/// Contains UI-agnostic presentation state for one browse location.
/// </summary>
public sealed record BrowseViewSettings
{
	public BrowseViewSettings(
		ViewLayoutMode layoutMode = ViewLayoutMode.Details,
		IEnumerable<ViewColumnSettings>? columns = null,
		string? sortPropertyId = null,
		ViewSortDirection sortDirection = ViewSortDirection.Ascending,
		double? itemSize = null)
	{
		if (itemSize is not null)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemSize.Value);
		}

		LayoutMode = layoutMode;
		Columns = Array.AsReadOnly((columns ?? []).ToArray());
		SortPropertyId = sortPropertyId;
		SortDirection = sortDirection;
		ItemSize = itemSize;
	}

	public static BrowseViewSettings Default { get; } = new();

	public ViewLayoutMode LayoutMode { get; }

	public IReadOnlyList<ViewColumnSettings> Columns { get; }

	public string? SortPropertyId { get; }

	public ViewSortDirection SortDirection { get; }

	public double? ItemSize { get; }
}
