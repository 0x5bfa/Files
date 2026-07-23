// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Storage;

namespace Files.Core.Browsing;

public readonly record struct StorableKey(
	StorageSourceId SourceId,
	string ItemId);
