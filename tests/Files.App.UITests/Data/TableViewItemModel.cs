// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using Files.App.Controls;

namespace Files.App.UITests.Data
{
	internal partial class TableViewItemModel : ObservableObject, ITableViewCellValueProvider, ITableViewCellValueEditor
	{
		[ObservableProperty]
		public partial string? Name { get; set; }

		[ObservableProperty]
		public partial string? DateUpdated { get; set; }

		[ObservableProperty]
		public partial string? Type { get; set; }

		[ObservableProperty]
		public partial string? Size { get; set; }

		public string GetValue(string name)
		{
			return name switch
			{
				nameof(Name) => Name ?? string.Empty,
				nameof(DateUpdated) => DateUpdated ?? string.Empty,
				nameof(Type) => Type ?? string.Empty,
				nameof(Size) => Size ?? string.Empty,
				_ => string.Empty,
			};
		}

		public bool TrySetValue(string name, string value)
		{
			switch (name)
			{
				case nameof(Name):
					Name = value;
					return true;
				case nameof(DateUpdated):
					DateUpdated = value;
					return true;
				case nameof(Type):
					Type = value;
					return true;
				case nameof(Size):
					Size = value;
					return true;
				default:
					return false;
			}
		}
	}
}
