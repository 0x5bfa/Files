// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.UITests.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Files.App.UITests.Data;

public partial class SidebarViewSampleMenuItemTemplateSelector : DataTemplateSelector
{
	public DataTemplate ItemTemplate { get; set; } = null!;

	public DataTemplate HeaderItemTemplate { get; set; } = null!;

	public DataTemplate SeparatorItemTemplate { get; set; } = null!;

	protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
	{
		return item is SidebarViewSampleHeaderItem
			? HeaderItemTemplate
			: item is SidebarViewSampleSeparatorItem
				? SeparatorItemTemplate
				: ItemTemplate;
	}
}
