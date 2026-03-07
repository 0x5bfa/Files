// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Input;
using System.Runtime.InteropServices;
using Windows.System;

namespace Files.App.Controls
{
	public partial class TableViewTextColumn : TableViewBindableColumn
	{
		public TableViewTextColumn()
		{
			DefaultStyleKey = typeof(TableViewTextColumn);
		}

		public override FrameworkElement GenerateElement(object dataItem)
		{
			if (string.IsNullOrEmpty(Binding) ||
				dataItem is not ITableViewCellValueProvider cellValueProvider ||
				cellValueProvider?.GetValue(Binding) is not string cellValue)
				throw new ArgumentException("The type of the argument was invalid.", $"{dataItem}");

			return new TextBlock()
			{
				Style = ElementStyle,
				Text = cellValue,
			};
		}

		public override FrameworkElement GenerateEditingElement(object dataItem)
		{
			if (string.IsNullOrEmpty(Binding) ||
				dataItem is not ITableViewCellValueProvider cellValueProvider ||
				cellValueProvider?.GetValue(Binding) is not string cellValue)
				throw new ArgumentException("The type of the argument was invalid.", $"{dataItem}");

			return new TextBox()
			{
				Style = EditingElementStyle,
				Text = cellValue,
			};
		}

		protected internal override bool CanEdit(object dataItem)
		{
			return !string.IsNullOrEmpty(Binding) &&
				dataItem is ITableViewCellValueEditor;
		}

		protected internal override void PrepareCellForEdit(TableViewCell cell, FrameworkElement editingElement)
		{
			if (editingElement is not TextBox textBox)
				return;

			textBox.Loaded += EditingTextBox_Loaded;
			textBox.LostFocus += EditingTextBox_LostFocus;
			textBox.KeyDown += EditingTextBox_KeyDown;
		}

		protected internal override bool CommitCellEdit(TableViewCell cell)
		{
			if (cell.EditingElement is not TextBox textBox)
				return true;

			if (cell.DataItem is not ITableViewCellValueEditor cellEditor ||
				string.IsNullOrEmpty(Binding))
			{
				DetachEditingTextBox(textBox);
				return true;
			}

			if (!cellEditor.TrySetValue(Binding, textBox.Text))
			{
				textBox.DispatcherQueue.TryEnqueue(() =>
				{
					textBox.Focus(FocusState.Programmatic);
					textBox.SelectAll();
				});
				return false;
			}

			DetachEditingTextBox(textBox);
			return true;
		}

		protected internal override void CancelCellEdit(TableViewCell cell)
		{
			DetachEditingTextBox(cell.EditingElement as TextBox);
		}

		private void EditingTextBox_Loaded(object sender, RoutedEventArgs e)
		{
			if (sender is not TextBox textBox)
				return;

			textBox.Loaded -= EditingTextBox_Loaded;
			textBox.DispatcherQueue.TryEnqueue(() =>
			{
				if (textBox.FindAscendant<TableViewCell>() is not { IsEditing: true } cell ||
					cell.EditingElement != textBox)
				{
					return;
				}

				textBox.Focus(FocusState.Programmatic);
				textBox.SelectAll();
			});
		}

		private void EditingTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			try
			{
				if (sender is not TextBox textBox)
					return;

				var focusedElement = textBox.XamlRoot is null
					? null
					: FocusManager.GetFocusedElement(textBox.XamlRoot);

				// Keep the editor open while its context menu is active.
				if (focusedElement is AppBarButton or Popup)
					return;

				textBox.FindAscendant<TableViewCell>()?.CommitEdit();
			}
			catch (COMException)
			{
			}
		}

		private void EditingTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
		{
			if (sender is not TextBox textBox ||
				textBox.FindAscendant<TableViewCell>() is not { } cell)
			{
				return;
			}

			switch (e.Key)
			{
				case VirtualKey.Enter:
					cell.CommitEdit();
					e.Handled = true;
					break;
				case VirtualKey.Escape:
					cell.CancelEdit();
					e.Handled = true;
					break;
			}
		}

		private void DetachEditingTextBox(TextBox? textBox)
		{
			if (textBox is null)
				return;

			textBox.Loaded -= EditingTextBox_Loaded;
			textBox.LostFocus -= EditingTextBox_LostFocus;
			textBox.KeyDown -= EditingTextBox_KeyDown;
		}
	}
}
