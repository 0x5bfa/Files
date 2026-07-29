// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Files.Core.ItemFeatures;

/// <summary>
/// Lazily resolves and owns the optional features attached to one item model.
/// </summary>
public interface IItemFeatures : IDisposable, IAsyncDisposable
{
	TFeature? Get<TFeature>()
		where TFeature : class;

	bool TryGet<TFeature>([NotNullWhen(true)] out TFeature? feature)
		where TFeature : class;
}
