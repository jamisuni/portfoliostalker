/*
 * Copyright (C) 2024 Jami Suni
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

using Pfs.Types;
using PfsUI.Components;
using static PfsUI.Components.PageHeader;

namespace PfsUI.Layout;

public partial class MainLayout
{
    public PageHeader _childPageHeader;

    #region Custom Menu on PageHeader

    public delegate Task CallbackPageHeaderArgsAsync(PageHeader.EvArgs args);   // As MainLayout owns PageHeader, this PageHeader event is passed
    private CallbackPageHeaderArgsAsync evFromPageHeaderAsync;                      // back to actual page
    public event CallbackPageHeaderArgsAsync EvFromPageHeaderAsync
    {
        add
        {
            evFromPageHeaderAsync = null;
            evFromPageHeaderAsync += value;
        }
        remove
        {
            evFromPageHeaderAsync -= value;
        }
    }

    // Allows page to set specific menu items it wants to add to main menu
    public void SetCustomMenuItems(List<PageHeader.MenuItem> menuItems)
    {
        _childPageHeader.SetCustomMenuItems(menuItems);
    }

    protected void OnEvFromPageHeaderAsync(PageHeader.EvArgs args) // Registered on Razon side!
    {   // FROM: PageHeader =>TO=> What ever page is active
        switch (args.ID)
        {
            case EvId.MenuSel:
            case EvId.ReportRefresh: // == ReportParams changed
            case EvId.SpeedButton:
                evFromPageHeaderAsync?.Invoke(args);
                break;
        }
    }

    #endregion

    #region Speed Button on PageHeader

    public void SetSpeedOperationLabel(string label = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            _childPageHeader.SetLabelSpeedOperation(string.Empty);
        else
            _childPageHeader.SetLabelSpeedOperation(label);
    }

    public void SetNotReport()
    {
        _childPageHeader.SetReport(ReportId.Unknown);
    }

    public void SetAsReport(ReportId reportId)
    {
        _childPageHeader.SetReport(reportId);
    }

    public void PageHeaderDoStateHasChanged()
    {
        _childPageHeader.DoStateHasChanged();
    }

    #endregion

    // All navigations are done thru PageHeader, I mean all of them! So that ''_popupNavMenuLoc'' gets updated correctly
    public void NavigateToHome()
    {
        _childPageHeader.NavigateToHome();
    }
}
