// Copyright (c) Files Community
// Licensed under the MIT License.

using App1;
using Microsoft.UI.Xaml;

namespace Files.App.UnitTests
{
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			ExtendsContentIntoTitleBar = true;

			var testClass = new UnitTest1();
			testClass.TestMethod1();
		}
	}
}
