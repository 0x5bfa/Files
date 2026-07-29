// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.ItemFeatures.Previews;

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

		if (hydrationPolicy is not PreviewHydrationPolicy.LocalOnly
			and not PreviewHydrationPolicy.AllowHydration)
		{
			throw new ArgumentOutOfRangeException(nameof(hydrationPolicy));
		}

		MaximumBytes = maximumBytes;
		HydrationPolicy = hydrationPolicy;
	}

	public long? MaximumBytes { get; }

	public PreviewHydrationPolicy HydrationPolicy { get; }
}
