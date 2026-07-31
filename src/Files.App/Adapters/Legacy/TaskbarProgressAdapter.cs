// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Files.App.Adapters.Legacy
{
	/// <summary>
	/// Isolates the existing status center from the Win32 taskbar progress API.
	/// </summary>
	internal sealed class TaskbarProgressAdapter : IDisposable
	{
		private ITaskbarList3? taskbarList;

		public static TaskbarProgressAdapter Default { get; } = new();

		private unsafe TaskbarProgressAdapter()
		{
			var classId = typeof(TaskbarList).GUID;
			var result = PInvoke.CoCreateInstance(
				classId,
				null,
				CLSCTX.CLSCTX_INPROC_SERVER,
				out ITaskbarList3? created);
			if (result.Succeeded && created is not null && created.HrInit().Succeeded)
				taskbarList = created;
		}

		public HRESULT SetProgressValue(HWND window, ulong completed, ulong total)
		{
			return taskbarList?.SetProgressValue(window, completed, total)
				?? HRESULT.E_FAIL;
		}

		public HRESULT SetProgressState(HWND window, TBPFLAG state)
		{
			return taskbarList?.SetProgressState(window, state)
				?? HRESULT.E_FAIL;
		}

		public void Dispose()
		{
			taskbarList = null;
		}
	}
}
