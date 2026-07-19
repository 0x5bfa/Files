// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;
using Files.Core.Storage;

namespace Files.Core.Properties;

/// <summary>
/// Reads properties for a batch of items owned by a provider or plugin.
/// </summary>
public interface IPropertyProvider
{
	bool CanProvide(CapabilityContext context);

	ValueTask<IReadOnlyDictionary<StorableReference, IReadOnlyDictionary<string, object?>>> GetPropertiesAsync(
		IReadOnlyList<CapabilityContext> contexts,
		CancellationToken cancellationToken = default);
}
