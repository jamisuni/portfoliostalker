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

public partial class DlgOrderEdit
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] IDialogService Dialog { get; set; }

    [CascadingParameter] MudDialogInstance MudDialog { get; set; }

    [Parameter] public MarketId Market { get; set; }            // Mandatory
    [Parameter] public string Symbol { get; set; }              // Mandatory
    [Parameter] public string PfName { get; set; } = null;      // If given then locks Pf selection
    [Parameter] public SOrder Defaults { get; set; } = null;    // If set then brings defaults in (used on edit and re-new's etc)
    [Parameter] public bool Edit { get; set; } = false;         // If set true then 'Defaults' must contain actually existing stored Order

    protected bool _fullscreen = false;
    protected List<string> _pfNames = new();
    protected bool _lockedPfName = false;
    protected SOrder _order = null;
    protected DateTime? _minDate = null;
    protected DateTime? _lastDate = DateTime.UtcNow.Date;

    protected override void OnInitialized()
    {
        _minDate = Pfs.Platform().GetCurrentLocalDate().ToDateTime(new TimeOnly());
        _pfNames = Pfs.Stalker().GetPortfolios().Select(pf => pf.Name).ToList();

        if (string.IsNullOrWhiteSpace(PfName) == false && _pfNames.Contains(PfName))
            // If name given and its valid (may have changed by pf rename)
            _lockedPfName = true;
        else
            PfName = string.Empty;

        if ( Defaults != null )
            _order = Defaults.DeepCopy();

        if (Edit) // Expects 'Defaults' to be set if Edit=true
        {
            _lastDate = _order.LastDate.ToDateTime(new TimeOnly());
        }
        else // Add -operation (even if default is given, still uses new period)
        {
            _lastDate = Pfs.Platform().GetCurrentLocalDate().FridayOnTwoWeeksAhead().ToDateTime(new TimeOnly());
        }

        if (_order == null)
            _order = new();

        return;
    }

    protected void OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;

        MudDialog.Options.FullWidth = _fullscreen;
        MudDialog.SetOptions(MudDialog.Options);
    }

    private void DlgCancel()
    {
        MudDialog.Cancel();
    }

    protected void DlgDeleteOrder()
    {   // Note! Called also from 'DlgConvertOrderSync' so dont make async.. just do it...
        if (Edit == false)
            return;

        // Delete-Order PfName SRef Price
        string cmd = $"Delete-Order PfName=[{PfName}] SRef=[{Market}${Symbol}] Price=[{_order.PricePerUnit}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
    }

    protected async Task DlgResetOrderAsync()
    {
        if (Edit == false)
            return;

        // Set-Order PfName SRef Price
        string cmd = $"Set-Order PfName=[{PfName}] SRef=[{Market}${Symbol}] Price=[{_order.PricePerUnit}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }


    private async Task DlgConvertOrderSync()
    {
        SHolding holding = new()
        {
            PricePerUnit = Defaults.PricePerUnit,
            Units = Defaults.Units,
            PurhaceDate = Pfs.Platform().GetCurrentLocalDate().AddDays(-1),
        };

        var parameters = new DialogParameters
        {
            { "Market", Market },
            { "Symbol", Symbol },
            { "PfName", PfName },
            { "Defaults", holding },
            { "Edit", false }
        };

        var dialog = Dialog.Show<DlgHoldingsEdit>("", parameters);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Looks like this was Converted to new Holding, so delete Order itself
            DlgDeleteOrder();
        }
    }
    protected async Task<bool> Verify()
    {
        if (_order.Type == SOrder.OrderType.Unknown || _order.PricePerUnit <= 0.01M || _order.Units <= 0.01M)
        {
            await Dialog.ShowMessageBox("Cant do!", "Please select Type, and fill fields!", yesText: "Ok");
            return false;
        }
        return true;
    }

    private async Task DlgAddOrderAsync()
    {
        if (await Verify() == false)
            return;

        // Add-Order PfName Type SRef Units Price LastDate
        string cmd = $"Add-Order PfName=[{PfName}] Type=[{_order.Type}] SRef=[{Market}${Symbol}] Units=[{_order.Units}] Price=[{_order.PricePerUnit}] LastDate=[{_lastDate.Value.ToString("yyyy-MM-dd")}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }

    private async Task DlgEditOrderAsync()
    {
        if (await Verify() == false)
            return;

        // Edit-Order PfName Type SRef EditedPrice Units Price LastDate
        string cmd = $"Edit-Order PfName=[{PfName}] Type=[{_order.Type}] SRef=[{Market}${Symbol}] EditedPrice=[{Defaults.PricePerUnit}] Units=[{_order.Units}] " + 
                     $"Price=[{_order.PricePerUnit}] LastDate=[{_lastDate.Value.ToString("yyyy-MM-dd")}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await Dialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }
}
