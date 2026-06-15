// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Controls;

/// <summary>
/// The display mode of the <see cref="SidebarView2"/>.
/// </summary>
public enum SidebarViewDisplayMode
{
	/// <summary>
	/// The sidebar chooses the display mode automatically.
	/// </summary>
	Auto,

	/// <summary>
	/// The sidebar is expanded on the left and items can also be expanded.
	/// </summary>
	Left,

	/// <summary>
	/// The sidebar is compact on the left and shows only item icons.
	/// </summary>
	LeftCompact,

	/// <summary>
	/// The sidebar is hidden and moves in from the left when <see cref="SidebarView2.IsPaneOpen"/> is set to <see langword="true"/>.
	/// </summary>
	LeftMinimal,
}
