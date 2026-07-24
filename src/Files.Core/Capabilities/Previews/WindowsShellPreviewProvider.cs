// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Runtime.Versioning;
using Files.Core.Capabilities;
using Files.Core.Storage.Windows;
using OwlCore.Storage;

namespace Files.Core.Capabilities.Previews;

[SupportedOSPlatform("windows")]
public sealed class WindowsShellPreviewProvider : IPreviewProvider
{
	private readonly IWindowsPreviewHandlerResolver handlerResolver;
	private readonly IWindowsShellPreviewPolicy policy;

	public WindowsShellPreviewProvider(
		IWindowsPreviewHandlerResolver handlerResolver,
		IWindowsShellPreviewPolicy policy)
	{
		ArgumentNullException.ThrowIfNull(handlerResolver);
		ArgumentNullException.ThrowIfNull(policy);
		this.handlerResolver = handlerResolver;
		this.policy = policy;
	}

	public bool CanProvide(CapabilityContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		return context.CoreModel is IWindowsStorable
			&& context.CoreModel is IFile;
	}

	public async ValueTask<PreviewResult?> GetPreviewAsync(
		PreviewRequest request,
		CapabilityContext context,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(context);
		cancellationToken.ThrowIfCancellationRequested();

		if (!CanProvide(context))
		{
			return null;
		}

		var handlerClsid = await handlerResolver
			.ResolveAsync(context, cancellationToken)
			.ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();

		if (handlerClsid is null)
		{
			return null;
		}

		var blockReason = policy.GetBlockReason(context, handlerClsid.Value);
		return blockReason is not null
			? new BlockedPreviewResult(blockReason.Value)
			: new WindowsShellPreviewResult(context.Reference, handlerClsid.Value);
	}
}
