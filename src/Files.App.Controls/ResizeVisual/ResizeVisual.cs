// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Input;

namespace Files.App.Controls
{
	public sealed partial class ResizeVisual : Control
	{
		/// <summary>Fires when the Thumb control loses mouse capture.</summary>
		public event DragCompletedEventHandler? DragCompleted;

		/// <summary>Fires one or more times as the mouse pointer is moved when a Thumb control has logical focus and mouse capture.</summary>
		public event DragDeltaEventHandler? DragDelta;

		/// <summary>Fires when a Thumb control receives logical focus and mouse capture.</summary>
		public event DragStartedEventHandler? DragStarted;

		private Grid? _root;
		private Thumb? _thumb;

		private bool _pointerExited;

		[GeneratedDependencyProperty]
		public partial bool FollowPointer { get; set; }

		[GeneratedDependencyProperty]
		public partial double ThumbHeight { get; set; }

		[GeneratedDependencyProperty]
		public partial double ThumbWidth { get; set; }

		public ResizeVisual()
		{
			DefaultStyleKey = typeof(ResizeVisual);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			_root = GetTemplateChild("PART_RootGrid") as Grid
				?? throw new MissingFieldException($"Could not find {"PART_RootGrid"} in the given {nameof(ResizeVisual)}'s style.");
			_thumb = GetTemplateChild("PART_Thumb") as Thumb
				?? throw new MissingFieldException($"Could not find {"PART_Thumb"} in the given {nameof(ResizeVisual)}'s style.");

			Unloaded += DragThumb_Unloaded;

			_root.PointerEntered += Grid_PointerEntered;
			_root.PointerExited += Grid_PointerExited;

			_thumb.PointerEntered += Thumb_PointerEntered;
			_thumb.PointerExited += Thumb_PointerExited;
			_thumb.DragStarted += Thumb_DragStarted;
			_thumb.DragDelta += Thumb_DragDelta;
			_thumb.DragCompleted += Thumb_DragCompleted;
		}

		private void DragThumb_Unloaded(object sender, RoutedEventArgs e)
		{
			if (_root is not null)
			{
				_root.PointerEntered -= Grid_PointerEntered;
				_root.PointerExited -= Grid_PointerExited;
			}

			if (_thumb is not null)
			{
				_thumb.PointerEntered -= Thumb_PointerEntered;
				_thumb.PointerExited -= Thumb_PointerExited;
				_thumb.PointerExited -= Thumb_PointerExited;
				_thumb.DragStarted -= Thumb_DragStarted;
				_thumb.DragDelta -= Thumb_DragDelta;
				_thumb.DragCompleted -= Thumb_DragCompleted;
			}
		}

		private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			if (_thumb is not null && !_thumb.IsDragging)
			{
				VisualStateManager.GoToState(this, "Visible", true);
			}
		}

		private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			if (_thumb is not null && !_thumb.IsDragging)
			{
				VisualStateManager.GoToState(this, "Collapsed", true);
			}
		}

		private void Thumb_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			_pointerExited = false;
		}

		private void Thumb_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			_pointerExited = true;
		}

		private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
		{
			DragStarted?.Invoke(this, e);
		}

		private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
			DragDelta?.Invoke(this, e);
		}

		private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
		{
			DragCompleted?.Invoke(this, e);

			if (_thumb is not null && _pointerExited)
			{
				VisualStateManager.GoToState(this, "Collapsed", true);
				_pointerExited = false;
			}
		}
	}
}
