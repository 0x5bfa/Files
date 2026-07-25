// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Previews;

public enum PreviewBlockReason
{
	RequiresHydration,
	TooLarge,
	AccessDenied,
	DisabledByPolicy,
}

/// <summary>
/// Indicates that a provider understands the item but cannot preview it under the current policy.
/// </summary>
public sealed class BlockedPreviewResult : PreviewResult
{
	public BlockedPreviewResult(PreviewBlockReason reason)
	{
		if (reason is not PreviewBlockReason.RequiresHydration
			and not PreviewBlockReason.TooLarge
			and not PreviewBlockReason.AccessDenied
			and not PreviewBlockReason.DisabledByPolicy)
		{
			throw new ArgumentOutOfRangeException(nameof(reason));
		}

		Reason = reason;
	}

	public PreviewBlockReason Reason { get; }
}
