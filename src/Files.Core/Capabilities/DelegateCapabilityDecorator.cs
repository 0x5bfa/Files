// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Decorates a capability through a composition-root supplied delegate.
/// </summary>
public sealed class DelegateCapabilityDecorator<TCapability> : ICapabilityDecorator<TCapability>
	where TCapability : class
{
	private readonly Func<CapabilityContext, TCapability, TCapability> decorate;

	public DelegateCapabilityDecorator(Func<CapabilityContext, TCapability, TCapability> decorate)
	{
		ArgumentNullException.ThrowIfNull(decorate);
		this.decorate = decorate;
	}

	public TCapability Decorate(CapabilityContext context, TCapability capability)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(capability);
		return decorate(context, capability)
			?? throw new InvalidOperationException("A capability decorator returned null.");
	}
}
