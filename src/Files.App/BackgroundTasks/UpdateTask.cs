// Copyright (c) Files Community
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.StartScreen;

namespace Files.App.BackgroundTasks;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("B3A8E4D4-3B9C-4D49-9D4A-3DA7F1D8B9F3")]
[ComSourceInterfaces(typeof(IBackgroundTask))]
public sealed class UpdateTask : IBackgroundTask
{
	[MTAThread]
	public async void Run(IBackgroundTaskInstance taskInstance)
	{
		var deferral = taskInstance.GetDeferral();

		try
		{
			try { await RefreshJumpListAsync(); } catch { }
			try { DeleteLogFiles(); } catch { }
		}
		finally
		{
			deferral.Complete();
		}
	}

	private static void DeleteLogFiles()
	{
		File.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, "debug.log"));
		File.Delete(Path.Combine(ApplicationData.Current.LocalFolder.Path, "debug_fulltrust.log"));
	}

	private static async Task RefreshJumpListAsync()
	{
		if (JumpList.IsSupported())
		{
			var instance = await JumpList.LoadCurrentAsync();
			instance.SystemGroupKind = JumpListSystemGroupKind.None;

			var jumpListItems = instance.Items.ToList();
			instance.Items.Clear();

			foreach (var temp in jumpListItems)
			{
				var jumplistItem = Windows.UI.StartScreen.JumpListItem.CreateWithArguments(temp.Arguments, temp.DisplayName);
				jumplistItem.Description = jumplistItem.Arguments;
				jumplistItem.GroupName = "ms-resource:///Resources/JumpListRecentGroupHeader";
				jumplistItem.Logo = new Uri("ms-appx:///Assets/FolderIcon.png");
				instance.Items.Add(jumplistItem);
			}

			await instance.SaveAsync();
		}
	}
}
