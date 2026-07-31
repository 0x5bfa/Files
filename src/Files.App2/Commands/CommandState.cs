// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App2.Commands;

public sealed record CommandState(
	bool IsVisible,
	bool IsEnabled,
	bool IsChecked = false,
	string? DisabledReasonResourceKey = null);
