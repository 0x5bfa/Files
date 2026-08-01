// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App2.Commands.Handlers;
using Files.App2.Localization;

namespace Files.App2.Commands;

public static class App2CommandRegistration
{
	public static CommandRegistry Build()
	{
		var builder = new CommandRegistryBuilder();
		RegisterNavigation(builder);
		RegisterWindow(builder);
		RegisterPane(builder);
		return builder.Build();
	}

	private static void RegisterNavigation(CommandRegistryBuilder builder)
	{
		builder.Register(
			new(
				CommandIds.NavigateBack,
				"Navigation.Back",
				AppStrings.Back,
				AppStrings.Navigation,
				10),
			static _ => new NavigationCommandHandler(CommandIds.NavigateBack));
		builder.Register(
			new(
				CommandIds.NavigateForward,
				"Navigation.Forward",
				AppStrings.Forward,
				AppStrings.Navigation,
				20),
			static _ => new NavigationCommandHandler(CommandIds.NavigateForward));
		builder.Register(
			new(
				CommandIds.NavigateUp,
				"Navigation.Up",
				AppStrings.Up,
				AppStrings.Navigation,
				30),
			static _ => new NavigationCommandHandler(CommandIds.NavigateUp));
		builder.Register(
			new(
				CommandIds.NavigateHome,
				"Navigation.Home",
				AppStrings.Home,
				AppStrings.Navigation,
				40),
			static _ => new NavigationCommandHandler(CommandIds.NavigateHome));
		builder.Register(
			new(
				CommandIds.NavigatePath,
				"Navigation.Path",
				AppStrings.Address,
				AppStrings.Navigation,
				50),
			static _ => new NavigationCommandHandler(CommandIds.NavigatePath));
		builder.Register(
			new(
				CommandIds.Refresh,
				"Navigation.Refresh",
				AppStrings.Refresh,
				AppStrings.Navigation,
				60),
			static _ => new NavigationCommandHandler(CommandIds.Refresh));
		builder.Register(
			new(
				CommandIds.OpenItem,
				"Item.Open",
				AppStrings.Open,
				AppStrings.Item,
				10),
			static _ => new NavigationCommandHandler(CommandIds.OpenItem));
	}

	private static void RegisterWindow(CommandRegistryBuilder builder)
	{
		builder.Register(
			new(
				CommandIds.NewTab,
				"Tab.New",
				AppStrings.Add,
				AppStrings.Tabs,
				10),
			static _ => new WindowCommandHandler(CommandIds.NewTab));
		builder.Register(
			new(
				CommandIds.CloseTab,
				"Tab.Close",
				AppStrings.Close,
				AppStrings.Tabs,
				20),
			static _ => new WindowCommandHandler(CommandIds.CloseTab));
	}

	private static void RegisterPane(CommandRegistryBuilder builder)
	{
		builder.Register(
			new(
				CommandIds.NewPane,
				"Pane.New",
				AppStrings.Add,
				AppStrings.Panes,
				10),
			static _ => new PaneCommandHandler(CommandIds.NewPane));
		builder.Register(
			new(
				CommandIds.ClosePane,
				"Pane.Close",
				AppStrings.Close,
				AppStrings.Panes,
				20),
			static _ => new PaneCommandHandler(CommandIds.ClosePane));
	}
}
