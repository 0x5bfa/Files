// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Commands;

public interface ICommandHandler
{
	CommandId Id { get; }

	CommandConcurrencyPolicy ConcurrencyPolicy { get; }

	CommandState GetState(CommandContext context);

	ValueTask<CommandExecutionResult> ExecuteAsync(
		CommandContext context,
		CancellationToken cancellationToken = default);
}
