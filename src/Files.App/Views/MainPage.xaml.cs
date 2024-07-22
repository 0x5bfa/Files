// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using CommunityToolkit.WinUI.Helpers;
using CommunityToolkit.WinUI.UI;
using CommunityToolkit.WinUI.UI.Controls;
using Files.App.UserControls.Sidebar;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Data;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Services.Store;
using WinRT.Interop;
using VirtualKey = Windows.System.VirtualKey;

namespace Files.App.Views
{
	/// <summary>
	/// Displays the main page, which is set to <see cref="MainWindow.Content"/> directly and contains all components in Files.
	/// </summary>
	public sealed partial class MainPage : Page
	{
		// Dependency injections

		private readonly IGeneralSettingsService generalSettingsService = Ioc.Default.GetRequiredService<IGeneralSettingsService>();
		private readonly StatusCenterViewModel OngoingTasksViewModel = Ioc.Default.GetRequiredService<StatusCenterViewModel>();
		private readonly SidebarViewModel SidebarAdaptiveViewModel = Ioc.Default.GetRequiredService<SidebarViewModel>();
		private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
  		private readonly IWindowContext WindowContext = Ioc.Default.GetRequiredService<IWindowContext>();
		private readonly MainPageViewModel ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
		private readonly ICommandManager Commands = Ioc.Default.GetRequiredService<ICommandManager>();

		// Fields

		private bool _keyReleased = true;

		private DispatcherQueueTimer _updateDateDisplayTimer;

		private bool isAppRunningAsAdmin
			=> ElevationHelpers.IsAppRunAsAdmin();

		// Constructor

		public MainPage()
		{
			InitializeComponent();

			SidebarAdaptiveViewModel.PaneFlyout = (MenuFlyout)Resources["SidebarContextMenu"];

			if (FilePropertiesHelpers.FlowDirectionSettingIsRightToLeft)
				FlowDirection = FlowDirection.RightToLeft;

			ViewModel.PropertyChanged += ViewModel_PropertyChanged;
			UserSettingsService.OnSettingChangedEvent += UserSettingsService_OnSettingChanged;

			_updateDateDisplayTimer = DispatcherQueue.CreateTimer();
			_updateDateDisplayTimer.Interval = TimeSpan.FromSeconds(1);
			_updateDateDisplayTimer.Tick += UpdateDateDisplayTimer_Tick;
		}

		// Override methods

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			ViewModel.OnNavigatedToAsync(e);
		}

		protected override async void OnPreviewKeyDown(KeyRoutedEventArgs e)
		{
			base.OnPreviewKeyDown(e);

			switch (e.Key)
			{
				case VirtualKey.Menu or VirtualKey.Control or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows:
					break;
				default:
					var currentModifiers = HotKeyHelpers.GetCurrentKeyModifiers();
					HotKey hotKey = new((Keys)e.Key, currentModifiers);

					// TextBox takes precedence over any hotkeys.
					if (e.OriginalSource is DependencyObject source &&
						source.FindAscendantOrSelf<TextBox>() is not null)
						break;

					// Execute command for the hotkey
					var command = Commands[hotKey];
					if (command.Code is not CommandCodes.None && _keyReleased)
					{
						_keyReleased = false;
	
						if (command.IsExecutable)
						{
							e.Handled = true;
							await command.ExecuteAsync();
						}
					}
					break;
			}
		}

		protected override void OnPreviewKeyUp(KeyRoutedEventArgs e)
		{
			base.OnPreviewKeyUp(e);

			switch (e.Key)
			{
				case VirtualKey.Menu or VirtualKey.Control or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows:
					break;
				default:
					_keyReleased = true;
					break;
			}
		}

		protected override void OnLostFocus(RoutedEventArgs e)
		{
			// A workaround for issue with OnPreviewKeyUp not being called when the hotkey displays a dialog

			base.OnLostFocus(e);
			_keyReleased = true;
		}

		// Methods

		private async Task PromptForReviewAsync()
		{
			// TODO: Move to IAppDialogService
			var promptForReviewDialog = new ContentDialog
			{
				Title = "ReviewFiles".ToLocalized(),
				Content = "ReviewFilesContent".ToLocalized(),
				PrimaryButtonText = "Yes".ToLocalized(),
				SecondaryButtonText = "No".ToLocalized()
			};

			if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
				promptForReviewDialog.XamlRoot = MainWindow.Instance.Content.XamlRoot;

			var result = await promptForReviewDialog.TryShowAsync();
			if (result is not ContentDialogResult.Primary)
				return;

			try
			{
				var storeContext = StoreContext.GetDefault();
				InitializeWithWindow.Initialize(storeContext, MainWindow.Instance.WindowHandle);
				var storeRateAndReviewResult = await storeContext.RequestRateAndReviewAppAsync();

				UserSettingsService.ApplicationSettingsService.ClickedToReviewApp = true;
			}
			catch (Exception) { }
		}

