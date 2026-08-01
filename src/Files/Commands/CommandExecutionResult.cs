// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Commands;

public enum CommandExecutionStatus
{
	Succeeded,
	Canceled,
	Unsupported,
	Failed,
}

public sealed record CommandExecutionResult(
	CommandExecutionStatus Status,
	Exception? Error = null)
{
	public static CommandExecutionResult Succeeded() =>
		new(CommandExecutionStatus.Succeeded);

	public static CommandExecutionResult Canceled() =>
		new(CommandExecutionStatus.Canceled);

	public static CommandExecutionResult Unsupported() =>
		new(CommandExecutionStatus.Unsupported);

	public static CommandExecutionResult Failed(Exception error)
	{
		ArgumentNullException.ThrowIfNull(error);
		return new(CommandExecutionStatus.Failed, error);
	}
}
