// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Allows stream previews without applying hydration or trust restrictions.
/// </summary>
public sealed class AllowPreviewStreamAccessPolicy
	: IPreviewStreamAccessPolicy
{
	public static AllowPreviewStreamAccessPolicy Instance { get; } = new();

	private AllowPreviewStreamAccessPolicy()
	{
	}

	public ValueTask<PreviewBlockReason?> GetBlockReasonAsync(
		PreviewRequest request,
		CapabilityContext context,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(context);
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult<PreviewBlockReason?>(null);
	}
}
