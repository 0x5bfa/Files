// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Models;

namespace Files.Core.Browsing;

public static class BrowseSessionModelExtensions
{
	public static IStorableModel? GetFocusedItem(
		this IBrowseSessionModel session)
	{
		ArgumentNullException.ThrowIfNull(session);

		return session.Selection.FocusedKey is { } key
			? session.Items.FirstOrDefault(
				item => item.Reference.GetKey() == key)
			: null;
	}

	public static IReadOnlyList<IStorableModel> GetSelectedItems(
		this IBrowseSessionModel session)
	{
		ArgumentNullException.ThrowIfNull(session);

		var selectedKeys = session.Selection.SelectedKeys.ToHashSet();
		return Array.AsReadOnly(
			session.Items
				.Where(item => selectedKeys.Contains(item.Reference.GetKey()))
				.ToArray());
	}
}
