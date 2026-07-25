// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Creates item-bound capability sets from composition-root registrations.
/// </summary>
public sealed class CapabilityPipeline
{
	private readonly IReadOnlyDictionary<Type, IReadOnlyList<object>> contributors;
	private readonly IReadOnlyDictionary<Type, object> composers;
	private readonly IReadOnlyDictionary<Type, IReadOnlyList<object>> decorators;

	internal CapabilityPipeline(
		IReadOnlyDictionary<Type, IReadOnlyList<object>> contributors,
		IReadOnlyDictionary<Type, object> composers,
		IReadOnlyDictionary<Type, IReadOnlyList<object>> decorators)
	{
		this.contributors = contributors;
		this.composers = composers;
		this.decorators = decorators;
	}

	public static CapabilityPipeline Empty { get; } = new CapabilityPipelineBuilder().Build();

	public ICapabilitySet CreateSet(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		return new CapabilitySet(this, context);
	}

	internal CapabilityResolution<TCapability> Resolve<TCapability>(CapabilityContext context)
		where TCapability : class
	{
		var candidates = new List<CapabilityCandidate<TCapability>>();
		var ownedInstances = new List<object>();

		try
		{
			if (context.CoreModel is TCapability directCapability)
			{
				candidates.Add(new CapabilityCandidate<TCapability>(
					directCapability,
					0,
					"CoreModel",
					CapabilityOwnership.External));
			}

			foreach (var registration in GetContributors<TCapability>())
			{
				var capability = registration.Contributor.Create(context);

				if (capability is null)
				{
					continue;
				}

				candidates.Add(new CapabilityCandidate<TCapability>(
					capability,
					registration.Priority,
					registration.Origin,
					registration.Ownership));

				if (registration.Ownership is CapabilityOwnership.Model)
				{
					TrackOwned(context, capability, ownedInstances);
				}
			}

			var capabilityResult = Compose(context, candidates);

			if (capabilityResult is null)
			{
				DisposeTrackedInstances(ownedInstances);
				return CapabilityResolution<TCapability>.Empty;
			}

			if (!candidates.Any(candidate => ReferenceEquals(candidate.Capability, capabilityResult)))
			{
				TrackOwned(context, capabilityResult, ownedInstances);
			}

			foreach (var decorator in GetDecorators<TCapability>())
			{
				var innerCapability = capabilityResult;
				capabilityResult = decorator.Decorate(context, innerCapability)
					?? throw new InvalidOperationException(
						$"A decorator returned null for capability '{typeof(TCapability).FullName}'.");

				if (!ReferenceEquals(innerCapability, capabilityResult))
				{
					TrackOwned(context, capabilityResult, ownedInstances);
				}
			}

			return new CapabilityResolution<TCapability>(capabilityResult, ownedInstances);
		}
		catch (Exception resolutionError)
		{
			try
			{
				DisposeTrackedInstances(ownedInstances);
			}
			catch (AggregateException cleanupError)
			{
				throw new AggregateException(
					"Capability resolution and cleanup failed.",
					[
						resolutionError,
						.. cleanupError.InnerExceptions,
					]);
			}
			catch (Exception cleanupError)
			{
				throw new AggregateException(
					"Capability resolution and cleanup failed.",
					resolutionError,
					cleanupError);
			}

			throw;
		}
	}

	private TCapability? Compose<TCapability>(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<TCapability>> candidates)
		where TCapability : class
	{
		if (composers.TryGetValue(typeof(TCapability), out var composer))
		{
			return ((ICapabilityComposer<TCapability>)composer).Compose(context, candidates);
		}

		return candidates.Count switch
		{
			0 => null,
			1 => candidates[0].Capability,
			_ => throw new InvalidOperationException(
				$"Capability '{typeof(TCapability).FullName}' has multiple candidates but no composer."),
		};
	}

	private IReadOnlyList<CapabilityContributorRegistration<TCapability>> GetContributors<TCapability>()
		where TCapability : class
	{
		if (!contributors.TryGetValue(typeof(TCapability), out var registrations))
		{
			return Array.Empty<CapabilityContributorRegistration<TCapability>>();
		}

		return registrations
			.Cast<CapabilityContributorRegistration<TCapability>>()
			.ToArray();
	}

	private IReadOnlyList<ICapabilityDecorator<TCapability>> GetDecorators<TCapability>()
		where TCapability : class
	{
		if (!decorators.TryGetValue(typeof(TCapability), out var registrations))
		{
			return Array.Empty<ICapabilityDecorator<TCapability>>();
		}

		return registrations
			.Cast<ICapabilityDecorator<TCapability>>()
			.ToArray();
	}

	private static void TrackOwned(
		CapabilityContext context,
		object instance,
		List<object> ownedInstances)
	{
		if (ReferenceEquals(instance, context.CoreModel)
			|| ReferenceEquals(instance, context.Source)
			|| ownedInstances.Any(existing => ReferenceEquals(existing, instance)))
		{
			return;
		}

		if (instance is IDisposable or IAsyncDisposable)
		{
			ownedInstances.Add(instance);
		}
	}

	private static void DisposeTrackedInstances(List<object> ownedInstances)
	{
		var instances = ownedInstances.ToArray();
		ownedInstances.Clear();
		CapabilitySet
			.DisposeInstancesAsync(instances)
			.GetAwaiter()
			.GetResult();
	}
}

internal sealed record CapabilityResolution<TCapability>(
	TCapability? Capability,
	IReadOnlyList<object> OwnedInstances)
	where TCapability : class
{
	public static CapabilityResolution<TCapability> Empty { get; } = new(
		null,
		Array.Empty<object>());
}
