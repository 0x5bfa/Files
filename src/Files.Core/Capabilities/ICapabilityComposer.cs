// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Combines multiple candidates for one capability contract.
/// </summary>
public interface ICapabilityComposer<TCapability>
	where TCapability : class
{
	TCapability? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<TCapability>> candidates);
}
