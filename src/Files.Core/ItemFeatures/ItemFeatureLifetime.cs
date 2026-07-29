// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures;

/// <summary>
/// Describes who owns an item feature created by a factory.
/// </summary>
public enum ItemFeatureLifetime
{
	/// <summary>
	/// The item owns and disposes the feature.
	/// </summary>
	Item,

	/// <summary>
	/// The factory or another composition root owns the shared feature.
	/// </summary>
	Shared,
}
