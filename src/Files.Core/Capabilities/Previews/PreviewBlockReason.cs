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
		Reason = reason;
	}

	public PreviewBlockReason Reason { get; }
}
