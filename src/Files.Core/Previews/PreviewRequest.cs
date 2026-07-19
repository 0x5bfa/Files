// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Previews;

public sealed record PreviewRequest
{
	public PreviewRequest(long? maximumBytes = null)
	{
		if (maximumBytes is not null)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes.Value);
		}

		MaximumBytes = maximumBytes;
	}

	public long? MaximumBytes { get; }
}
