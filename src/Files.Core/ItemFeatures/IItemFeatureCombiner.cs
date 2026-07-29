// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures;

/// <summary>
/// Combines multiple options for one item feature.
/// </summary>
public interface IItemFeatureCombiner<TFeature>
	where TFeature : class
{
	TFeature? Combine(
		ItemContext context,
		IReadOnlyList<ItemFeatureOption<TFeature>> options);
}
