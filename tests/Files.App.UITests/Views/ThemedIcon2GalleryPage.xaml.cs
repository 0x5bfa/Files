// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

using Files.App.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.DataTransfer;

namespace Files.App.UITests.Views
{
	public sealed class ThemedIcon2GalleryEntry : INotifyPropertyChanged
	{
		private ThemedIconColorType _iconColorType;
		private double _iconSize = 24;
		private ThemedIconTypes _iconType = ThemedIconTypes.Layered;
		private bool _isFilled;
		private bool _isHighContrast;
		private ElementTheme _theme;

		public string AutomationId => $"ThemedIcon2.Gallery.{ShortName}";

		public string Availability => $"Layers: {(HasLayers ? "Yes" : "No")}; Filled: {(HasFilledData ? "Yes" : "No")}; Outline: {(HasOutlineData ? "Yes" : "No")}";

		public string AvailabilitySummary => $"L:{(HasLayers ? "Y" : "N")}  F:{(HasFilledData ? "Y" : "N")}  O:{(HasOutlineData ? "Y" : "N")}";

		public string Key { get; set; } = string.Empty;

		public string ShortName { get; set; } = string.Empty;

		public ThemedIcon2Data? IconData { get; set; }

		public ThemedIconColorType IconColorType
		{
			get => _iconColorType;
			set => SetProperty(ref _iconColorType, value);
		}

		public double IconSize
		{
			get => _iconSize;
			set => SetProperty(ref _iconSize, value);
		}

		public ThemedIconTypes IconType
		{
			get => _iconType;
			set => SetProperty(ref _iconType, value);
		}

		public bool IsFilled
		{
			get => _isFilled;
			set => SetProperty(ref _isFilled, value);
		}

		public bool IsHighContrast
		{
			get => _isHighContrast;
			set => SetProperty(ref _isHighContrast, value);
		}

		public ElementTheme Theme
		{
			get => _theme;
			set => SetProperty(ref _theme, value);
		}

		private bool HasFilledData => !string.IsNullOrWhiteSpace(IconData?.FilledData);

		private bool HasLayers => IconData?.Layers.Count > 0;

		private bool HasOutlineData => !string.IsNullOrWhiteSpace(IconData?.OutlineData);

		public event PropertyChangedEventHandler? PropertyChanged;

		private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(storage, value))
			{
				return;
			}

			storage = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public sealed partial class ThemedIcon2GalleryPage : Page
	{
		private const string IconKeyPrefix = "App.ThemedIcons2.";
		private readonly IReadOnlyList<ThemedIcon2GalleryEntry> _allIcons;

		public ObservableCollection<ThemedIcon2GalleryEntry> FilteredIcons { get; } = [];

		public ThemedIcon2GalleryPage()
		{
			InitializeComponent();
			_allIcons = BuildEntries();
			ApplyGallerySettings();
			ApplyFilter(string.Empty);
		}

		private static IReadOnlyList<ThemedIcon2GalleryEntry> BuildEntries()
		{
			var iconData = new Dictionary<string, ThemedIcon2Data>(StringComparer.Ordinal);
			CollectIconData(Application.Current.Resources, iconData);

			return iconData
				.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
				.Select(x => new ThemedIcon2GalleryEntry { Key = x.Key, ShortName = x.Key[IconKeyPrefix.Length..], IconData = x.Value, })
				.ToList();
		}

		private static void CollectIconData(ResourceDictionary dictionary, IDictionary<string, ThemedIcon2Data> iconData)
		{
			foreach (var key in dictionary.Keys)
			{
				if (key is not string resourceKey ||
					!resourceKey.StartsWith(IconKeyPrefix, StringComparison.Ordinal) ||
					!dictionary.TryGetValue(key, out var resourceValue) ||
					resourceValue is not ThemedIcon2Data data)
				{
					continue;
				}

				iconData[resourceKey] = data;
			}

			foreach (var mergedDictionary in dictionary.MergedDictionaries)
			{
				CollectIconData(mergedDictionary, iconData);
			}

			foreach (var themeDictionary in dictionary.ThemeDictionaries)
			{
				if (themeDictionary.Value is ResourceDictionary themedResourceDictionary)
				{
					CollectIconData(themedResourceDictionary, iconData);
				}
			}
		}

		private void ApplyFilter(string query)
		{
			FilteredIcons.Clear();
			var trimmed = query.Trim();

			foreach (var entry in _allIcons)
			{
				if (string.IsNullOrEmpty(trimmed) || entry.Key.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
				{
					FilteredIcons.Add(entry);
				}
			}

			ResultCountTextBlock.Text = $"{FilteredIcons.Count} of {_allIcons.Count} icons";
		}

		private void ApplyGallerySettings()
		{
			var iconType = VariantComboBox.SelectedIndex is 2 ? ThemedIconTypes.Outline : ThemedIconTypes.Layered;
			var isFilled = VariantComboBox.SelectedIndex is 1;
			var colorType = (ThemedIconColorType)Math.Clamp(ColorTypeComboBox.SelectedIndex, 0, Enum.GetValues<ThemedIconColorType>().Length - 1);
			var theme = ThemeComboBox.SelectedIndex switch
			{
				1 => ElementTheme.Light,
				2 => ElementTheme.Dark,
				_ => ElementTheme.Default,
			};
			var iconSize = double.IsFinite(IconSizeNumberBox.Value) && IconSizeNumberBox.Value > 0 ? IconSizeNumberBox.Value : 24;

			foreach (var entry in _allIcons)
			{
				entry.IconType = iconType;
				entry.IsFilled = isFilled;
				entry.IconColorType = colorType;
				entry.IconSize = iconSize;
				entry.IsHighContrast = HighContrastToggle.IsOn;
				entry.Theme = theme;
			}
		}

		private void GalleryHighContrastToggle_Toggled(object sender, RoutedEventArgs e)
		{
			if (!IsLoaded)
			{
				return;
			}

			ApplyGallerySettings();
		}

		private void GallerySettings_Changed(object sender, SelectionChangedEventArgs e)
		{
			if (!IsLoaded)
			{
				return;
			}

			ApplyGallerySettings();
		}

		private void GallerySizeNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
		{
			if (!IsLoaded)
			{
				return;
			}

			ApplyGallerySettings();
		}

		private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			ApplyFilter(SearchBox.Text);
		}

		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			SearchBox.Text = string.Empty;
			VariantComboBox.SelectedIndex = 0;
			ColorTypeComboBox.SelectedIndex = 0;
			ThemeComboBox.SelectedIndex = 0;
			IconSizeNumberBox.Value = 24;
			HighContrastToggle.IsOn = false;
			ApplyGallerySettings();
		}

		private async void IconButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button { Tag: string key })
			{
				return;
			}

			var package = new DataPackage();
			package.SetText(key);
			Clipboard.SetContent(package);

			CopiedInfoBar.Message = $"Copied: {key}";
			CopiedInfoBar.IsOpen = true;

			await System.Threading.Tasks.Task.Delay(2500);
			CopiedInfoBar.IsOpen = false;
		}
	}
}
