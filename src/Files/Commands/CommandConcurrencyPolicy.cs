// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Commands;

public enum CommandConcurrencyPolicy
{
	AllowParallel,
	CancelPrevious,
	RejectWhileRunning,
}
