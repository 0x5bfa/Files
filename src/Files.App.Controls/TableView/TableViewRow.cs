// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Foundation;

namespace Files.App.Controls
{
	public partial class TableViewRow : Panel
	{
		private WeakReference<TableView>? _owner;

		public TableViewRow()
		{
			Unloaded += TableViewRow_Unloaded;
		}

		public void SetOwner(TableView owner)
		{
			_owner = new(owner);
		}

		internal void Bind(TableView owner, ITableViewCellValueProvider dataItem)
		{
			EndEditingCells();

			SetOwner(owner);
			Children.Clear();

			foreach (var column in owner.Columns)
			{
				var cell = new TableViewCell();
				cell.Bind(column, dataItem);
				Children.Add(cell);
			}

			InvalidateArrange();
			InvalidateMeasure();
		}

		private void TableViewRow_Unloaded(object sender, RoutedEventArgs e)
		{
			EndEditingCells();
			_owner = null;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (_owner is null || !_owner.TryGetTarget(out var owner))
				return finalSize;

			double x = 0;
			double maxHeight = 0;

			foreach (var child in Children)
				maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);

			for (int index = 0; index < Children.Count; index++)
			{
				var column = owner.Columns[index];
				var child = Children[index];

				child.Arrange(new(
					x,
					0,
					column.ActualWidth,
					maxHeight));

				x += column.ActualWidth;
			}

			return new(x, maxHeight);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (Children.Count is 0 || _owner is null || !_owner.TryGetTarget(out var owner))
				return new(availableSize.Width, 0);

			double maxHeight = 0;

			for (int index = 0; index < Children.Count; index++)
			{
				var child = Children[index];
				var column = owner.Columns[index];

				child.Measure(new(Math.Max(column.ActualWidth - (column.Padding.Left + column.Padding.Right), 0D), availableSize.Height));

				maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
			}

			return new(availableSize.Width, maxHeight);
		}

		private void EndEditingCells()
		{
			foreach (var cell in Children.OfType<TableViewCell>())
				cell.EndEditBeforeRecycle();
		}
	}
}
