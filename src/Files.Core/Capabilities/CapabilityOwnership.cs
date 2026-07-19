// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Describes who owns a capability returned by a contributor.
/// </summary>
public enum CapabilityOwnership
{
	/// <summary>
	/// The capability set owns and disposes the returned instance.
	/// </summary>
	Model,

	/// <summary>
	/// The contributor or another composition root owns the returned instance.
	/// </summary>
	External,
}
