// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Capabilities.Archives;

/// <summary>
/// Marks a storage item as a candidate for archive-backed navigation.
/// </summary>
public interface IArchiveSource
{
	StorableReference Archive { get; }
}
