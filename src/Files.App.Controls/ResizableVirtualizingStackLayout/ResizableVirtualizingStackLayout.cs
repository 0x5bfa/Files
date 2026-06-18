// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Collections.Specialized;
using Windows.Foundation;

namespace Files.App.Controls;

/// <summary>
/// A virtualizing stack layout that sizes each item from its measured extent.
/// </summary>
public partial class ResizableVirtualizingStackLayout : VirtualizingLayout
{
	private readonly Dictionary<int, Size> _extentCache = [];

	private int _firstRealizedIndex;
	private int _lastRealizedIndex = -1;

	public static readonly DependencyProperty OrientationProperty =
		DependencyProperty.Register(
			nameof(Orientation),
			typeof(Orientation),
			typeof(ResizableVirtualizingStackLayout),
			new PropertyMetadata(Orientation.Vertical, OnOrientationPropertyChanged));

	public static readonly DependencyProperty SpacingProperty =
		DependencyProperty.Register(
			nameof(Spacing),
			typeof(double),
			typeof(ResizableVirtualizingStackLayout),
			new PropertyMetadata(0d, OnLayoutPropertyChanged));

	public static readonly DependencyProperty EstimatedItemExtentProperty =
		DependencyProperty.Register(
			nameof(EstimatedItemExtent),
			typeof(double),
			typeof(ResizableVirtualizingStackLayout),
			new PropertyMetadata(48d, OnLayoutPropertyChanged));

	public Orientation Orientation
	{
		get => (Orientation)GetValue(OrientationProperty);
		set => SetValue(OrientationProperty, value);
	}

	public double Spacing
	{
		get => (double)GetValue(SpacingProperty);
		set => SetValue(SpacingProperty, value);
	}

	public double EstimatedItemExtent
	{
		get => (double)GetValue(EstimatedItemExtentProperty);
		set => SetValue(EstimatedItemExtentProperty, value);
	}

	public ResizableVirtualizingStackLayout()
	{
		UpdateIndexBasedLayoutOrientation();
	}

	public double GetEstimatedExtent(int index)
	{
		return _extentCache.TryGetValue(index, out var size)
			? GetPrimarySize(size)
			: Math.Max(EstimatedItemExtent, 1);
	}

	protected override Size MeasureOverride(VirtualizingLayoutContext context, Size availableSize)
	{
		var itemCount = context.ItemCount;
		if (itemCount is 0)
		{
			ResetRealizedRange();
			return new Size();
		}

		TrimCache(itemCount);

		var realizationRect = context.RealizationRect;
		var realizationStart = GetPrimaryStart(realizationRect);
		var realizationEnd = GetPrimaryEnd(realizationRect);

		if (!IsUsableRealizationWindow(realizationStart, realizationEnd))
		{
			realizationStart = 0;
			realizationEnd = GetAvailablePrimarySize(availableSize);

			if (double.IsInfinity(realizationEnd) || realizationEnd <= 0)
				realizationEnd = GetEstimatedTotalExtent(itemCount);
		}

		_firstRealizedIndex = GetFirstRealizedIndex(itemCount, realizationStart);
		_lastRealizedIndex = GetLastRealizedIndex(itemCount, realizationEnd);

		var measureSize = Orientation is Orientation.Horizontal
			? new Size(double.PositiveInfinity, availableSize.Height)
			: new Size(availableSize.Width, double.PositiveInfinity);

		for (var index = _firstRealizedIndex; index <= _lastRealizedIndex; index++)
		{
			var element = context.GetOrCreateElementAt(index);
			element.Measure(measureSize);
			_extentCache[index] = element.DesiredSize;
		}

		return GetDesiredSize(itemCount);
	}

	protected override Size ArrangeOverride(VirtualizingLayoutContext context, Size finalSize)
	{
		if (_lastRealizedIndex < _firstRealizedIndex)
			return finalSize;

		var isHorizontal = Orientation is Orientation.Horizontal;
		var offset = GetOffsetForIndex(_firstRealizedIndex);

		for (var index = _firstRealizedIndex; index <= _lastRealizedIndex; index++)
		{
			var element = context.GetOrCreateElementAt(index);
			var size = GetCachedOrEstimatedSize(index);
			var primarySize = Math.Max(GetPrimarySize(size), 0);

			var bounds = isHorizontal
				? new Rect(offset, 0, primarySize, finalSize.Height)
				: new Rect(0, offset, finalSize.Width, primarySize);

			element.Arrange(bounds);
			offset += primarySize + (index < context.ItemCount - 1 ? Spacing : 0);
		}

		return finalSize;
	}

