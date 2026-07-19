// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Creates an item-bound capability when it applies to the supplied context.
/// </summary>
public interface ICapabilityContributor<TCapability>
	where TCapability : class
{
	TCapability? Create(CapabilityContext context);
}
