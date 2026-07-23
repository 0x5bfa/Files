// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities.Properties;

/// <summary>
/// Describes the property values required by a consumer.
/// </summary>
public sealed record PropertyRequest(
	IReadOnlyList<string> PropertyIds);