	protected override void OnItemsChangedCore(VirtualizingLayoutContext context, object source, NotifyCollectionChangedEventArgs args)
	{
		base.OnItemsChangedCore(context, source, args);

		_extentCache.Clear();
		ResetRealizedRange();
		InvalidateMeasure();
	}

	private static void OnOrientationPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is not ResizableVirtualizingStackLayout layout)
			return;

		layout._extentCache.Clear();
		layout.UpdateIndexBasedLayoutOrientation();
		layout.InvalidateMeasure();
	}

	private static void OnLayoutPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is not ResizableVirtualizingStackLayout layout)
			return;

		layout.InvalidateMeasure();
	}

	private void UpdateIndexBasedLayoutOrientation()
	{
		SetIndexBasedLayoutOrientation(Orientation is Orientation.Horizontal
			? IndexBasedLayoutOrientation.LeftToRight
			: IndexBasedLayoutOrientation.TopToBottom);
	}

	private Size GetDesiredSize(int itemCount)
	{
		var primary = GetEstimatedTotalExtent(itemCount);
		double secondary = 0;

		foreach (var size in _extentCache.Values)
			secondary = Math.Max(secondary, GetSecondarySize(size));

		return Orientation is Orientation.Horizontal
			? new Size(primary, secondary)
			: new Size(secondary, primary);
	}

	private Size GetCachedOrEstimatedSize(int index)
	{
		return _extentCache.TryGetValue(index, out var size)
			? size
			: GetEstimatedSize();
	}

	private Size GetEstimatedSize()
	{
		var estimatedExtent = Math.Max(EstimatedItemExtent, 1);

		return Orientation is Orientation.Horizontal
			? new Size(estimatedExtent, 0)
			: new Size(0, estimatedExtent);
	}

	private double GetEstimatedTotalExtent(int itemCount)
	{
		if (itemCount <= 0)
			return 0;

		double extent = 0;

		for (var index = 0; index < itemCount; index++)
			extent += GetPrimarySize(GetCachedOrEstimatedSize(index));

		return extent + Spacing * Math.Max(0, itemCount - 1);
	}

	private int GetFirstRealizedIndex(int itemCount, double realizationStart)
	{
		double offset = 0;

		for (var index = 0; index < itemCount; index++)
		{
			var extent = GetPrimarySize(GetCachedOrEstimatedSize(index));
			var nextOffset = offset + extent + (index < itemCount - 1 ? Spacing : 0);

			if (nextOffset >= realizationStart)
				return index;

			offset = nextOffset;
		}

		return Math.Max(0, itemCount - 1);
	}

	private int GetLastRealizedIndex(int itemCount, double realizationEnd)
	{
		double offset = 0;

		for (var index = 0; index < itemCount; index++)
		{
			offset += GetPrimarySize(GetCachedOrEstimatedSize(index));

			if (offset >= realizationEnd)
				return index;

			offset += index < itemCount - 1 ? Spacing : 0;
		}

		return Math.Max(0, itemCount - 1);
	}

	private double GetOffsetForIndex(int targetIndex)
	{
		double offset = 0;

		for (var index = 0; index < targetIndex; index++)
			offset += GetPrimarySize(GetCachedOrEstimatedSize(index)) + Spacing;

		return offset;
	}

	private void TrimCache(int itemCount)
	{
		foreach (var index in _extentCache.Keys.Where(index => index >= itemCount).ToList())
			_extentCache.Remove(index);
	}

	private void ResetRealizedRange()
	{
		_firstRealizedIndex = 0;
		_lastRealizedIndex = -1;
	}

	private bool IsUsableRealizationWindow(double realizationStart, double realizationEnd)
	{
		return !double.IsNaN(realizationStart) &&
			!double.IsNaN(realizationEnd) &&
			!double.IsInfinity(realizationStart) &&
			!double.IsInfinity(realizationEnd) &&
			realizationEnd > realizationStart;
	}

	private double GetAvailablePrimarySize(Size availableSize)
	{
		return Orientation is Orientation.Horizontal
			? availableSize.Width
			: availableSize.Height;
	}

	private double GetPrimaryStart(Rect rect)
	{
		return Orientation is Orientation.Horizontal
			? rect.X
			: rect.Y;
	}

	private double GetPrimaryEnd(Rect rect)
	{
		return Orientation is Orientation.Horizontal
			? rect.Right
			: rect.Bottom;
	}

	private double GetPrimarySize(Size size)
	{
		return Orientation is Orientation.Horizontal
			? size.Width
			: size.Height;
	}

	private double GetSecondarySize(Size size)
	{
		return Orientation is Orientation.Horizontal
			? size.Height
			: size.Width;
	}
}
