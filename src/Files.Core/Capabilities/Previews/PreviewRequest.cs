// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Previews;

public enum PreviewHydrationPolicy
{
	LocalOnly,
	AllowHydration,
}

public sealed record PreviewRequest
{
	public PreviewRequest(
		long? maximumBytes = null,
		PreviewHydrationPolicy hydrationPolicy = PreviewHydrationPolicy.LocalOnly)
	{
		if (maximumBytes is not null)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes.Value);
		}

		MaximumBytes = maximumBytes;
		HydrationPolicy = hydrationPolicy;
	}

	public long? MaximumBytes { get; }

	public PreviewHydrationPolicy HydrationPolicy { get; }
}
