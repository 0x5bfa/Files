// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Thumbnails;

public sealed record ThumbnailResult(
	ReadOnlyMemory<byte> Content,
	string ContentType,
	bool IsFallback);
