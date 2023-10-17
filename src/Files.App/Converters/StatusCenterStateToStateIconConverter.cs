﻿// Copyright (c) 2023 Files Community
// Licensed under the MIT License. See the LICENSE.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Files.App.Converters
{
	class StatusCenterStateToStateIconConverter : IValueConverter
	{
		public object? Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is StatusCenterItemIconKind state)
			{
				var pathMarkup = state switch
				{
					StatusCenterItemIconKind.Copy =>       Application.Current.Resources["AppThemePathIconActionCopy"] as string,
					StatusCenterItemIconKind.Move =>       Application.Current.Resources["AppThemePathIconActionMove"] as string,
					StatusCenterItemIconKind.Delete =>     Application.Current.Resources["AppThemePathIconActionDelete"] as string,
					StatusCenterItemIconKind.Recycle =>    Application.Current.Resources["AppThemePathIconActionDelete"] as string,
					StatusCenterItemIconKind.Extract =>    Application.Current.Resources["AppThemePathIconActionArchive"] as string,
					StatusCenterItemIconKind.Compress =>   Application.Current.Resources["AppThemePathIconActionArchive"] as string,
					StatusCenterItemIconKind.Successful => Application.Current.Resources["AppThemePathIconActionSuccess"] as string,
					StatusCenterItemIconKind.Error =>      Application.Current.Resources["AppThemePathIconActionInfo"] as string,
					_ => ""
				};

				string xaml = @$"<Path xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Path.Data>{pathMarkup}</Path.Data></Path>";

				if (XamlReader.Load(xaml) is not Path path)
					return null;

				// Initialize a new instance
				Geometry geometry = path.Data;

				// Destroy
				path.Data = null;

				return geometry;
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}