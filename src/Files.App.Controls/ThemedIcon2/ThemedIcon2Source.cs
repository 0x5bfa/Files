// Copyright (c) Files Community
// SPDX-License-Identifier: MPL-2.0

namespace Files.App.Controls;

/// <summary>
/// Provides a shareable themed icon source that creates <see cref="ThemedIcon2"/> instances.
/// </summary>
public sealed partial class ThemedIcon2Source : AnimatedIconSource
{
	private ThemedIcon2VisualSource _visualSource;

	/// <summary>Initializes a themed icon source.</summary>
	public ThemedIcon2Source()
	{
		_visualSource = new ThemedIcon2VisualSource(ThemedIcon2Data.Default, ThemedIconTypes.Layered, ThemedIconColorType.None, false, false, true, false, null, null, ElementTheme.Default, false);
		Source = _visualSource;
		_ = RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundPropertyChanged);
	}

	/// <inheritdoc />
	protected override IconElement CreateIconElementCore()
	{
		return new ThemedIcon2()
		{
			Data = Data,
			IconType = IconType,
			IconColorType = IconColorType,
			Color = Color,
			IsFilled = IsFilled,
			IsToggled = IsToggled,
			IconSize = IconSize,
			ToggleBehavior = ToggleBehavior,
			IsHighContrast = IsHighContrast,
		};
	}

	/// <inheritdoc />
	protected override DependencyProperty GetIconElementPropertyCore(DependencyProperty iconSourceProperty)
	{
		if (iconSourceProperty == DataProperty)
		{
			return ThemedIcon2.DataProperty;
		}

		if (iconSourceProperty == IconTypeProperty)
		{
			return ThemedIcon2.IconTypeProperty;
		}

		if (iconSourceProperty == IconColorTypeProperty)
		{
			return ThemedIcon2.IconColorTypeProperty;
		}

		if (iconSourceProperty == ColorProperty)
		{
			return ThemedIcon2.ColorProperty;
		}

		if (iconSourceProperty == IsFilledProperty)
		{
			return ThemedIcon2.IsFilledProperty;
		}

		if (iconSourceProperty == IsToggledProperty)
		{
			return ThemedIcon2.IsToggledProperty;
		}

		if (iconSourceProperty == IconSizeProperty)
		{
			return ThemedIcon2.IconSizeProperty;
		}

		if (iconSourceProperty == ToggleBehaviorProperty)
		{
			return ThemedIcon2.ToggleBehaviorProperty;
		}

		if (iconSourceProperty == IsHighContrastProperty)
		{
			return ThemedIcon2.IsHighContrastProperty;
		}

		return base.GetIconElementPropertyCore(iconSourceProperty);
	}

	private void OnForegroundPropertyChanged(DependencyObject sender, DependencyProperty property)
	{
		UpdateAppearance();
	}

	private void UpdateAppearance()
	{
		var isToggled = ToggleBehavior is ToggleBehaviors.On || (ToggleBehavior is ToggleBehaviors.Auto && IsToggled);
		if (!_visualSource.UpdateAppearance(IconType, IconColorType, IsFilled, isToggled, true, IsHighContrast, Foreground, Color, ElementTheme.Default, false))
		{
			_visualSource = new ThemedIcon2VisualSource(Data ?? ThemedIcon2Data.Default, IconType, IconColorType, IsFilled, isToggled, true, IsHighContrast, Foreground, Color, ElementTheme.Default, false);
			Source = _visualSource;
		}
	}

	private void UpdateDataSource()
	{
		var isToggled = ToggleBehavior is ToggleBehaviors.On || (ToggleBehavior is ToggleBehaviors.Auto && IsToggled);
		_visualSource = new ThemedIcon2VisualSource(Data ?? ThemedIcon2Data.Default, IconType, IconColorType, IsFilled, isToggled, true, IsHighContrast, Foreground, Color, ElementTheme.Default, false);
		Source = _visualSource;
	}
}
