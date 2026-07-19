// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Composes capability candidates through a composition-root supplied delegate.
/// </summary>
public sealed class DelegateCapabilityComposer<TCapability> : ICapabilityComposer<TCapability>
	where TCapability : class
{
	private readonly Func<
		CapabilityContext,
		IReadOnlyList<CapabilityCandidate<TCapability>>,
		TCapability?> compose;

	public DelegateCapabilityComposer(
		Func<
			CapabilityContext,
			IReadOnlyList<CapabilityCandidate<TCapability>>,
			TCapability?> compose)
	{
		ArgumentNullException.ThrowIfNull(compose);
		this.compose = compose;
	}

	public TCapability? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<TCapability>> candidates)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(candidates);
		return compose(context, candidates);
	}
}
