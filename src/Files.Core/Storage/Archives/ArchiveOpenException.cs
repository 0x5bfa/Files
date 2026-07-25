// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

public sealed class ArchiveOpenException : Exception
{
	public ArchiveOpenException(
		string message,
		Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
