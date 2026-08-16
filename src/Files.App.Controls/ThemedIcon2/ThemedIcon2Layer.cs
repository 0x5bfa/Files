// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.App.Controls;

/// <summary>
/// Describes one path in a layered <see cref="ThemedIcon2Data"/>.
/// </summary>
public sealed class ThemedIcon2Layer
{
	/// <summary>Gets or sets the semantic color role of this path.</summary>
	public ThemedIconLayerType LayerType { get; set; }

	/// <summary>Gets or sets the SVG path data.</summary>
	public string PathData { get; set; } = string.Empty;

	/// <summary>Gets or sets the opacity of this path.</summary>
	public double Opacity { get; set; } = 1;
}
