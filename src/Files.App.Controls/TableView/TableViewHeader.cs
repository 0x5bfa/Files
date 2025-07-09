// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;

namespace Files.App.Controls
{
	public partial class TableViewHeader : Button
	{
		internal protected const string VisualStateName_Unsorted = "Unsorted";
		internal protected const string VisualStateName_SortAscending = "SortAscending";
		internal protected const string VisualStateName_SortDescending = "SortDescending";

		[GeneratedDependencyProperty]
		public partial TableViewHeaderSortKind? SortDirection { get; set; }

		[GeneratedDependencyProperty(DefaultValue = true)]
		public partial bool CanBeSorted { get; set; }

		[GeneratedDependencyProperty]
		public partial string? Text { get; set; }

		public TableViewHeader()
		{
			DefaultStyleKey = typeof(TableViewHeader);
		}

		partial void OnSortDirectionChanged(TableViewHeaderSortKind? newValue)
		{
			VisualStateManager.GoToState(
				this,
				newValue switch
				{
					TableViewHeaderSortKind.Ascending => VisualStateName_SortAscending,
					TableViewHeaderSortKind.Descending => VisualStateName_SortDescending,
					_ => VisualStateName_Unsorted,
				},
				true);
		}
	}
}
