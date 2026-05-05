// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using System.Collections;

namespace Files.App.Controls;

/// <summary>
/// Exposes <see cref="SidebarViewItem"/> to Microsoft UI Automation.
/// </summary>
public sealed partial class SidebarViewItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, IExpandCollapseProvider, ISelectionItemProvider
{
	private new SidebarViewItem Owner { get; }

	public SidebarViewItemAutomationPeer(SidebarViewItem owner) : base(owner)
	{
		Owner = owner;
	}

	public ExpandCollapseState ExpandCollapseState
	{
		get
		{
			if (Owner.Children is IList { Count: > 0 })
				return Owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

			return ExpandCollapseState.LeafNode;
		}
	}

	public bool IsSelected => Owner.IsSelected;

	public IRawElementProviderSimple? SelectionContainer
	{
		get
		{
			if (Owner.Owner is null)
				return null;

			var peer = CreatePeerForElement(Owner.Owner);
			return peer is null ? null : ProviderFromPeer(peer);
		}
	}

	protected override string GetNameCore()
	{
		return Owner.Text ?? string.Empty;
	}

	protected override string GetClassNameCore()
	{
		return nameof(SidebarViewItem);
	}

	protected override AutomationControlType GetAutomationControlTypeCore()
	{
		return AutomationControlType.ListItem;
	}

	protected override object GetPatternCore(PatternInterface patternInterface)
	{
		if (patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem)
			return this;

		if (patternInterface == PatternInterface.ExpandCollapse && Owner.Children is IList { Count: > 0 })
			return this;

		return base.GetPatternCore(patternInterface);
	}

	public void Invoke()
	{
		Owner.InvokeFromAutomationPeer();
	}

	public void Collapse()
	{
		if (Owner.Children is IList { Count: > 0 })
			Owner.IsExpanded = false;
	}

	public void Expand()
	{
		if (Owner.Children is IList { Count: > 0 })
			Owner.IsExpanded = true;
	}

	public void AddToSelection()
	{
		Select();
	}

	public void RemoveFromSelection()
	{
		// Single-selection model; keep current behavior with no-op removal.
	}

	public void Select()
	{
		Owner.Owner?.RaiseItemInvoked(Owner);
	}

	protected override int GetSizeOfSetCore()
	{
		var baseValue = base.GetSizeOfSetCore();
		if (baseValue != -1)
			return baseValue;

		return GetOwnerCollection().Count;
	}

	protected override int GetPositionInSetCore()
	{
		var baseValue = base.GetPositionInSetCore();
		if (baseValue != -1)
			return baseValue;

		var collection = GetOwnerCollection();
		if (collection.Count == 0)
			return -1;

		var itemValue = Owner.DataContext ?? Owner;
		return collection.IndexOf(itemValue) + 1;
	}

	private IList GetOwnerCollection()
	{
		if (Owner.FindAscendant<SidebarViewItem>() is SidebarViewItem parent && parent.Children is IList children)
			return children;

		if (Owner.Owner?.MenuItemsSource is IList rootItems)
			return rootItems;

		return new List<object>();
	}
}