		private async Task AppRunningAsAdminPromptAsync()
		{
			// TODO: Move to IAppDialogService
			var runningAsAdminPrompt = new ContentDialog
			{
				Title = "FilesRunningAsAdmin".ToLocalized(),
				Content = "FilesRunningAsAdminContent".ToLocalized(),
				PrimaryButtonText = "Ok".ToLocalized(),
				SecondaryButtonText = "DontShowAgain".ToLocalized()
			};

			var result = await runningAsAdminPrompt.TryShowAsync();
			if (result is ContentDialogResult.Secondary)
				UserSettingsService.ApplicationSettingsService.ShowRunningAsAdminPrompt = false;
		}

		private int SetTitleBarDragRegion(InputNonClientPointerSource source, SizeInt32 size, double scaleFactor, Func<UIElement, RectInt32?, RectInt32> getScaledRect)
		{
			var height = (int)TabBar.ActualHeight;

			source.SetRegionRects(
				NonClientRegionKind.Passthrough,
				[getScaledRect(
					this,
					new RectInt32(
						0,
						0,
						(int)(TabBar.ActualWidth + TabBar.Margin.Left - TabBar.DragArea.ActualWidth),
						height))
				]);

			return height;
		}

		private void UpdateStatusBarProperties()
		{
			if (StatusBar is not null)
			{
				StatusBar.StatusBarViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePaneOrColumn.SlimContentPage?.StatusBarViewModel;
				StatusBar.SelectedItemsPropertiesViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePaneOrColumn.SlimContentPage?.SelectedItemsPropertiesViewModel;
			}
		}

		private void UpdateNavToolbarProperties()
		{
			if (AddressToolbar is not null)
				AddressToolbar.ViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePaneOrColumn.ToolbarViewModel;

			if (Toolbar is not null)
				Toolbar.ViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePaneOrColumn.ToolbarViewModel;
		}

