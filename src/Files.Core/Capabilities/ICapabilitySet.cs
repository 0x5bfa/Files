// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Files.Core.Capabilities;

/// <summary>
/// Lazily resolves and owns the capabilities attached to one application model.
/// </summary>
public interface ICapabilitySet : IDisposable, IAsyncDisposable
{
	TCapability? Get<TCapability>()
		where TCapability : class;

	bool TryGet<TCapability>([NotNullWhen(true)] out TCapability? capability)
		where TCapability : class;
}
