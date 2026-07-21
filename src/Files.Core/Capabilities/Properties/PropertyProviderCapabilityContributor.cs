// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Properties;

/// <summary>
/// Binds a source-scoped or plugin-scoped property provider to one model.
/// </summary>
public sealed class PropertyProviderCapabilityContributor : ICapabilityContributor<IPropertySource>
{
	private readonly IPropertyProvider provider;

	public PropertyProviderCapabilityContributor(IPropertyProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		this.provider = provider;
	}

	public IPropertySource? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return provider.CanProvide(context)
			? new BoundPropertySource(provider, context)
			: null;
	}

	private sealed class BoundPropertySource : IPropertySource
	{
		private readonly IPropertyProvider provider;
		private readonly CapabilityContext context;

		public BoundPropertySource(
			IPropertyProvider provider,
			CapabilityContext context)
		{
			this.provider = provider;
			this.context = context;
		}

		public async ValueTask<IReadOnlyDictionary<string, object?>> GetPropertiesAsync(
			PropertyRequest request,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			var result = await provider
				.GetPropertiesAsync(request, [context], cancellationToken)
				.ConfigureAwait(false);

			return result.TryGetValue(context.Reference, out var properties)
				? properties
				: EmptyProperties.Instance;
		}
	}

	private static class EmptyProperties
	{
		public static IReadOnlyDictionary<string, object?> Instance { get; }
			= new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
	}
}
