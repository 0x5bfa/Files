// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Files.App.Controls;

/// <summary>
/// An <see cref="ItemsRepeater"/> that lets realized items be reordered with drag gestures.
/// </summary>
public partial class ReorderableItemsRepeater : ItemsRepeater
{
	private const double AutoScrollActivationMargin = 40;
	private const double AutoScrollMaxStep = 18;

	private bool _isDragging;
	private bool _isSnapping;
	private bool _isHorizontal;

	private UIElement? _dragItem;
	private int _dragItemOriginalIndex = -1;
	private TranslateTransform? _dragItemTransform;
	private ScrollViewer? _ancestorScrollViewer;

	private double _spacing;
	private double[]? _originalPositions;
	private double[]? _itemExtents;
	private UIElement?[]? _realizedElements;
	private TranslateTransform?[]? _itemTransforms;
	private List<int>? _logicalOrder;
	private Storyboard?[]? _displacementStoryboards;
	private double[]? _displacementTargets;

	public event EventHandler<ReorderedItemsEventArgs>? Reordered;

	public ReorderableItemsRepeater()
	{
		ElementPrepared += OnElementPrepared;
		ElementClearing += OnElementClearing;
	}

	private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
	{
		var element = args.Element;

		element.ManipulationMode = ManipulationModes.System | ManipulationModes.TranslateX | ManipulationModes.TranslateY;
		element.ManipulationStarting -= OnItemManipulationStarting;
		element.ManipulationDelta -= OnItemManipulationDelta;
		element.ManipulationCompleted -= OnItemManipulationCompleted;
		element.ManipulationStarting += OnItemManipulationStarting;
		element.ManipulationDelta += OnItemManipulationDelta;
		element.ManipulationCompleted += OnItemManipulationCompleted;
	}

	private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
	{
		var element = args.Element;

		element.ManipulationStarting -= OnItemManipulationStarting;
		element.ManipulationDelta -= OnItemManipulationDelta;
		element.ManipulationCompleted -= OnItemManipulationCompleted;
	}

	private void OnItemManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
	{
		if (sender is not UIElement dragElement || _isSnapping)
			return;

		var itemCount = ItemsSourceView.Count;
		if (itemCount < 2)
			return;

		var dragItemOriginalIndex = GetElementIndex(dragElement);
		if (dragItemOriginalIndex < 0)
			return;

		_ancestorScrollViewer ??= TryFindAncestorScrollViewer(dragElement);
		_isHorizontal = GetLayoutOrientation() is Orientation.Horizontal;
		_spacing = GetLayoutSpacing();

		e.Mode = _isHorizontal ? ManipulationModes.TranslateX : ManipulationModes.TranslateY;

		_dragItem = dragElement;
		_dragItemOriginalIndex = dragItemOriginalIndex;
		_isDragging = true;

		_originalPositions = new double[itemCount];
		_itemExtents = new double[itemCount];
		_realizedElements = new UIElement[itemCount];
		_itemTransforms = new TranslateTransform[itemCount];
		_logicalOrder = [.. Enumerable.Range(0, itemCount)];
		_displacementStoryboards = new Storyboard[itemCount];
		_displacementTargets = new double[itemCount];

		var fallbackExtent = GetFallbackExtent(itemCount);
		double position = 0;

		for (var i = 0; i < itemCount; i++)
		{
			var element = TryGetElement(i);
			var extent = GetElementExtent(element);

			if (extent <= 0)
				extent = GetLayoutEstimatedExtent(i);

			if (extent <= 0)
				extent = fallbackExtent;

			_originalPositions[i] = position;
			_itemExtents[i] = extent;
			_realizedElements[i] = element;

			if (element is not null)
			{
				var transform = new TranslateTransform();
				_itemTransforms[i] = transform;
				element.RenderTransform = transform;
			}

			position += extent + (i < itemCount - 1 ? _spacing : 0);
		}

		_dragItemTransform = _itemTransforms[_dragItemOriginalIndex];
		Canvas.SetZIndex(_dragItem, 100);

		e.Handled = true;
	}

