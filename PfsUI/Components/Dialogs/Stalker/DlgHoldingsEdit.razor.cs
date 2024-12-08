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

public partial class DlgHoldingsEdit
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }
    [Parameter] public string PfName { get; set; }
    [Parameter] public SHolding Defaults { get; set; } = null;      // If defined allows to preset some proposed values (used Edit & example Order defaults)
    [Parameter] public bool Edit { get; set; } = false;             // If edit is true then 'Defaults' should contain full StockHolding structure

    protected bool _fullscreen { get; set; } = false;
    protected CurrencyId _marketCurrency;
    protected CurrencyId _homeCurrency;

    protected List<string> _pfNames = new();
    protected string _lockedPfName = null;
    protected decimal _pricePerUnit = 0;
    protected decimal _totalFee = 0;
    protected decimal _units = 0;
    protected string _purhaceId = string.Empty;                     // Locked if edit, as not allowed to be changed
    protected DateTime? _purhaceDate = null;
    protected decimal _currencyRate = 1;
    protected string _purhaceNote = string.Empty;
    protected bool _viewCurrencyRate = true;
    protected string _conversionLabel = string.Empty;
    protected bool _allowDelete = false;                            // Can delete only if has no trades, nor dividents
    protected bool _allowEditUnits = false;                         // Cant edit units if has dividents

    protected override void OnInitialized()
    {
        _marketCurrency = Pfs.Account().GetMarketMeta(Market).Currency;
        _homeCurrency = Pfs.Config().HomeCurrency;
        _purhaceDate = Pfs.Platform().GetCurrentLocalDate().AddDays(-1).ToDateTimeLocal();
        _pfNames = Pfs.Stalker().GetPortfolios().Select(pf => pf.Name).ToList();

        if ( string.IsNullOrWhiteSpace(PfName) == false )
            _lockedPfName = $"Portfolio {PfName}";

        if (Defaults != null)
        {
            if (Edit && Defaults.Units < Defaults.OriginalUnits)
                throw new InvalidProgramException("Err: DlgHoldingsEdit: Should not allow editing this holding, something is sold!");

            _pricePerUnit = Defaults.PricePerUnit;
            _units = Defaults.Units;
            _totalFee = Defaults.FeePerUnit * Defaults.Units;
            _purhaceId = Defaults.PurhaceId;
            _purhaceDate = Defaults.PurhaceDate.ToDateTimeLocal();
            _currencyRate = Defaults.CurrencyRate;
            _purhaceNote = Defaults.PurhaceNote;

            if ( Edit && Defaults.Units == Defaults.OriginalUnits && Defaults.AnyDividents() == false)
                _allowDelete = true;    // Only allowed if nothing is done for holding, or all trades/dividents deleted before

            if (Defaults.AnyDividents())
                _allowEditUnits = true; // If has dividents then no more changing unit amounts
        }

        _conversionLabel = $"From {_marketCurrency} to {_homeCurrency}";

        if (_marketCurrency == _homeCurrency)
        {
            // Actually no conversion rate viewing as its home currency, just hardcode it 1x now
            _viewCurrencyRate = false;
            _currencyRate = 1;
        }
    }

    protected async Task OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;
        await MudDialog.SetOptionsAsync(MudDialog.Options with { FullScreen = fullscreen });
    }

    protected async Task OnBtnGetCurrencyAsync()
    {
        if (_purhaceDate.HasValue == false)
            return;

        decimal? ret = await Pfs.Account().GetHistoryRateAsync(_marketCurrency, DateOnly.FromDateTime(_purhaceDate.Value));

        if (ret.HasValue)
            _currencyRate = decimal.Round(ret.Value, 5);
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }

    private async Task OnBtnDeleteAsync()
    {
        if (_allowDelete == false)
            return;
            
        // Delete-Holding PurhaceId
        string cmd = $"Delete-Holding PurhaceId=[{Defaults.PurhaceId}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await LaunchDialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }

    private async Task OnBtnEditAsync()
    {
        if (string.IsNullOrWhiteSpace(PfName) ||
            _purhaceId != Defaults.PurhaceId ||
            _units < 0.001m ||
            _pricePerUnit < 0.001m ||
            _totalFee < 0 ||
            _currencyRate < 0.001m)
            return;

        // Edit-Holding PurhaceId Date Units Price Fee CurrencyRate Note
        string cmd = $"Edit-Holding PurhaceId=[{_purhaceId}] Date=[{_purhaceDate.Value.ToString("yyyy-MM-dd")}] Units=[{_units}] Price=[{_pricePerUnit}] Fee=[{_totalFee}] CurrencyRate=[{_currencyRate}] Note=[{_purhaceNote}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await LaunchDialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }

    private async Task OnBtnAddAsync()
    {
        if (string.IsNullOrWhiteSpace(PfName) ||
            string.IsNullOrWhiteSpace(_purhaceId) ||
            _units < 0.001m ||
            _pricePerUnit < 0.001m ||
            _totalFee < 0 ||
            _currencyRate < 0.001m)
            return;

        // Add-Holding PfName SRef PurhaceId Date Units Price Fee CurrencyRate Note
        string cmd = $"Add-Holding PfName=[{PfName}] SRef=[{Market}${Symbol}] PurhaceId=[{_purhaceId}] Date=[{_purhaceDate.Value.ToString("yyyy-MM-dd")}] " +
                     $"Units=[{_units}] Price=[{_pricePerUnit}] Fee=[{_totalFee}] CurrencyRate=[{_currencyRate}] Note=[{_purhaceNote}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await LaunchDialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }
}
