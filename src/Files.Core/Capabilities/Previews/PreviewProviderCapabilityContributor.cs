// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Binds a shared preview provider to one item capability context.
/// </summary>
public sealed class PreviewProviderCapabilityContributor
	: ICapabilityContributor<IPreviewSource>
{
	private readonly IPreviewProvider provider;

	public PreviewProviderCapabilityContributor(IPreviewProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);
		this.provider = provider;
	}

	public IPreviewSource? Create(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return provider.CanProvide(context)
			? new BoundPreviewSource(provider, context)
			: null;
	}

	private sealed class BoundPreviewSource : IPreviewSource
	{
		private readonly IPreviewProvider provider;
		private readonly CapabilityContext context;

		public BoundPreviewSource(
			IPreviewProvider provider,
			CapabilityContext context)
		{
			this.provider = provider;
			this.context = context;
		}

		public ValueTask<PreviewResult?> GetPreviewAsync(
			PreviewRequest request,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			return provider.GetPreviewAsync(
				request,
				context,
				cancellationToken);
		}
	}
}
