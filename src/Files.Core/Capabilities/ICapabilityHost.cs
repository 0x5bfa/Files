// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Core.Capabilities;

/// <summary>
/// Exposes optional capabilities without adding them to the model's mandatory contract.
/// </summary>
public interface ICapabilityHost
{
	ICapabilitySet Capabilities { get; }
}
