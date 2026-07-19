// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Describes one implementation offered for a capability contract.
/// </summary>
public sealed record CapabilityCandidate<TCapability>(
	TCapability Capability,
	int Priority,
	string Origin,
	CapabilityOwnership Ownership)
	where TCapability : class;
