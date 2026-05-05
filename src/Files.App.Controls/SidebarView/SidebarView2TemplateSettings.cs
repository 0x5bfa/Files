// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;

namespace Files.App.Controls;

public partial class SidebarView2TemplateSettings : DependencyObject
{
	[GeneratedDependencyProperty(DefaultValue = -240d)]
	public partial double NegativeOpenPaneLength { get; internal set; }

	[GeneratedDependencyProperty(DefaultValue = -56d)]
	public partial double NegativeCompactPaneLength { get; internal set; }
}
