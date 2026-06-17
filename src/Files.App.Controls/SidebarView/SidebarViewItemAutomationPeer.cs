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
			if (Owner.HasChildren())
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
		return Owner.Text?.ToString() ?? string.Empty;
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

		if (patternInterface == PatternInterface.ExpandCollapse && Owner.HasChildren())
			return this;

		return base.GetPatternCore(patternInterface);
	}

	public void Invoke()
	{
		Owner.InvokeFromAutomationPeer();
	}

	public void Collapse()
	{
		if (Owner.HasChildren())
			Owner.IsExpanded = false;
	}

	public void Expand()
	{
		if (Owner.HasChildren())
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

		var flatItem = Owner.DataContext as FlatSidebarItem;
		if (flatItem is null)
			return -1;

		var index = IndexOf(collection, flatItem);
		return index < 0 ? -1 : index + 1;
	}

	private IReadOnlyList<FlatSidebarItem> GetOwnerCollection()
	{
		if (Owner.Owner is null)
			return [];

		if (Owner.DataContext is FlatSidebarItem flatItem &&
			IndexOf(Owner.Owner.FooterVisibleItems, flatItem) >= 0)
		{
			return Owner.Owner.FooterVisibleItems;
		}

		return Owner.Owner.MenuVisibleItems;
	}

	private static int IndexOf(IReadOnlyList<FlatSidebarItem> collection, FlatSidebarItem item)
	{
		for (var index = 0; index < collection.Count; index++)
		{
			if (ReferenceEquals(collection[index], item))
				return index;
		}

		return -1;
	}
}
