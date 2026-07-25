// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

/// <summary>
/// Identifies an item inside an archive independently of its active backend.
/// </summary>
public interface IArchiveEntry
{
	StorableReference Archive { get; }

	string EntryPath { get; }
}
