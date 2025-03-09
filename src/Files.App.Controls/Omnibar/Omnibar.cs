// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Shapes;
using System.Linq;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Input;

namespace Files.App.Controls
{
	// Content
	[ContentProperty(Name = nameof(Modes))]
	// Template parts
	[TemplatePart(Name = "ModesItemsRepeater", Type = typeof(ItemsRepeater))]
	// Visual states
	[TemplateVisualState(Name = "Focused", GroupName = "FocusStates")]
	[TemplateVisualState(Name = "Unfocused", GroupName = "FocusStates")]
	public partial class Omnibar : Control
	{
		private const string ModesItemsRepeater = "ModesItemsRepeater";

		private ItemsRepeater? _modesItemsRepeater;

		public Omnibar()
		{
			DefaultStyleKey = typeof(Omnibar);

			Modes ??= [];
		}

		protected override void OnApplyTemplate()
		{
			_modesItemsRepeater = GetTemplateChild(ModesItemsRepeater) as ItemsRepeater
				?? throw new MissingFieldException($"Could not find {ModesItemsRepeater} in {nameof(Omnibar)}'s style.");

			_modesItemsRepeater.ItemsSource = Modes;

			GotFocus += Omnibar_GotFocus;
			LostFocus += Omnibar_LostFocus;

			base.OnApplyTemplate();
		}

		private void Omnibar_GotFocus(object sender, RoutedEventArgs e)
		{
			VisualStateManager.GoToState(this, "Focused", true);
		}

		private void Omnibar_LostFocus(object sender, RoutedEventArgs e)
		{
			var focus = FocusState;

			VisualStateManager.GoToState(this, "Unfocused", true);
		}
	}
}
