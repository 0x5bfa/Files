// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Files.App.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace Files.App.UITests.Views
{
	public sealed partial class ThemedIcon2Page : Page
	{
		public ThemedIcon2Data AlternateIconData { get; } = new()
		{
			Size = 16,
			OutlineData = "M8 1L15 8L8 15L1 8L8 1ZM8 3.2L3.2 8L8 12.8L12.8 8L8 3.2Z",
			FilledData = "M8 1L15 8L8 15L1 8L8 1Z",
		};

		public ThemedIcon2Data EmptyIconData { get; } = new();

		public ThemedIcon2Data FilledOnlyIconData { get; } = new()
		{
			Size = 16,
			FilledData = "M2 2H14V14H2Z",
		};

		public ThemedIcon2Data LayeredOnlyIconData { get; } = CreateLayeredOnlyIconData();

		public ThemedIcon2Data MalformedIconData { get; } = new()
		{
			Size = 16,
			OutlineData = "not-valid-svg-path",
		};

		public ThemedIcon2Data OutlineOnlyIconData { get; } = new()
		{
			Size = 16,
			OutlineData = "M2 2H14V14H2ZM4 4V12H12V4H4Z",
		};

		public ThemedIcon2Page()
		{
			InitializeComponent();
		}

		private static ThemedIcon2Data CreateLayeredOnlyIconData()
		{
			var data = new ThemedIcon2Data { Size = 16 };
			data.Layers.Add(new ThemedIcon2Layer { LayerType = ThemedIconLayerType.Base, PathData = "M1 1H15V15H1Z", });
			data.Layers.Add(new ThemedIcon2Layer { LayerType = ThemedIconLayerType.Accent, Opacity = 0.75, PathData = "M4 4H12V12H4Z", });

			return data;
		}

		private void DynamicDataToggle_Toggled(object sender, RoutedEventArgs e)
		{
			if (!IsLoaded || !DynamicLoadedToggle.IsOn)
			{
				return;
			}

			ApplyDynamicData();
		}

		private void DynamicSemanticColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!IsLoaded || !DynamicLoadedToggle.IsOn)
			{
				return;
			}

			ApplyDynamicSemanticColor();
		}

		private void DynamicLoadedToggle_Toggled(object sender, RoutedEventArgs e)
		{
			if (!IsLoaded || !DynamicLoadedToggle.IsOn)
			{
				return;
			}

			DispatcherQueue.TryEnqueue(() =>
			{
				ApplyDynamicData();
				ApplyDynamicSemanticColor();
			});
		}

		private void ResetDynamicSampleButton_Click(object sender, RoutedEventArgs e)
		{
			DynamicToggledToggle.IsOn = false;
			DynamicEnabledToggle.IsOn = true;
			DynamicHighContrastToggle.IsOn = false;
			DynamicDataToggle.IsOn = false;
			DynamicLoadedToggle.IsOn = true;
			DynamicSemanticColorComboBox.SelectedIndex = 0;
			DynamicSizeNumberBox.Value = 32;
		}

		private void ApplyDynamicData()
		{
			var data = DynamicDataToggle.IsOn ? AlternateIconData : (ThemedIcon2Data)Resources["IconTest"];
			DynamicLayeredIcon.Data = data;
			DynamicFilledIcon.Data = data;
			DynamicOutlineIcon.Data = data;
		}

		private void ApplyDynamicSemanticColor()
		{
			var colorType = (ThemedIconColorType)Math.Clamp(DynamicSemanticColorComboBox.SelectedIndex, 0, Enum.GetValues<ThemedIconColorType>().Length - 1);
			var customColor = new SolidColorBrush(Colors.DeepPink) { Opacity = 0.8 };
			foreach (var icon in new[] { DynamicLayeredIcon, DynamicFilledIcon, DynamicOutlineIcon })
			{
				icon.IconColorType = colorType;
				icon.Color = customColor;
				icon.Foreground = new SolidColorBrush(Colors.Black);
			}
		}
	}
}
