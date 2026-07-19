// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Wraps a composed capability with cross-cutting behavior.
/// </summary>
public interface ICapabilityDecorator<TCapability>
	where TCapability : class
{
	TCapability Decorate(CapabilityContext context, TCapability capability);
}
