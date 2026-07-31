// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App2.Commands;

public sealed record CommandDescriptor(
	CommandId Id,
	string LabelResourceKey,
	string IconKey,
	string Group,
	int Order);