		private void UpdatePositioning()
		{
			// NOTE:
			//  Due to an issue with WCT 7.x we couldn't use VisualStateManager.
			//  If we migrated to 8.x we might be able to fix.

			if (InfoPane is null || !ViewModel.ShouldPreviewPaneBeActive)
			{
				PaneRow.MinHeight = 0;
				PaneRow.MaxHeight = double.MaxValue;
				PaneRow.Height = new GridLength(0);
				PaneColumn.MinWidth = 0;
				PaneColumn.MaxWidth = double.MaxValue;
				PaneColumn.Width = new GridLength(0);
			}
			else
			{
				InfoPane.UpdatePosition(RootGrid.ActualWidth, RootGrid.ActualHeight);
				switch (InfoPane.Position)
				{
					case PreviewPanePositions.None:
						PaneRow.MinHeight = 0;
						PaneRow.Height = new GridLength(0);
						PaneColumn.MinWidth = 0;
						PaneColumn.Width = new GridLength(0);
						break;
					case PreviewPanePositions.Right:
						InfoPane.SetValue(Grid.RowProperty, 1);
						InfoPane.SetValue(Grid.ColumnProperty, 2);
						PaneSplitter.SetValue(Grid.RowProperty, 1);
						PaneSplitter.SetValue(Grid.ColumnProperty, 1);
						PaneSplitter.Width = 2;
						PaneSplitter.Height = RootGrid.ActualHeight;
						PaneSplitter.GripperCursor = GridSplitter.GripperCursorType.SizeWestEast;
						PaneSplitter.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast));
						PaneColumn.MinWidth = InfoPane.MinWidth;
						PaneColumn.MaxWidth = InfoPane.MaxWidth;
						PaneColumn.Width = new GridLength(UserSettingsService.InfoPaneSettingsService.VerticalSizePx, GridUnitType.Pixel);
						PaneRow.MinHeight = 0;
						PaneRow.MaxHeight = double.MaxValue;
						PaneRow.Height = new GridLength(0);
						break;
					case PreviewPanePositions.Bottom:
						InfoPane.SetValue(Grid.RowProperty, 3);
						InfoPane.SetValue(Grid.ColumnProperty, 0);
						PaneSplitter.SetValue(Grid.RowProperty, 2);
						PaneSplitter.SetValue(Grid.ColumnProperty, 0);
						PaneSplitter.Height = 2;
						PaneSplitter.Width = RootGrid.ActualWidth;
						PaneSplitter.GripperCursor = GridSplitter.GripperCursorType.SizeNorthSouth;
						PaneSplitter.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth));
						PaneColumn.MinWidth = 0;
						PaneColumn.MaxWidth = double.MaxValue;
						PaneColumn.Width = new GridLength(0);
						PaneRow.MinHeight = InfoPane.MinHeight;
						PaneRow.MaxHeight = InfoPane.MaxHeight;
						PaneRow.Height = new GridLength(UserSettingsService.InfoPaneSettingsService.HorizontalSizePx, GridUnitType.Pixel);
						break;
				}
			}
		}

		private void LoadPaneChanged()
		{
			try
			{
				var isHomePage = !(SidebarAdaptiveViewModel.PaneHolder?.ActivePane?.InstanceViewModel?.IsPageTypeNotHome ?? false);
				var isMultiPane = SidebarAdaptiveViewModel.PaneHolder?.IsMultiPaneActive ?? false;
				var isBigEnough = !App.AppModel.IsMainWindowClosed &&
					(MainWindow.Instance.Bounds.Width > 450 && MainWindow.Instance.Bounds.Height > 450 || RootGrid.ActualWidth > 700 && MainWindow.Instance.Bounds.Height > 360);

				ViewModel.ShouldPreviewPaneBeDisplayed = (!isHomePage || isMultiPane) && isBigEnough;
				ViewModel.ShouldPreviewPaneBeActive = UserSettingsService.InfoPaneSettingsService.IsEnabled && ViewModel.ShouldPreviewPaneBeDisplayed;
				ViewModel.ShouldViewControlBeDisplayed = SidebarAdaptiveViewModel.PaneHolder?.ActivePane?.InstanceViewModel?.IsPageTypeNotHome ?? false;

				UpdatePositioning();
			}
			catch (Exception ex)
			{
				// Handles exception in case WinUI Windows is closed (#15599)
				App.Logger.LogWarning(ex, ex.Message);
			}
		}

		// Event methods

		private void UserSettingsService_OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			switch (e.SettingName)
			{
				case nameof(IInfoPaneSettingsService.IsEnabled):
					LoadPaneChanged();
					break;
			}
		}

		private void HorizontalMultitaskingControl_Loaded(object sender, RoutedEventArgs e)
		{
			TabBar.DragArea.SizeChanged += (_, _) => MainWindow.Instance.RaiseSetTitleBarDragRegion(SetTitleBarDragRegion);

			if (ViewModel.MultitaskingControl is not TabBar)
			{
				ViewModel.MultitaskingControl = TabBar;
				ViewModel.MultitaskingControls.Add(TabBar);
				ViewModel.MultitaskingControl.CurrentInstanceChanged += MultitaskingControl_CurrentInstanceChanged;
			}
		}

		public async void TabItemContent_ContentChanged(object? sender, TabBarItemParameter e)
		{
			if (SidebarAdaptiveViewModel.PaneHolder is null)
				return;

			var paneArgs = e.NavigationParameter as PaneNavigationArguments;
			SidebarAdaptiveViewModel.UpdateSidebarSelectedItemFromArgs(SidebarAdaptiveViewModel.PaneHolder.IsLeftPaneActive ?
				paneArgs?.LeftPaneNavPathParam : paneArgs?.RightPaneNavPathParam);

			UpdateStatusBarProperties();
			LoadPaneChanged();
			UpdateNavToolbarProperties();
			await NavigationHelpers.UpdateInstancePropertiesAsync(paneArgs);

			// Save the updated tab list
			AppLifecycleHelper.SaveSessionTabs();
		}

		public async void MultitaskingControl_CurrentInstanceChanged(object? sender, CurrentInstanceChangedEventArgs e)
		{
			if (SidebarAdaptiveViewModel.PaneHolder is not null)
				SidebarAdaptiveViewModel.PaneHolder.PropertyChanged -= PaneHolder_PropertyChanged;

			var navArgs = e.CurrentInstance.TabBarItemParameter?.NavigationParameter;
			if (e.CurrentInstance is IShellPanesPage currentInstance)
			{
				SidebarAdaptiveViewModel.PaneHolder = currentInstance;
				SidebarAdaptiveViewModel.PaneHolder.PropertyChanged += PaneHolder_PropertyChanged;
			}

			SidebarAdaptiveViewModel.NotifyInstanceRelatedPropertiesChanged((navArgs as PaneNavigationArguments)?.LeftPaneNavPathParam);

			if (SidebarAdaptiveViewModel.PaneHolder?.ActivePaneOrColumn.SlimContentPage?.StatusBarViewModel is not null)
				SidebarAdaptiveViewModel.PaneHolder.ActivePaneOrColumn.SlimContentPage.StatusBarViewModel.ShowLocals = true;

			UpdateStatusBarProperties();
			UpdateNavToolbarProperties();
			LoadPaneChanged();

			e.CurrentInstance.ContentChanged -= TabItemContent_ContentChanged;
			e.CurrentInstance.ContentChanged += TabItemContent_ContentChanged;

			await NavigationHelpers.UpdateInstancePropertiesAsync(navArgs);
		}

		private void PaneHolder_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			SidebarAdaptiveViewModel.NotifyInstanceRelatedPropertiesChanged(SidebarAdaptiveViewModel.PaneHolder.ActivePane?.TabBarItemParameter?.NavigationParameter?.ToString());
			UpdateStatusBarProperties();
			UpdateNavToolbarProperties();
			LoadPaneChanged();
		}

		private void Page_Loaded(object sender, RoutedEventArgs e)
		{
			MainWindow.Instance.AppWindow.Changed += (_, _) => MainWindow.Instance.RaiseSetTitleBarDragRegion(SetTitleBarDragRegion);

			// Load the deferred primary controls to boost startup
			FindName(nameof(TabBar));
			FindName(nameof(AddressToolbar));
			FindName(nameof(Toolbar));
			FindName(nameof(StatusBar));

			// Notify user that drag and drop is disabled
			if (AppLifecycleHelper.AppEnvironment is not AppEnvironment.Dev &&
				isAppRunningAsAdmin &&
				UserSettingsService.ApplicationSettingsService.ShowRunningAsAdminPrompt)
				DispatcherQueue.TryEnqueue(async () => await AppRunningAsAdminPromptAsync());

			// Prompt user to review app in the Store
			if (AppLifecycleHelper.AppEnvironment is not AppEnvironment.Store &&
				UserSettingsService.ApplicationSettingsService.ClickedToReviewApp is false &&
				SystemInformation.Instance.TotalLaunchCount is 15 or 30 or 60)
				DispatcherQueue.TryEnqueue(async () => await PromptForReviewAsync());
		}

		private void PreviewPane_Loaded(object sender, RoutedEventArgs e)
		{
			_updateDateDisplayTimer.Start();
		}

		private void PreviewPane_Unloaded(object sender, RoutedEventArgs e)
		{
			_updateDateDisplayTimer.Stop();
		}

		private void UpdateDateDisplayTimer_Tick(object sender, object e)
		{
			if (!App.AppModel.IsMainWindowClosed)
				InfoPane?.ViewModel.UpdateDateDisplay();
		}

		private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			switch (InfoPane?.Position)
			{
				case PreviewPanePositions.Right when ContentColumn.ActualWidth == ContentColumn.MinWidth:
					UserSettingsService.InfoPaneSettingsService.VerticalSizePx += e.NewSize.Width - e.PreviousSize.Width;
					break;
				case PreviewPanePositions.Bottom when ContentRow.ActualHeight == ContentRow.MinHeight:
					UserSettingsService.InfoPaneSettingsService.HorizontalSizePx += e.NewSize.Height - e.PreviousSize.Height;
					break;
			}

			LoadPaneChanged();
		}

		private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ViewModel.ShouldPreviewPaneBeActive) && ViewModel.ShouldPreviewPaneBeActive)
				await Ioc.Default.GetRequiredService<InfoPaneViewModel>().UpdateSelectedItemPreviewAsync();
		}

		private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
		{
			switch (e.Key)
			{
				case VirtualKey.Menu or VirtualKey.Control or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows:
					break;
				default:
					var currentModifiers = HotKeyHelpers.GetCurrentKeyModifiers();
					HotKey hotKey = new((Keys)e.Key, currentModifiers);

					// Prevents the arrow key events from navigating the list instead of switching compact overlay
					if (Commands[hotKey].Code is CommandCodes.EnterCompactOverlay or CommandCodes.ExitCompactOverlay)
						Focus(FocusState.Keyboard);
					break;
			}
		}

		private void NavToolbar_Loaded(object sender, RoutedEventArgs e)
		{
			UpdateNavToolbarProperties();
		}

		private void InfoPaneSizer_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
		{
			this.ChangeCursor(InputSystemCursor.Create(PaneSplitter.GripperCursor == GridSplitter.GripperCursorType.SizeWestEast ?
				InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth));
		}

		private void InfoPaneSizer_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
		{
			switch (InfoPane?.Position)
			{
				case PreviewPanePositions.Right:
					UserSettingsService.InfoPaneSettingsService.VerticalSizePx = InfoPane.ActualWidth;
					break;
				case PreviewPanePositions.Bottom:
					UserSettingsService.InfoPaneSettingsService.HorizontalSizePx = InfoPane.ActualHeight;
					break;
			}

			this.ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
		}

		private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
		{
			// TODO: Use TwoWay binding
			if (SidebarView.DisplayMode == SidebarDisplayMode.Minimal)
				SidebarView.IsPaneOpen = !SidebarControl.IsPaneOpen;
		}
	}
}
