// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

public sealed class ArchiveCredential
{
	public ArchiveCredential(string password)
	{
		ArgumentNullException.ThrowIfNull(password);
		Password = password;
	}

	public string Password { get; }

	public override string ToString()
		=> nameof(ArchiveCredential);
}