	private void OnItemManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
	{
		if (!_isDragging || _dragItemTransform is null || _logicalOrder is null ||
			_originalPositions is null || _itemExtents is null || _itemTransforms is null)
			return;

		_ = _isHorizontal
			? _dragItemTransform.X += e.Delta.Translation.X
			: _dragItemTransform.Y += e.Delta.Translation.Y;

		if (sender is UIElement senderElement)
			TryAutoScrollDuringDrag(senderElement, e.Position);

		var dragCenterPosition =
			_originalPositions[_dragItemOriginalIndex] +
			_itemExtents[_dragItemOriginalIndex] / 2.0 +
			(_isHorizontal ? _dragItemTransform.X : _dragItemTransform.Y);

		var anySwap = false;
		bool swapped;

		do
		{
			swapped = false;
			var currentPosition = _logicalOrder.IndexOf(_dragItemOriginalIndex);
			var targetPositions = ComputeTargetPositions();

			if (currentPosition < _logicalOrder.Count - 1)
			{
				var nextItemIndex = _logicalOrder[currentPosition + 1];
				var nextMidpoint = targetPositions[nextItemIndex] + _itemExtents[nextItemIndex] / 2.0;

				if (dragCenterPosition > nextMidpoint)
				{
					_logicalOrder[currentPosition] = nextItemIndex;
					_logicalOrder[currentPosition + 1] = _dragItemOriginalIndex;
					swapped = true;
					anySwap = true;
				}
			}

			if (!swapped && currentPosition > 0)
			{
				var previousIndex = _logicalOrder[currentPosition - 1];
				var previousCenterPosition = targetPositions[previousIndex] + _itemExtents[previousIndex] / 2.0;

				if (dragCenterPosition < previousCenterPosition)
				{
					_logicalOrder[currentPosition] = previousIndex;
					_logicalOrder[currentPosition - 1] = _dragItemOriginalIndex;
					swapped = true;
					anySwap = true;
				}
			}
		}
		while (swapped);

		if (anySwap)
			UpdateDisplacedItemTransforms();

		e.Handled = true;
	}

	private void OnItemManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
	{
		if (!_isDragging || _dragItemTransform is null || _logicalOrder is null ||
			_originalPositions is null || _itemExtents is null)
			return;

		_isDragging = false;
		_isSnapping = true;

		var targetPositions = ComputeTargetPositions();
		var snapTarget = targetPositions[_dragItemOriginalIndex] - _originalPositions[_dragItemOriginalIndex];

		var snapAnimation = new DoubleAnimation
		{
			To = snapTarget,
			Duration = new Duration(TimeSpan.FromSeconds(0.25)),
			EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
		};

		var storyboard = new Storyboard();
		Storyboard.SetTarget(snapAnimation, _dragItemTransform);
		Storyboard.SetTargetProperty(snapAnimation, _isHorizontal ? "X" : "Y");
		storyboard.Children.Add(snapAnimation);
		storyboard.Completed += OnSnapAnimationCompleted;
		storyboard.Begin();

		e.Handled = true;
	}

