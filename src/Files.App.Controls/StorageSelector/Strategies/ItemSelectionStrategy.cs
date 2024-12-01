// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using System.Collections.Generic;

namespace Files.App.Controls
{
	public abstract class ItemSelectionStrategy
	{
		protected readonly ICollection<object> _selectedItems;

		protected ItemSelectionStrategy(ICollection<object> selectedItems)
		{
			_selectedItems = selectedItems;
		}

		public abstract void HandleIntersectionWithItem(object item);

		public abstract void HandleNoIntersectionWithItem(object item);

		public virtual void StartSelection()
		{
		}

		public virtual void HandleNoItemSelected()
		{
		}
	}
}
