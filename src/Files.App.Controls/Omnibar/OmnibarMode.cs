// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;
using System.Linq;
using System.Collections.Generic;

namespace Files.App.Controls
{
	public partial class OmnibarMode : Control
	{
		public OmnibarMode()
		{
			DefaultStyleKey = typeof(Omnibar);
		}

		protected override void OnApplyTemplate()
		{
			PointerEntered += OmnibarMode_PointerEntered;
			PointerPressed += OmnibarMode_PointerPressed;
			PointerReleased += OmnibarMode_PointerReleased;
			PointerExited += OmnibarMode_PointerExited;

			base.OnApplyTemplate();
		}

		private void OmnibarMode_PointerExited(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "Normal", true);
		}

		private void OmnibarMode_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerOver", true);
		}

		private void OmnibarMode_PointerPressed(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "Pressed", true);
		}

		private void OmnibarMode_PointerEntered(object sender, PointerRoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "PointerOver", true);
		}
	}
}
