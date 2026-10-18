// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

#pragma warning disable IDE0130 // Windows APIs share a namespace across responsibility folders.

using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Ole;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace Files.App.Utils.Shell.ContextMenus;

internal static class FilesShellExplorerCommandSource
{
	private const int MaximumSubCommandDepth = 2;
	private const int PendingResult = unchecked((int)0x8000000A);

	internal static unsafe WindowsShellExplorerCommand? TryCreateDescription(IExplorerCommand command, IShellItemArray selection, WindowsShellExplorerCommandToken token, string fallbackTitle, int depth,
		CancellationToken cancellationToken, string? staticDisplayName = null, string? staticIconPath = null, string? iconPackageFullName = null, string? fallbackIconPath = null, int fallbackIconIndex = 0,
		string? fallbackIconPackageFullName = null, Action<WindowsShellExplorerCommand, IExplorerCommand>? retain = null)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var stateResult = command.GetState(selection, false, out var state);
		if (stateResult == HRESULT.E_NOTIMPL && command is IExplorerCommandState stateCommand)
		{
			stateResult = stateCommand.GetState(selection, false, out var stateValue);
			state = (_EXPCMDSTATE)stateValue;
		}

		if (stateResult.Value is PendingResult)
		{
			if (command is IExplorerCommandState slowStateCommand)
			{
				stateResult = slowStateCommand.GetState(selection, true, out var stateValue);
				state = (_EXPCMDSTATE)stateValue;
			}
			else
			{
				stateResult = command.GetState(selection, true, out state);
			}
		}
		else if (stateResult == HRESULT.E_NOTIMPL && WindowsShellCommandStorePropertyBag.TryCreate(token.VerbId) is not null)
		{
			stateResult = HRESULT.S_OK;
		}

		if (stateResult.Failed || state.HasFlag(_EXPCMDSTATE.ECS_HIDDEN))
		{
			return null;
		}

		var flagsResult = command.GetFlags(out var flags);
		if (flagsResult.Failed)
		{
			flags = _EXPCMDFLAGS.ECF_DEFAULT;
		}

		var title = ReadCommandString(command.GetTitle, selection);
		if (string.IsNullOrWhiteSpace(title))
		{
			title = staticDisplayName;
		}

		if (string.IsNullOrWhiteSpace(title))
		{
			title = string.IsNullOrWhiteSpace(fallbackTitle) ? token.VerbId : fallbackTitle;
		}

		var iconLocation = ReadCommandString(command.GetIcon, selection);
		if (string.IsNullOrWhiteSpace(iconLocation))
		{
			iconLocation = staticIconPath;
		}

		var (iconPath, iconIndex) = ParseCommandIcon(iconLocation);
		var children = depth < MaximumSubCommandDepth && flags.HasFlag(_EXPCMDFLAGS.ECF_HASSUBCOMMANDS)
			? GetSubCommandDescriptions(command, selection, token, depth + 1, cancellationToken, iconPackageFullName, fallbackIconPath, fallbackIconIndex, fallbackIconPackageFullName, retain)
			: [];

		var description = new WindowsShellExplorerCommand(token, token.VerbId, title, iconPath, iconIndex, !state.HasFlag(_EXPCMDSTATE.ECS_DISABLED), state.HasFlag(_EXPCMDSTATE.ECS_CHECKED),
			state.HasFlag(_EXPCMDSTATE.ECS_RADIOCHECK), flags.HasFlag(_EXPCMDFLAGS.ECF_ISSEPARATOR), children, iconPackageFullName: iconPackageFullName,
			fallbackIconPath: fallbackIconPath, fallbackIconIndex: fallbackIconIndex, fallbackIconPackageFullName: fallbackIconPackageFullName);
		retain?.Invoke(description, command);

