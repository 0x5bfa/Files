// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.UITests.Data;

internal class TableViewColumnModel(string header, string propertyName)
{
	public string? Header { get; set; } = header;

	public string? PropertyName { get; set; } = propertyName;
}
