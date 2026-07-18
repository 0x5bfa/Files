// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Identifies a configured storage source, such as a Windows shell namespace or FTP connection.
/// </summary>
public sealed record StorageSourceId
{
	public StorageSourceId(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		Value = value;
	}

	public string Value { get; }

	public override string ToString() => Value;
}
