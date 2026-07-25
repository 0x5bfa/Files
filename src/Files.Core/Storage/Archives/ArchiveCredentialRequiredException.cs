// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Archives;

public sealed class ArchiveCredentialRequiredException : Exception
{
	public ArchiveCredentialRequiredException(
		ArchiveCredentialChallenge challenge)
		: base(CreateMessage(challenge))
	{
		Challenge = challenge;
	}

	public ArchiveCredentialChallenge Challenge { get; }

	private static string CreateMessage(
		ArchiveCredentialChallenge challenge)
	{
		ArgumentNullException.ThrowIfNull(challenge);
		return $"A credential is required to open archive '{challenge.DisplayName}'.";
	}
}
