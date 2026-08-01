// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Files.Localization;

internal static class AppStrings
{
	private static readonly ResourceMap Resources = new ResourceManager()
		.MainResourceMap
		.TryGetSubtree("Resources");

	public static string Home => Get("Home");

	public static string Loading => Get("Loading");

	public static string NoTabs => Get("NoTabs");

	public static string NewTab => Get("NewTab");

	public static string NoPane => Get("NoPane");

	public static string OperationCanceled => Get("OperationCanceled");

	public static string Folder => Get("Folder");

	public static string File => Get("File");

	public static string FolderPathRequired => Get("FolderPathRequired");

	public static string Back => Get("Back");

	public static string Forward => Get("Forward");

	public static string Up => Get("Up");

	public static string Address => Get("Address");

	public static string Refresh => Get("Refresh");

	public static string Open => Get("Open");

	public static string Add => Get("Add");

	public static string Close => Get("Close");

	public static string Navigation => Get("Navigation");

	public static string Item => Get("Item");

	public static string Tabs => Get("Tabs");

	public static string Panes => Get("Panes");

	public static string FormatItemCount(int count) => string.Format(
		CultureInfo.CurrentCulture,
		count is 1 ? Get("ItemCountSingle") : Get("ItemCountPlural"),
		count);

	public static string FormatNotFolder(string path) => string.Format(
		CultureInfo.CurrentCulture,
		Get("NotFolderFormat"),
		path);

	private static string Get(string key) =>
		Resources.TryGetValue(key)?.ValueAsString ?? key;
}
