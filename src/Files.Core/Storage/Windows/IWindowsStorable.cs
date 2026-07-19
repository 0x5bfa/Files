// Copyright (c) Files Community
// Licensed under the MIT License.

using OwlCore.Storage;

namespace Files.Core.Storage.Windows;

/// <summary>
/// Describes an OwlCore item backed by the Windows Shell namespace.
/// </summary>
public interface IWindowsStorable : IStorableChild, IStorageAddressSource
{
	string ParsingName { get; }

	string? FileSystemPath { get; }

	bool IsFileSystem { get; }
}
