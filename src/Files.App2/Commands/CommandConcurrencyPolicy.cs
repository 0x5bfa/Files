// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App2.Commands;

public enum CommandConcurrencyPolicy
{
	AllowParallel,
	CancelPrevious,
	RejectWhileRunning,
}
