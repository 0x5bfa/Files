// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Browsing;

public static class StorableReferenceExtensions
{
	public static StorableKey GetKey(this StorableReference reference)
	{
		ArgumentNullException.ThrowIfNull(reference);
		return new StorableKey(reference.SourceId, reference.ItemId);
	}
}