	private void OnSnapAnimationCompleted(object? sender, object e)
	{
		if (_dragItem is not null)
			Canvas.SetZIndex(_dragItem, 0);

		var orderChanged = false;
		if (_logicalOrder is not null)
		{
			for (var i = 0; i < _logicalOrder.Count; i++)
			{
				if (_logicalOrder[i] != i)
				{
					orderChanged = true;
					break;
				}
			}
		}

		if (_displacementStoryboards is not null)
		{
			foreach (var storyboard in _displacementStoryboards)
				storyboard?.Stop();
		}

		if (_realizedElements is not null)
		{
			foreach (var element in _realizedElements)
			{
				if (element is not null)
					element.RenderTransform = null;
			}
		}

		if (orderChanged && _logicalOrder is not null)
		{
			int[] reorderedIndexMap = [.. _logicalOrder];

			if (TryCommitReorderToItemsSource(reorderedIndexMap))
				Reordered?.Invoke(this, new ReorderedItemsEventArgs(reorderedIndexMap));
		}

		ResetDragState();
	}

	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Move is discovered dynamically to support ObservableCollection<T> ItemsSource.")]
	private bool TryCommitReorderToItemsSource(int[] reorderedIndexMap)
	{
		if (ItemsSource is not IList itemsSource ||
			itemsSource.IsReadOnly ||
			itemsSource.IsFixedSize ||
			itemsSource.Count != reorderedIndexMap.Length)
			return false;

		var moveMethod = itemsSource.GetType().GetMethod(
			"Move",
			BindingFlags.Instance | BindingFlags.Public,
			null,
			[typeof(int), typeof(int)],
			null);

		var currentOrder = Enumerable.Range(0, reorderedIndexMap.Length).ToList();

		try
		{
			for (var targetIndex = 0; targetIndex < reorderedIndexMap.Length; targetIndex++)
			{
				var desiredOldIndex = reorderedIndexMap[targetIndex];
				var currentIndex = currentOrder.IndexOf(desiredOldIndex);
				if (currentIndex == targetIndex)
					continue;

				if (moveMethod is not null)
				{
					_ = moveMethod.Invoke(itemsSource, [currentIndex, targetIndex]);
				}
				else
				{
					var movedItem = itemsSource[currentIndex];
					itemsSource.RemoveAt(currentIndex);
					itemsSource.Insert(targetIndex, movedItem);
				}

				currentOrder.RemoveAt(currentIndex);
				currentOrder.Insert(targetIndex, desiredOldIndex);
			}

			return true;
		}
		catch (NotSupportedException)
		{
			return false;
		}
		catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException)
		{
			return false;
		}
	}

	private static ScrollViewer? TryFindAncestorScrollViewer(DependencyObject? element)
	{
		while (element is not null)
		{
			if (element is ScrollViewer scrollViewer)
				return scrollViewer;

			element = VisualTreeHelper.GetParent(element);
		}

		return null;
	}

	private void TryAutoScrollDuringDrag(UIElement senderElement, Point pointerPositionInSender)
	{
		if (_ancestorScrollViewer is null || _dragItemTransform is null)
			return;

		var transformToScrollViewer = senderElement.TransformToVisual(_ancestorScrollViewer);
		var pointerInViewport = transformToScrollViewer.TransformPoint(pointerPositionInSender);

		if (_isHorizontal)
		{
			var horizontalDelta = ComputeAutoScrollDelta(
				pointerInViewport.X,
				_ancestorScrollViewer.ViewportWidth,
				_ancestorScrollViewer.HorizontalOffset,
				_ancestorScrollViewer.ScrollableWidth);

			if (horizontalDelta == 0)
				return;

			var newHorizontalOffset = _ancestorScrollViewer.HorizontalOffset + horizontalDelta;
			var didScroll = _ancestorScrollViewer.ChangeView(newHorizontalOffset, null, null, true);
			if (didScroll)
				_dragItemTransform.X += horizontalDelta;
		}
		else
		{
			var verticalDelta = ComputeAutoScrollDelta(
				pointerInViewport.Y,
				_ancestorScrollViewer.ViewportHeight,
				_ancestorScrollViewer.VerticalOffset,
				_ancestorScrollViewer.ScrollableHeight);

			if (verticalDelta == 0)
				return;

			var newVerticalOffset = _ancestorScrollViewer.VerticalOffset + verticalDelta;
			var didScroll = _ancestorScrollViewer.ChangeView(null, newVerticalOffset, null, true);
			if (didScroll)
				_dragItemTransform.Y += verticalDelta;
		}
	}

	private static double ComputeAutoScrollDelta(double pointerPosition, double viewportSize, double currentOffset, double scrollableSize)
	{
		if (viewportSize <= 0 || scrollableSize <= 0)
			return 0;

		double delta = 0;

		if (pointerPosition < AutoScrollActivationMargin && currentOffset > 0)
		{
			var overscroll = AutoScrollActivationMargin - pointerPosition;
			var strength = Math.Clamp(overscroll / AutoScrollActivationMargin, 0, 1);
			delta = -Math.Clamp(strength * AutoScrollMaxStep, 1, AutoScrollMaxStep);
		}
		else if (pointerPosition > viewportSize - AutoScrollActivationMargin && currentOffset < scrollableSize)
		{
			var overscroll = pointerPosition - (viewportSize - AutoScrollActivationMargin);
			var strength = Math.Clamp(overscroll / AutoScrollActivationMargin, 0, 1);
			delta = Math.Clamp(strength * AutoScrollMaxStep, 1, AutoScrollMaxStep);
		}

		if (delta is 0)
			return 0;

		var targetOffset = Math.Clamp(currentOffset + delta, 0, scrollableSize);
		return targetOffset - currentOffset;
	}

	private double[] ComputeTargetPositions()
	{
		var count = _logicalOrder!.Count;
		var positions = new double[count];
		double position = 0;

		for (var i = 0; i < count; i++)
		{
			var itemIndex = _logicalOrder[i];
			positions[itemIndex] = position;
			position += _itemExtents![itemIndex] + (i < count - 1 ? _spacing : 0);
		}

		return positions;
	}

	private void UpdateDisplacedItemTransforms()
	{
		var targetPositions = ComputeTargetPositions();

		for (var i = 0; i < _logicalOrder!.Count; i++)
		{
			var itemIndex = _logicalOrder[i];
			if (itemIndex == _dragItemOriginalIndex)
				continue;

			var transform = _itemTransforms![itemIndex];
			if (transform is null)
				continue;

			var target = targetPositions[itemIndex] - _originalPositions![itemIndex];

			if (_displacementTargets![itemIndex] == target)
				continue;

			var previousStoryboard = _displacementStoryboards![itemIndex];
			if (previousStoryboard is not null)
			{
				previousStoryboard.Stop();

				var delta = _displacementTargets[itemIndex];
				_ = _isHorizontal ? transform.X = delta : transform.Y = delta;
			}

			_displacementTargets[itemIndex] = target;

			var animation = new DoubleAnimation
			{
				To = target,
				Duration = new Duration(TimeSpan.FromSeconds(0.25)),
				EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut }
			};

			var storyboard = new Storyboard();
			Storyboard.SetTarget(animation, transform);
			Storyboard.SetTargetProperty(animation, _isHorizontal ? "X" : "Y");
			storyboard.Children.Add(animation);
			_displacementStoryboards[itemIndex] = storyboard;
			storyboard.Begin();
		}
	}

	private Orientation GetLayoutOrientation()
	{
		return Layout switch
		{
			ResizableVirtualizingStackLayout layout => layout.Orientation,
			StackLayout layout => layout.Orientation,
			_ => Orientation.Vertical,
		};
	}

	private double GetLayoutSpacing()
	{
		return Layout switch
		{
			ResizableVirtualizingStackLayout layout => layout.Spacing,
			StackLayout layout => layout.Spacing,
			_ => 0,
		};
	}

	private double GetLayoutEstimatedExtent(int index)
	{
		return Layout is ResizableVirtualizingStackLayout layout
			? layout.GetEstimatedExtent(index)
			: 0;
	}

	private double GetFallbackExtent(int itemCount)
	{
		double totalExtent = 0;
		var realizedCount = 0;

		for (var i = 0; i < itemCount; i++)
		{
			var extent = GetElementExtent(TryGetElement(i));
			if (extent <= 0)
				continue;

			totalExtent += extent;
			realizedCount++;
		}

		return realizedCount > 0
			? totalExtent / realizedCount
			: 1;
	}

	private double GetElementExtent(UIElement? element)
	{
		if (element is null)
			return 0;

		return _isHorizontal
			? element.ActualSize.X
			: element.ActualSize.Y;
	}

	private void ResetDragState()
	{
		_dragItem = null;
		_dragItemOriginalIndex = -1;
		_dragItemTransform = null;
		_ancestorScrollViewer = null;
		_originalPositions = null;
		_itemExtents = null;
		_realizedElements = null;
		_itemTransforms = null;
		_logicalOrder = null;
		_displacementStoryboards = null;
		_displacementTargets = null;
		_spacing = 0;
		_isSnapping = false;
	}
}
