// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Properties;

/// <summary>
/// Reads properties bound to one application model.
/// </summary>
public interface IPropertySource
{
	ValueTask<IReadOnlyDictionary<string, object?>> GetPropertiesAsync(
		CancellationToken cancellationToken = default);
}
