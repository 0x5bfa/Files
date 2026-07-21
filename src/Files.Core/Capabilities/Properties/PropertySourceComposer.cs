// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Properties;

/// <summary>
/// Merges properties from all candidates, with higher-priority candidates winning duplicate keys.
/// </summary>
public sealed class PropertySourceComposer : ICapabilityComposer<IPropertySource>
{
	public IPropertySource? Compose(
		CapabilityContext context,
		IReadOnlyList<CapabilityCandidate<IPropertySource>> candidates)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(candidates);

		var sources = candidates
			.OrderByDescending(static candidate => candidate.Priority)
			.Select(static candidate => candidate.Capability)
			.ToArray();

		return sources.Length switch
		{
			0 => null,
			1 => sources[0],
			_ => new CompositePropertySource(sources),
		};
	}

	private sealed class CompositePropertySource : IPropertySource
	{
		private readonly IReadOnlyList<IPropertySource> sources;

		public CompositePropertySource(IReadOnlyList<IPropertySource> sources)
		{
			this.sources = sources;
		}

		public async ValueTask<IReadOnlyDictionary<string, object?>> GetPropertiesAsync(
			CancellationToken cancellationToken = default)
		{
			var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

			foreach (var source in sources)
			{
				cancellationToken.ThrowIfCancellationRequested();
				var properties = await source
					.GetPropertiesAsync(cancellationToken)
					.ConfigureAwait(false);

				foreach (var property in properties)
				{
					merged.TryAdd(property.Key, property.Value);
				}
			}

			return new ReadOnlyDictionary<string, object?>(merged);
		}
	}
}
