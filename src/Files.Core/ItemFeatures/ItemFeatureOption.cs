// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures;

/// <summary>
/// Describes one available implementation of an item feature.
/// </summary>
public sealed record ItemFeatureOption<TFeature>(
	TFeature Feature,
	int Priority,
	string Origin,
	ItemFeatureLifetime Lifetime)
	where TFeature : class;
