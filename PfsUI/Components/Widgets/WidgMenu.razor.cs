/*
 * Copyright (C) 2?24 Jami Suni
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

using Microsoft.AspNetCore.Components;
using MudBlazor;
using Pfs.Types;

namespace PfsUI.Components;

public partial class WidgMenu
{
    [Inject] PfsUiState PfsUiState { get; set; }
    [Inject] PfsClientAccess PfsClientAccess { get; set; }
    [Inject] NavigationManager NavigationManager { get; set; } // https://blazor-university.com/routing/navigating-our-app-via-code/

    [Parameter] public EventCallback<EvNavLocArgs> EvNavLocChanged { get; set; }    // Sent owner anytime user wants to move new NavLoc on PFS

    protected bool _visible = true;

    public class EvNavLocArgs
    {
        public string Selection;
        public string Parent;
        public MenuEntryType Type;
    }

    protected EvNavLocArgs _currSel = null;
    protected List<WidgMenuElem> _menuPFs = new();      // For prev-next operations
    protected List<WidgMenuElem> _menuSGs = new();

    private HashSet<WidgMenuElem> TreeItems { get; set; } = new HashSet<WidgMenuElem>();

    protected string _homeLabel = "N/A";

    protected override void OnInitialized()
    {
        _homeLabel = $"PFS v{PfsClientAccess.PfsClientVersionNumber}";

        PfsUiState.OnMenuUpdated += OnMenuUpdated;
    }

    protected override void OnParametersSet()
    {
        try
        {
            TreeItems = GetMenuData();
        }
        catch (Exception)
        {
        }
    }

    public void NavigateToNext()
    {
        if (_currSel == null)
            return;

        switch ( _currSel.Type )
        {
            case MenuEntryType.Portfolio:
                {
                    int pos = _menuPFs.FindIndex(s => s.Name == _currSel.Selection);

                    if (pos >= 0 && pos < _menuPFs.Count() - 1)
                        TreeActivationChanged(_menuPFs[pos + 1]);
                }
                break;

        }
    }

    public void NavigateToPrev()
    {
        if (_currSel == null)
            return;

        switch (_currSel.Type)
        {
            case MenuEntryType.Portfolio:
                {
                    int pos = _menuPFs.FindIndex(s => s.Name == _currSel.Selection);

                    if (pos > 0 )
                        TreeActivationChanged(_menuPFs[pos - 1]);
                }
                break;
        }
    }

    protected WidgMenuElem _prevSel = null;

    public void TreeActivationChanged(WidgMenuElem d)
    {
        if (d == null)
            // Gives null if selecting exactly same link twice, but we do wanna allow it
            d = _prevSel;

        if (d == null)
            return;

        _prevSel = d;

        _currSel = new()
        {
            Type = MenuEntryType.Unknown,
        };

        switch (d.Type)
        {
            case MenuEntryType.Home:

                _currSel.Selection = "Home";
                _currSel.Parent = String.Empty;
                _currSel.Type = MenuEntryType.Home;

                NavigationManager.NavigateTo("/");
                break;

            case MenuEntryType.Portfolio:

                _currSel.Selection = d.Name;
                _currSel.Parent = d.Parent;
                _currSel.Type = MenuEntryType.Portfolio;

                NavigationManager.NavigateTo("/Portfolio/" + d.Name);
                break;
        }

        EvNavLocChanged.InvokeAsync(_currSel);
    }

    protected void OnMenuUpdated()
    {
        TreeItems = GetMenuData();
        StateHasChanged();
    }

    public HashSet<WidgMenuElem> GetMenuData()
    {
        _menuSGs = new();
        _menuPFs = new();

        HashSet<WidgMenuElem> ret = new HashSet<WidgMenuElem>();
        List<WidgMenuElem> tempList = new List<WidgMenuElem>();  // temporary list to find parents easier than multilevel HashSet mess...

        List<MenuEntry> inputList = PfsClientAccess.Account().GetMenuData();

        foreach (MenuEntry input in inputList)
        {
            WidgMenuElem output = new WidgMenuElem(input.Name, input.ParentName, input.Type);

            if (string.IsNullOrEmpty(input.ParentName) == true)
            {
                ret.Add(output);
                tempList.Add(output);
            }
            else
            {
                WidgMenuElem parent = tempList.First(x => x.Name == input.ParentName && x.Type == Local_ParentType(output.Type));

                if (parent.TreeItems == null)
                    parent.TreeItems = new HashSet<WidgMenuElem>();

                parent.TreeItems.Add(output);
                parent.IsExpanded = true;
                tempList.Add(output);

                // Lazy coder attack, dont have energy to hass mess.. so just keep additional list that tells next/prev...
                switch (output.Type)
                {
                    case MenuEntryType.Portfolio:  _menuPFs.Add(output); break;
                }
            }
        }

        return ret;


        MenuEntryType Local_ParentType(MenuEntryType child)
        {
            switch (child)
            {
                case MenuEntryType.Portfolio: return MenuEntryType.Home;
            }
            return MenuEntryType.Unknown;
        }
    }

    public class WidgMenuElem
    {
        public string Name { get; set; }

        public string Parent { get; set; }

        public MenuEntryType Type { get; set; }

        public string Icon { get; set; }

        public bool IsExpanded { get; set; }

        public HashSet<WidgMenuElem> TreeItems { get; set; }

        public WidgMenuElem(string name, string parentName, MenuEntryType type)
        {
            Name = name;
            Type = type;
            Parent = parentName;

            switch (type)
            {
                case MenuEntryType.Home:
                    Icon = Icons.Material.Filled.Home;
                    break;

                case MenuEntryType.Portfolio:
                    Icon = Icons.Material.Filled.Label;
                    break;
            }
        }
    }
}