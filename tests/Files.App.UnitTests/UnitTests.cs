// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Storage;
using Files.Shared.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace App1
{
	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void TestMethod1()
		{
			var windowsFile = WindowsStorable.TryParse("Shell:AppsFolder");

			Assert.IsNotNull(windowsFile);

			ChecksumHelpers.CalculateChecksumForPath("C:\\");
		}
	}
}
