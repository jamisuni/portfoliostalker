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
using System.Collections.ObjectModel;

namespace PfsUI.Components;

// Per givent parameter targets specific stock of specific portfolio, to add it new divident by manually entering details
public partial class DlgDividentAdd
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public string PfName { get; set; }
    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }
    [Parameter] public SHolding Holding { get; set; } = null;   // If defined then targets specific holding/trade

    // Decision! There is no EDIT! This DLG can be used to SET divident to specific stock over many holdings,
    //           or ADD divident to specific induvidual holding of stock (allowing detailed manual add).

    protected bool _fullscreen { get; set; } = false;
    protected CurrencyId _homeCurrency;
    protected CurrencyId _currency;
    protected List<CurrencyId> _allCurrencies = new();
    protected List<string> _pfsWithHoldings = new();

    protected bool _lockedUnits = false;
    protected DateTime? _exDivDate = DateTime.UtcNow.Date.AddDays(-1); // Going -1 as PFS is using 'Latest' of day, and its not available for today...
    protected DateTime? _paymentDate = DateTime.UtcNow.Date.AddDays(-1); // This is also mandatory IF conversion rate is required

    protected string _currencyLabel = string.Empty;                 // also controls if currencyRate is asked at all
    protected string _extraHeader = "";

    protected decimal _paymentPerUnit = 0;
    protected decimal PaymentPerUnit
    {
        get { return _paymentPerUnit; }
        set { _paymentPerUnit = value; RecalcTotals(); }
    }

    protected decimal _units = 0;
    protected decimal Units
    {
        get { return _units; }
        set { _units = value; RecalcTotals(); }
    }

    protected decimal _currencyRate = 1;
    protected decimal CurrencyRate
    {
        get { return _currencyRate; }
        set { _currencyRate = value; RecalcTotals(); }
    }

    protected bool _currencyRateEdit = true;
    protected bool CurrencyRateEdit
    {
        get { return _currencyRateEdit; }
        set { _currencyRateEdit = value; RecalcTotals(); }
    }

    protected decimal _hcTotal = 0;
    protected decimal _mcTotal = 0;

    protected decimal HcTotal
    {
        get { return _hcTotal; }
        set { _hcTotal = value; RecalcTotals(); }
    }

    protected string PfSelection
    {
        get { return PfName; }
        set { PfName = value; InitWithPf(PfName); StateHasChanged(); }
    }

    protected override void OnInitialized()
    {
        _homeCurrency = Pfs.Config().HomeCurrency;
        _currency = Pfs.Account().GetMarketMeta(Market).Currency;
        _currencyLabel = $"Conversion rate to {_homeCurrency}";

        foreach ( CurrencyId currency in Enum.GetValues(typeof(CurrencyId)))
        {
            if (currency == CurrencyId.Unknown || currency == _homeCurrency)
                continue;

            _allCurrencies.Add(currency);
        }

        if (Holding != null)
        {
            // If targets specific holding or trade then units are locked to that amount
            _lockedUnits = true;
            _units = Holding.Units;

            if (Holding.Sold != null)
                _extraHeader = "To traded holding, sold " + Holding.Sold.SaleDate.ToString("yyyy-MM-dd");
            else
                // Dialog is started to add divident under specific holding
                _extraHeader = "To holding, purhaced " + Holding.PurhaceDate.ToString("yyyy-MM-dd");
        }
        else if (string.IsNullOrEmpty(PfName) )
        {
            foreach (SPortfolio pf in Pfs.Stalker().GetPortfolios())
                if (Pfs.Stalker().GetPortfolioHoldings(pf.Name, $"{Market}${Symbol}").Count > 0)
                    _pfsWithHoldings.Add(pf.Name);
        }
        else
            InitWithPf(PfName);
    }

    protected void InitWithPf(string pfName)
    {
        ReadOnlyCollection<SHolding> holdings = Pfs.Stalker().GetPortfolioHoldings(PfName, $"{Market}${Symbol}");

        _units = holdings.Select(h => h.Units).Sum(); // Default units per pf's current ownings, but dont lock it

        SHolding holding = holdings.MaxBy(h => h.PurhaceDate);

        if (holding.Dividents.Count() > 0)
        {
            _currency = holding.Dividents.Last().Currency;
            PaymentPerUnit = holding.Dividents.Last().PaymentPerUnit;
        }
    }

    protected async Task OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;
        await MudDialog.SetOptionsAsync(MudDialog.Options with { FullScreen = fullscreen });
    }

    protected void RecalcTotals()
    {
        _mcTotal = _paymentPerUnit * _units;

        if (_currency != _homeCurrency)
        {
            if (_currencyRateEdit)
            {   // Normal case: Give units, mvDivPerUnit, currencyRate and get totals
                _hcTotal = (_mcTotal * _currencyRate).Round5();
            }
            else if (_hcTotal > 0)
            {   // Special case: Give units, mvDivPerUnit, hcTotal and get currencyRate
                _currencyRate = (_hcTotal / _mcTotal).Round5();
            }
        }
        StateHasChanged();
    }

    protected void OnCurrencyChanged(CurrencyId currency)
    {
        _currency = currency;
        StateHasChanged();
    }

    protected async Task OnBtnGetCurrencyAsync()
    {
        if (_paymentDate.HasValue == false)
            return;

        decimal? ret = await Pfs.Eod().GetHistoryRateAsync(_currency, DateOnly.FromDateTime(_paymentDate.Value));

        if (ret.HasValue)
            _currencyRate = decimal.Round(ret.Value, 5);

        RecalcTotals();
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }

    private async Task OnBtnAddAsync()
    {
        if (_units <= 0.01m ||
             _paymentPerUnit <= 0 ||
             _currencyRate <= 0.001m)
            return;

        string optPurhaceId = Holding?.PurhaceId;
        string optTradeId = Holding?.Sold?.TradeId;

        // Add-Divident PfName SRef OptPurhaceId OptTradeId ExDivDate PaymentDate Units PaymentPerUnit CurrencyRate Currency
        string cmd = $"Add-Divident PfName=[{PfName}] SRef=[{Market}${Symbol}] OptPurhaceId=[{optPurhaceId}] OptTradeId=[{optTradeId}] ExDivDate=[{_exDivDate.Value.ToString("yyyy-MM-dd")}] " +
                     $"PaymentDate=[{_paymentDate.Value.ToString("yyyy-MM-dd")}] Units=[{_units}] PaymentPerUnit=[{_paymentPerUnit}] CurrencyRate=[{_currencyRate}] Currency=[{_currency}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await LaunchDialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }
}
