// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Creates a capability through a composition-root supplied delegate.
/// </summary>
public sealed class DelegateCapabilityContributor<TCapability> : ICapabilityContributor<TCapability>
	where TCapability : class
{
	private readonly Func<CapabilityContext, TCapability?> factory;

	public DelegateCapabilityContributor(Func<CapabilityContext, TCapability?> factory)
	{
		ArgumentNullException.ThrowIfNull(factory);
		this.factory = factory;
	}

	public TCapability? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return factory(context);
	}
}
