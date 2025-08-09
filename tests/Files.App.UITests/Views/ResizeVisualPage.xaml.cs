// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Files.App.UITests.Views
{
	public sealed partial class ResizeVisualPage : Page
	{
		public ResizeVisualPage()
		{
			InitializeComponent();
		}

		private void Page_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
		{
			Thumb1.RenderTransform = new TranslateTransform();
			((TranslateTransform)Thumb1.RenderTransform).X = Rectangle1.Width - Thumb1.ActualWidth / 2;

			Thumb2.RenderTransform = new TranslateTransform();
			((TranslateTransform)Thumb2.RenderTransform).X = Rectangle2.Width - Thumb2.ActualWidth / 2;
		}

		private void Thumb1_DragStarted(object sender, DragStartedEventArgs e) { }

		private void Thumb1_DragDelta(object sender, DragDeltaEventArgs e)
		{
			if (Rectangle1.MinWidth > Rectangle1.Width + e.HorizontalChange ||
				Rectangle1.MaxWidth < Rectangle1.Width + e.HorizontalChange)
				return;

			Rectangle1.Width += e.HorizontalChange;
			((TranslateTransform)Thumb1.RenderTransform).X = Rectangle1.Width - Thumb1.ActualWidth / 2;
		}

		private void Thumb1_DragCompleted(object sender, DragCompletedEventArgs e) { }

		private void Thumb2_DragStarted(object sender, DragStartedEventArgs e) { }

		private void Thumb2_DragDelta(object sender, DragDeltaEventArgs e)
		{
			if (Rectangle2.MinWidth > Rectangle2.Width + e.HorizontalChange ||
				Rectangle2.MaxWidth < Rectangle2.Width + e.HorizontalChange)
				return;

			Rectangle2.Width += e.HorizontalChange;
			((TranslateTransform)Thumb2.RenderTransform).X = Rectangle2.Width - Thumb2.ActualWidth / 2;
		}

		private void Thumb2_DragCompleted(object sender, DragCompletedEventArgs e) { }

		private void Page_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
		{
			Loaded -= Page_Loaded;
			Thumb1.DragStarted -= Thumb1_DragStarted;
			Thumb1.DragDelta -= Thumb1_DragDelta;
			Thumb1.DragCompleted -= Thumb1_DragCompleted;
			Thumb2.DragStarted -= Thumb2_DragStarted;
			Thumb2.DragDelta -= Thumb2_DragDelta;
			Thumb2.DragCompleted -= Thumb2_DragCompleted;
		}
	}
}
