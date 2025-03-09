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
	[TemplatePart(Name = "PART_ModesHostGrid", Type = typeof(Grid))]
	// Visual states
	[TemplateVisualState(Name = "Focused", GroupName = "FocusStates")]
	[TemplateVisualState(Name = "Unfocused", GroupName = "FocusStates")]
	public partial class Omnibar : Control
	{
		private const string ModesHostGrid = "PART_ModesHostGrid";

		private Grid? _modesHostGrid;
		private bool _isFocused;

		public Omnibar()
		{
			DefaultStyleKey = typeof(Omnibar);

			Modes ??= [];
		}

		protected override void OnApplyTemplate()
		{
			_modesHostGrid = GetTemplateChild(ModesHostGrid) as Grid
				?? throw new MissingFieldException($"Could not find {ModesHostGrid} in {nameof(Omnibar)}'s style.");

			if (Modes is null)
				return;

			// Populate the modes1
			foreach (var mode in Modes)
			{
				// Insert a divider
				if (_modesHostGrid.Children.Count is not 0)
				{
					var divider = new Border()
					{
						Width = 1,
						Height = 24,
						//Style = (Style)Application.Current.Resources["DefaultModeDividerStyle"]
					};

					_modesHostGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
					_modesHostGrid.Children.Add(divider);
					Grid.SetColumn(divider, _modesHostGrid.Children.Count - 1);
				}

				// Insert the mode
				_modesHostGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
				_modesHostGrid.Children.Add(mode);
				Grid.SetColumn(mode, _modesHostGrid.Children.Count - 1);
			}

			GotFocus += Omnibar_GotFocus;
			LostFocus += Omnibar_LostFocus;

			UpdateVisualStates();

			base.OnApplyTemplate();
		}

		// Private methods

		private void UpdateVisualStates()
		{
			VisualStateManager.GoToState(
				this,
				_isFocused ? "Focused" : "Normal",
				true);
		}

		// Events

		private void Omnibar_GotFocus(object sender, RoutedEventArgs e)
		{
			_isFocused = true;
			UpdateVisualStates();
		}

		private void Omnibar_LostFocus(object sender, RoutedEventArgs e)
		{
			_isFocused = false;
			UpdateVisualStates();
		}
	}
}
