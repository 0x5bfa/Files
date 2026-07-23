// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage.Windows;

/// <summary>
/// Contains only apartment-neutral data needed to materialize a Windows Shell item.
/// </summary>
internal sealed record WindowsItemLocator
{
	public WindowsItemLocator(
		ReadOnlyMemory<byte> absolutePidl,
		string parsingName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(parsingName);
		AbsolutePidl = absolutePidl;
		ParsingName = parsingName;
	}

	public ReadOnlyMemory<byte> AbsolutePidl { get; }

	public string ParsingName { get; }
}
