// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Registers capability contributors, composers, and decorators at the composition root.
/// </summary>
public sealed class CapabilityPipelineBuilder
{
	private readonly Dictionary<Type, List<object>> contributors = [];
	private readonly Dictionary<Type, object> composers = [];
	private readonly Dictionary<Type, List<object>> decorators = [];

	public CapabilityPipelineBuilder AddContributor<TCapability>(
		ICapabilityContributor<TCapability> contributor,
		int priority = 0,
		CapabilityOwnership ownership = CapabilityOwnership.Model,
		string? origin = null)
		where TCapability : class
	{
		ArgumentNullException.ThrowIfNull(contributor);

		var registration = new CapabilityContributorRegistration<TCapability>(
			contributor,
			priority,
			ownership,
			origin ?? contributor.GetType().Name);

		GetOrCreateList(contributors, typeof(TCapability)).Add(registration);
		return this;
	}

	public CapabilityPipelineBuilder SetComposer<TCapability>(
		ICapabilityComposer<TCapability> composer)
		where TCapability : class
	{
		ArgumentNullException.ThrowIfNull(composer);

		if (!composers.TryAdd(typeof(TCapability), composer))
		{
			throw new InvalidOperationException(
				$"A composer is already registered for capability '{typeof(TCapability).FullName}'.");
		}

		return this;
	}

	public CapabilityPipelineBuilder AddDecorator<TCapability>(
		ICapabilityDecorator<TCapability> decorator)
		where TCapability : class
	{
		ArgumentNullException.ThrowIfNull(decorator);
		GetOrCreateList(decorators, typeof(TCapability)).Add(decorator);
		return this;
	}

	public CapabilityPipeline Build()
	{
		return new CapabilityPipeline(
			CloneLists(contributors),
			new Dictionary<Type, object>(composers),
			CloneLists(decorators));
	}

	private static List<object> GetOrCreateList(
		Dictionary<Type, List<object>> registrations,
		Type capabilityType)
	{
		if (!registrations.TryGetValue(capabilityType, out var values))
		{
			values = [];
			registrations.Add(capabilityType, values);
		}

		return values;
	}

	private static Dictionary<Type, IReadOnlyList<object>> CloneLists(
		Dictionary<Type, List<object>> registrations)
	{
		return registrations.ToDictionary(
			static pair => pair.Key,
			static pair => (IReadOnlyList<object>)pair.Value.ToArray());
	}
}

internal sealed record CapabilityContributorRegistration<TCapability>(
	ICapabilityContributor<TCapability> Contributor,
	int Priority,
	CapabilityOwnership Ownership,
	string Origin)
	where TCapability : class;
