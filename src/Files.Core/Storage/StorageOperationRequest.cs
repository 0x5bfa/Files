// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Storage;

/// <summary>
/// Describes one storage operation requested by the application.
/// </summary>
public abstract record StorageOperationRequest;

/// <summary>
/// Requests that one item be renamed within its current parent.
/// </summary>
public sealed record RenameOperationRequest(
	StorableReference Item,
	string NewName) : StorageOperationRequest;
