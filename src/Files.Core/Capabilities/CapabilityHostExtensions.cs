// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Files.Core.Capabilities;

/// <summary>
/// Provides concise access to optional capabilities exposed by a model.
/// </summary>
public static class CapabilityHostExtensions
{
	public static TCapability? Get<TCapability>(this ICapabilityHost host)
		where TCapability : class
	{
		ArgumentNullException.ThrowIfNull(host);
		return host.Capabilities.Get<TCapability>();
	}

	public static bool TryGet<TCapability>(
		this ICapabilityHost host,
		[NotNullWhen(true)] out TCapability? capability)
		where TCapability : class
	{
		ArgumentNullException.ThrowIfNull(host);
		return host.Capabilities.TryGet(out capability);
	}
}
