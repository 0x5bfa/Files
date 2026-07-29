// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures;

/// <summary>
/// Wraps an item feature with cross-cutting behavior.
/// </summary>
public interface IItemFeatureWrapper<TFeature>
	where TFeature : class
{
	TFeature Wrap(ItemContext context, TFeature feature);
}
