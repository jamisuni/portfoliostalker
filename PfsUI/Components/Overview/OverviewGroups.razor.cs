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
using System.Collections.Immutable;

using MudBlazor;

using Pfs.Types;

namespace PfsUI.Components;

public partial class OverviewGroups
{
    [Inject] IDialogService LaunchDialog { get; set; }
    [Inject] PfsClientAccess Pfs { get; set; }

    private EventHandler<SelChangedEvArgs> evSelChanged;
    public event EventHandler<SelChangedEvArgs> EvSelChanged
    {
        add
        {
            if (evSelChanged == null || !evSelChanged.GetInvocationList().Contains(value))
                evSelChanged += value;
        }
        remove
        {
            evSelChanged -= value;
        }
    }

    protected MudCarousel<CarouselPages> _carousel;
    protected IList<CarouselPages> _groups = new List<CarouselPages>();
    protected string _HC;
    protected int _index = 0;

    protected class CarouselPages
    {
        public OverviewGroupsData d;
    }

    public class SelChangedEvArgs
    {
        public string OrdersFromPf;
        public List<string> SRefs;
    }

    protected override void OnParametersSet()
    {
        _HC = UiF.Curr(Pfs.Config().HomeCurrency);

        ReloadReport();
    }

    public void Owner_ReloadReport()
    {
        ReloadReport();
        OnSpinnerChanged(_index);
        StateHasChanged();
    }

    protected void ReloadReport()
    {
        Result<List<OverviewGroupsData>> groupData = Pfs.Report().GetOverviewGroups();

        if (groupData.Ok == false)
        {   // ?? Never happens ??
            return;
        }

        foreach (OverviewGroupsData gd in groupData.Data)
        {
            CarouselPages entry = new()
            {
                d = gd,
            };

            _groups.Add(entry);
        }
    }

    protected void OnSpinnerChanged(int index)
    {
        _index = index;

        evSelChanged?.Invoke(this, new SelChangedEvArgs()
        {
            SRefs = _groups[index].d.SRefs,
            OrdersFromPf = _groups[index].d.LimitSinglePf,
        });
    }
}
