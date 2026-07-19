// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ViewSettings;

public sealed record ViewColumnSettings
{
	public ViewColumnSettings(
		string propertyId,
		double width,
		int order,
		bool isVisible = true)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(propertyId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
		ArgumentOutOfRangeException.ThrowIfNegative(order);

		PropertyId = propertyId;
		Width = width;
		Order = order;
		IsVisible = isVisible;
	}

	public string PropertyId { get; }

	public double Width { get; }

	public int Order { get; }

	public bool IsVisible { get; }
}
