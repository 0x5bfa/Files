using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Files.App.Controls;

namespace Files.App.Controls
{
	public partial class TableViewTextColumn : TableViewColumn
	{
		public Style? CellStyle { get; private set; }

		public TableViewTextColumn()
		{
			DefaultStyleKey = typeof(TableViewColumn);
		}

		public override FrameworkElement BuildCellElement(object dataItem)
		{
			if (string.IsNullOrEmpty(Binding) ||
				dataItem is not ITableViewCellValueProvider cellValueProvider ||
				cellValueProvider?.GetValue(Binding) is not string cellValue)
				throw new ArgumentException("The type of the argument was invalid.", $"{dataItem}");

			var cell = new TextBlock()
			{
				//Style = CellStyle,
				Text = cellValue,
				TextTrimming = TextTrimming.CharacterEllipsis,
				TextWrapping = TextWrapping.NoWrap,
				Margin = Padding,
			};

			return cell;
		}

		public override FrameworkElement BuildEditCellElement(object dataItem)
		{
			throw new NotImplementedException();
		}

		public override void ApplyStyle(Style style)
		{
			throw new NotImplementedException();
		}
	}
}
