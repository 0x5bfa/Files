// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures;

/// <summary>
/// Exposes optional features without adding them to the model's required contract.
/// </summary>
public interface IHasItemFeatures
{
	IItemFeatures Features { get; }
}
