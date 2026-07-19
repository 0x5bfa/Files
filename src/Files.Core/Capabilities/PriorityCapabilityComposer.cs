// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Selects the single highest-priority capability candidate.
/// </summary>
public sealed class PriorityCapabilityComposer<TCapability> : ICapabilityComposer<TCapability>
	where TCapability : class
{
	public TCapability? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<TCapability>> candidates)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(candidates);

		if (candidates.Count is 0)
		{
			return null;
		}

		var highestPriority = candidates.Max(static candidate => candidate.Priority);
		var matches = candidates
			.Where(candidate => candidate.Priority == highestPriority)
			.ToArray();

		if (matches.Length is not 1)
		{
			throw new InvalidOperationException(
				$"Capability '{typeof(TCapability).FullName}' has more than one candidate at priority {highestPriority}.");
		}

		return matches[0].Capability;
	}
}
