// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Core.Capabilities;

namespace Files.Core.Capabilities.Previews;

/// <summary>
/// Allows registered Shell preview handlers. The default activator still uses a local server.
/// </summary>
public sealed class AllowWindowsShellPreviewPolicy
	: IWindowsShellPreviewPolicy
{
	public static AllowWindowsShellPreviewPolicy Instance { get; } = new();

	private AllowWindowsShellPreviewPolicy()
	{
	}

	public PreviewBlockReason? GetBlockReason(
		CapabilityContext context,
		Guid handlerClsid)
	{
		ArgumentNullException.ThrowIfNull(context);
		if (handlerClsid == Guid.Empty)
		{
			throw new ArgumentException(
				"A preview handler CLSID is required.",
				nameof(handlerClsid));
		}

		return null;
	}
}
