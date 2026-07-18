// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Describes an address that a storage source may be able to resolve.
/// </summary>
public sealed record StorageAddress
{
	public StorageAddress(string scheme, string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		Scheme = scheme;
		Value = value;
	}

	public string Scheme { get; }

	public string Value { get; }

	public override string ToString() => $"{Scheme}:{Value}";
}
