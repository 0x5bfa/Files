// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Files.App.Controls
{
	public sealed class IgnorePreviousItemSelectionStrategy : ItemSelectionStrategy
	{
		public IgnorePreviousItemSelectionStrategy(ICollection<object> selectedItems) : base(selectedItems)
		{
		}

		public override void HandleIntersectionWithItem(object item)
		{
			try
			{
				// Select item intersection with the rectangle
				if (!_selectedItems.Contains(item))
					_selectedItems.Add(item);
			}
			catch (COMException) // The list is being modified (#5325)
			{
			}
		}

		public override void HandleNoIntersectionWithItem(object item)
		{
			try
			{
				_selectedItems.Remove(item);
			}
			catch (COMException) // The list is being modified (#5325)
			{
			}
		}

		public override void StartSelection()
		{
			_selectedItems.Clear();
		}

		public override void HandleNoItemSelected()
		{
			_selectedItems.Clear();
		}
	}
}
