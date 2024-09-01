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

using Microsoft.AspNetCore.Components;

using MudBlazor;
using Pfs.Client;
using Pfs.Types;

namespace PfsUI.Components;

public partial class Overview
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    protected OverviewGroups _childGroups;
    protected OverviewStocks _childStocks;

    protected override void OnParametersSet()
    {
        Pfs.Client().EventPfsClient2Page += OnEventPfsClient;
    }

    public void ByOwner_ReloadReport()
    {
        _childStocks.Owner_ReloadReport();
        _childGroups.Owner_ReloadReport();
        StateHasChanged();
    }

    protected void OnEventPfsClient(object sender, IFEClient.FeEventArgs args)
    {
        if (Enum.TryParse(args.Event, out PfsClientEventId clientEvId) == true)
        {   // This event seams to be coming all the way from PFS Client side itself

            switch (clientEvId)
            {
                case PfsClientEventId.FetchEodsFinished:
                    _childStocks.Owner_ReloadReport();
                    _childGroups.Owner_ReloadReport();
                    break;
            }
        }


    }

    protected bool _justOnce = true;

    protected override void OnAfterRender(bool firstRender)  // Not set yet on 'OnInitialized' nor on 'OnParametersSet'
    {
        if (_childGroups != null)
            _childGroups.EvSelChanged += OnStockSelChangedEv;
    }
    protected void OnStockSelChangedEv(object _, OverviewGroups.SelChangedEvArgs args)
    {
        _childStocks.OnUpdateStocks(args);
    }
}
