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
using Pfs.Types;

namespace PfsUI.Components;

// Editing of Report Filter's those are used to control what PF,Period,Segment,etc stocks/events gets to report's. 
public partial class DlgReportFilters
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    [Parameter] public ReportFilters Filters { get; set; } = null;
    [Parameter] public ReportId ReportId { get; set; } = ReportId.Unknown;

    protected bool _fullscreen { get; set; } = false;

    protected string _customName = string.Empty;
    protected string[] _customAll = Array.Empty<string>();

    // PORTFOLIO - if not locked, then can select one/many/all PFs
    protected bool _hidePF = false;
    protected IReadOnlyCollection<SPortfolio> _allPFs;
    private IEnumerable<string> _selPFs { get; set; } = new HashSet<string>();

    // MARKET
    protected IReadOnlyCollection<MarketId> _allMarkets;
    private IEnumerable<MarketId> _selMarkets { get; set; } = new HashSet<MarketId>();

    // OWNING
    private IEnumerable<ReportOwningFilter> _selOwning { get; set; } = new HashSet<ReportOwningFilter>();

    // SECTORS 0,1,2
    protected string[] _sectorNames;
    protected string[] _allSector0;
    protected string[] _allSector1;
    protected string[] _allSector2;
    private IEnumerable<string> _selSector0 { get; set; } = new HashSet<string>();
    private IEnumerable<string> _selSector1 { get; set; } = new HashSet<string>();
    private IEnumerable<string> _selSector2 { get; set; } = new HashSet<string>();

    protected override void OnInitialized()
    {
        _allPFs = Pfs.Stalker().GetPortfolios();
        _allMarkets = Pfs.Account().GetActiveMarketsMeta().Select(m => m.ID).ToList();

        ReloadCustomNames();

        Get(Filters);
    }

    protected void ReloadCustomNames()
    {
        _customAll = Pfs.Report().ListReportFilters();
    }

    protected void OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;

        MudDialog.Options.FullWidth = _fullscreen;
        MudDialog.SetOptions(MudDialog.Options);
    }

    protected void OnBtnLoadCustom(string customName)
    {
        Get(Pfs.Report().GetReportFilters(customName));
        StateHasChanged();
    }

    protected async Task OnBtnSaveCustomAsync()
    {
        ReportFilters filter = Set(ReportFilters.Create(_customName));

        if (filter.IsEmpty())
        {
            await Dialog.ShowMessageBox("Failed!", $"Not going to save empty filter, just use -default-", yesText: "Ok");
            return;
        }

        Pfs.Report().StoreReportFilters(filter);

        ReloadCustomNames();
        StateHasChanged();
    }

    protected void OnBtnDeleteCustom()
    {
        Pfs.Report().StoreReportFilters(ReportFilters.Create(_customName));

        Get(Pfs.Report().GetReportFilters(ReportFilters.DefaultTag));

        ReloadCustomNames();
        StateHasChanged();
    }

    protected void DlgCancel()
    {
        MudDialog.Cancel();
    }

    protected void Get(ReportFilters filter)
    {
        if (filter.Get(FilterId.PfName) != null)
            _selPFs = [.. Filters.Get(FilterId.PfName)];

        if (filter.Get(FilterId.Market) != null)
            _selMarkets = [.. Filters.Get(FilterId.Market).Select(m => (MarketId)Enum.Parse(typeof(MarketId), m))];

        if (filter.Get(FilterId.Owning) != null)
            _selOwning = [.. Filters.Get(FilterId.Owning).Select(o => (ReportOwningFilter)Enum.Parse(typeof(ReportOwningFilter), o))];

        _hidePF = ReportFilters.GetLocked(ReportId).Contains(FilterId.PfName);

        _sectorNames = Pfs.Stalker().GetSectorNames();

        if (string.IsNullOrEmpty(_sectorNames[0]) == false)
        {
            _allSector0 = Pfs.Stalker().GetSectorFieldNames(0);

            if (filter.Get(FilterId.Sector0) != null)
                _selSector0 = [.. Filters.Get(FilterId.Sector0)];
        }

        if (string.IsNullOrEmpty(_sectorNames[1]) == false)
        {
            _allSector1 = Pfs.Stalker().GetSectorFieldNames(1);

            if (filter.Get(FilterId.Sector1) != null)
                _selSector1 = [.. Filters.Get(FilterId.Sector1)];
        }

        if (string.IsNullOrEmpty(_sectorNames[2]) == false)
        {
            _allSector2 = Pfs.Stalker().GetSectorFieldNames(2);

            if (filter.Get(FilterId.Sector2) != null)
                _selSector2 = [.. Filters.Get(FilterId.Sector2)];
        }

        _customName = filter.Name;
    }

    protected ReportFilters Set(ReportFilters filter)
    {
        if (_hidePF == false)
        {
            if (_selPFs.Count() == 0)
                filter.Set(FilterId.PfName);
            else
                filter.Set(FilterId.PfName, _selPFs.ToArray());
        }

        if (_selMarkets.Count() == 0)
            filter.Set(FilterId.Market);
        else
            filter.Set(FilterId.Market, _selMarkets.Select(m => m.ToString()).ToArray());

        if (_selOwning.Count() == 0)
            filter.Set(FilterId.Owning);
        else
            filter.Set(FilterId.Owning, _selOwning.Select(o => o.ToString()).ToArray());

        if (_selSector0.Count() == 0)
            filter.Set(FilterId.Sector0);
        else
            filter.Set(FilterId.Sector0, _selSector0.ToArray());

        if (_selSector1.Count() == 0)
            filter.Set(FilterId.Sector1);
        else
            filter.Set(FilterId.Sector1, _selSector1.ToArray());

        if (_selSector2.Count() == 0)
            filter.Set(FilterId.Sector2);
        else
            filter.Set(FilterId.Sector2, _selSector2.ToArray());

        return filter;
    }

    protected async Task OnBtnUseAsync()
    {
        MudDialog.Close(DialogResult.Ok(Set(Filters)));
    }
}