		return description;
	}

	private static IReadOnlyList<WindowsShellExplorerCommand> GetSubCommandDescriptions(IExplorerCommand parent, IShellItemArray selection, WindowsShellExplorerCommandToken parentToken, int depth,
		CancellationToken cancellationToken, string? iconPackageFullName, string? fallbackIconPath, int fallbackIconIndex,
		string? fallbackIconPackageFullName, Action<WindowsShellExplorerCommand, IExplorerCommand>? retain)
	{
		if (parent.EnumSubCommands(out var enumerator).Failed || enumerator is null)
		{
			return [];
		}

		var descriptions = new List<WindowsShellExplorerCommand>();
		var subCommandIndex = 0;
		while (TryGetNextCommand(enumerator, out var subCommand))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var path = parentToken.SubCommandPath.Append(subCommandIndex).ToArray();
			var token = new WindowsShellExplorerCommandToken(parentToken.ClassId, parentToken.VerbId, path);
			if (TryCreateDescription(subCommand, selection, token, parentToken.VerbId, depth, cancellationToken,
				iconPackageFullName: iconPackageFullName, fallbackIconPath: fallbackIconPath, fallbackIconIndex: fallbackIconIndex,
				fallbackIconPackageFullName: fallbackIconPackageFullName, retain: retain) is { } description)
			{
				descriptions.Add(description);
			}

			subCommandIndex++;
		}

		return descriptions;
	}

	private static bool TryGetSubCommand(IExplorerCommand parent, int requestedIndex, out IExplorerCommand command)
	{
		command = null!;
		if (requestedIndex < 0 || parent.EnumSubCommands(out var enumerator).Failed || enumerator is null)
		{
			return false;
		}

		for (var index = 0; index <= requestedIndex; index++)
		{
			if (!TryGetNextCommand(enumerator, out command))
			{
				return false;
			}
		}

		return true;
	}

	private static unsafe bool TryGetNextCommand(IEnumExplorerCommand enumerator, out IExplorerCommand command)
	{
		var commands = new IExplorerCommand[1];
		uint fetched = 0;
		var result = enumerator.Next(1, commands, &fetched);
		command = commands[0];

		return result.Succeeded && fetched is 1 && command is not null;
	}

	internal static unsafe IExplorerCommand? TryCreateExplorerCommand(WindowsFileExplorerAppExtensionRegistration registration, object? site = null)
	{
		var classId = registration.ClassId;
		var hr = PInvoke.CoCreateInstance(classId, null, CLSCTX.CLSCTX_INPROC_SERVER | CLSCTX.CLSCTX_LOCAL_SERVER, out IExplorerCommand? command);
		if (hr.Failed || command is null)
		{
			return null;
		}

		if (site is not null && command is IObjectWithSite objectWithSite && objectWithSite.SetSite(site).Failed)
			return null;

		if (command is IInitializeCommand initializer && !string.IsNullOrEmpty(registration.VerbId))
		{
			var propertyBag = WindowsShellCommandStorePropertyBag.TryCreate(registration.VerbId);
			fixed (char* verbId = registration.VerbId)
			{
				if (initializer.Initialize(new PCWSTR(verbId), propertyBag!).Failed)
				{
					return null;
				}
			}
		}
		return command;
	}

	private static unsafe string? ReadCommandString(CommandStringGetter getter, IShellItemArray selection)
	{
		PWSTR value = default;
		try
		{
			var hr = getter(selection, &value);

			return hr.Succeeded && value.Value is not null ? new string(value.Value) : null;
		}
		finally
		{
			PInvoke.CoTaskMemFree(value.Value);
		}
	}

	internal static (string? Path, int Index) ParseCommandIcon(string? iconLocation)
	{
		if (string.IsNullOrWhiteSpace(iconLocation))
		{
			return (null, 0);
		}

		var path = iconLocation.Trim();
		var iconIndex = 0;
		var separatorIndex = path.LastIndexOf(',');
		if (separatorIndex >= 0 && int.TryParse(path.AsSpan(separatorIndex + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex))
		{
			iconIndex = parsedIndex;
			path = path[..separatorIndex];
		}

		path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

		return string.IsNullOrWhiteSpace(path) ? (null, 0) : (path, iconIndex);
	}

	private unsafe delegate HRESULT CommandStringGetter(IShellItemArray selection, PWSTR* value);

	internal static bool Invoke(IExplorerCommand command, IShellItemArray selection, string? workingDirectory, System.Drawing.Point invocationPoint)
	{
		var invokeResult = command.Invoke(selection, null!);
		if (invokeResult != HRESULT.E_NOTIMPL)
		{
			return invokeResult.Succeeded;
		}

		if (command is not IObjectWithSelection objectWithSelection || command is not IExecuteCommand executeCommand || objectWithSelection.SetSelection(selection).Failed)
			return false;

		if (!string.IsNullOrWhiteSpace(workingDirectory))
			executeCommand.SetDirectory(workingDirectory);
		executeCommand.SetPosition(invocationPoint);
		executeCommand.SetShowWindow((int)Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_SHOWNORMAL);

		return executeCommand.Execute().Succeeded;
	}
}
