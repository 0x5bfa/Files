// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Files.App.Controls;

/// <summary>
/// Exposes <see cref="SidebarView2"/> to Microsoft UI Automation.
/// </summary>
public sealed partial class SidebarView2AutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
	public bool CanSelectMultiple => false;
	public bool IsSelectionRequired => false;

	private new SidebarView2 Owner { get; }

	public SidebarView2AutomationPeer(SidebarView2 owner) : base(owner)
	{
		Owner = owner;
	}

	protected override object GetPatternCore(PatternInterface patternInterface)
	{
		if (patternInterface == PatternInterface.Selection)
			return this;

		return base.GetPatternCore(patternInterface);
	}

	protected override string GetClassNameCore()
	{
		return nameof(SidebarView2);
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.Pane;
	}

	public IRawElementProviderSimple[] GetSelection()
	{
		if (Owner.SelectedItem is UIElement selectedElement)
		{
			var peer = CreatePeerForElement(selectedElement);
			if (peer is not null)
				return [ProviderFromPeer(peer)];
		}

		return [];
	}
}
