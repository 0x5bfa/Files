// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Files.App.Controls
{
	public partial class TableViewCell : ContentControl
	{
		private static readonly SolidColorBrush TransparentBackground = new(Microsoft.UI.Colors.Transparent);
		private ITableViewCellValueProvider? _dataItem;
		private DateTimeOffset _lastClickTimestamp;

		internal TableViewColumn? Column { get; private set; }
		internal ITableViewCellValueProvider? DataItem => _dataItem;
		internal FrameworkElement? EditingElement => IsEditing ? Content as FrameworkElement : null;
		internal bool IsEditing { get; private set; }

		public TableViewCell()
		{
			Background = TransparentBackground;
			HorizontalContentAlignment = HorizontalAlignment.Stretch;
			VerticalContentAlignment = VerticalAlignment.Stretch;
			IsTabStop = false;

			PointerReleased += TableViewCell_PointerReleased;
		}

		internal void Bind(TableViewColumn column, ITableViewCellValueProvider dataItem)
		{
			EndEditBeforeRecycle();

			Column = column;
			_dataItem = dataItem;
			IsEditing = false;
			ResetEditGesture();
			Content = column.GenerateElement(dataItem);
		}

		internal void EndEditBeforeRecycle()
		{
			if (!IsEditing)
				return;

			if (!CommitEdit())
				CancelEdit();
		}

		internal bool BeginEdit()
		{
			if (IsEditing || Column is null || _dataItem is null || !Column.CanEdit(_dataItem))
				return false;

			var editingElement = Column.GenerateEditingElement(_dataItem);
			Content = editingElement;
			IsEditing = true;
			ResetEditGesture();
			Column.PrepareCellForEdit(this, editingElement);
			return true;
		}

		internal bool CommitEdit()
		{
			if (!IsEditing || Column is null)
				return false;

			if (!Column.CommitCellEdit(this))
				return false;

			IsEditing = false;
			ResetEditGesture();
			Content = _dataItem is null ? null : Column.GenerateElement(_dataItem);
			return true;
		}

		internal void CancelEdit()
		{
			if (!IsEditing || Column is null)
				return;

			Column.CancelCellEdit(this);
			IsEditing = false;
			ResetEditGesture();
			Content = _dataItem is null ? null : Column.GenerateElement(_dataItem);
		}

		private void TableViewCell_PointerReleased(object sender, PointerRoutedEventArgs e)
		{
			if (Column is null ||
				_dataItem is null ||
				IsEditing ||
				!Column.CanEdit(_dataItem) ||
				e.Pointer.PointerDeviceType is not PointerDeviceType.Mouse ||
				e.GetCurrentPoint(this).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonReleased)
			{
				return;
			}

			var now = DateTimeOffset.UtcNow;
			if (_lastClickTimestamp != default &&
				now - _lastClickTimestamp <= Column.EditDoubleClickInterval)
			{
				ResetEditGesture();
				if (BeginEdit())
					e.Handled = true;
				return;
			}

			_lastClickTimestamp = now;
		}

		private void ResetEditGesture()
		{
			_lastClickTimestamp = default;
		}
	}
}

