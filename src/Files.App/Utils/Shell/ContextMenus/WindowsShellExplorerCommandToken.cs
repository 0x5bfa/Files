// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

#pragma warning disable IDE0130 // Windows APIs share a namespace across responsibility folders.

namespace Files.App.Utils.Shell.ContextMenus;

internal sealed record WindowsShellExplorerCommandToken(Guid ClassId, string VerbId, IReadOnlyList<int> SubCommandPath);
