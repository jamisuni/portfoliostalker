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

// Allows to sell previous holding(s) to cash profit/looses. Dlg allows to target selling specific holding, or do automatic fifo sale
public partial class DlgSale
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Inject] private IDialogService LaunchDialog { get; set; }

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; }

    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }
    [Parameter] public string PfName { get; set; }
    [Parameter] public SHolding TargetHolding { get; set; } = null; // If != null then targets to sale specific holding fully/partially
    [Parameter] public DefValues Defaults { get; set; } = null;

    public record DefValues(decimal MaxUnits = 0, decimal Units = 0, decimal PricePerUnit = 0, DateOnly? Date = null);

    protected string _header = string.Empty;
    protected bool _fullscreen = false;
    protected decimal _pricePerUnit = 0;
    protected decimal _totalFee = 0;
    protected decimal _units = 0;
    protected string _labelSoldUnits = "Sold Units";
    protected string _tradeId = string.Empty;                     // Locked if edit, as not allowed to be changed
    protected DateTime? _tradeDate = null;
    protected decimal _currencyRate = 1;
    protected string _tradeNote = string.Empty;
    protected bool _viewCurrencyRate = true;
    protected string _conversionLabel = string.Empty;

    protected CurrencyId _marketCurrency = CurrencyId.Unknown;
    protected CurrencyId _homeCurrency = CurrencyId.Unknown;

    // Decision! 2024-Sep: There is no EDIT! But its possible to delete latest 'Trade' of specific stock under specific Portfolio (so can retry)

    protected override void OnInitialized()
    {
        _marketCurrency = Pfs.Account().GetMarketMeta(Market).Currency;
        _homeCurrency = Pfs.Config().HomeCurrency;
        _tradeDate = Pfs.Platform().GetCurrentLocalDate().AddDays(-1).ToDateTimeLocal();

        if (_marketCurrency == _homeCurrency)
        {
            // Actually no conversion rate viewing as its home currency, just hardcode it 1x now
            _viewCurrencyRate = false;
        }
        else
            _conversionLabel = $"Conversion rate from {_marketCurrency} to {_homeCurrency}";

        if (Defaults != null)
        {
            if (Defaults.MaxUnits > 0)
                _labelSoldUnits = $"Sold Units (max={Defaults.MaxUnits})";

            if (Defaults.Units > 0)
                _units = Defaults.Units;

            if (Defaults.PricePerUnit > 0)
                _pricePerUnit = Defaults.PricePerUnit;

            if (Defaults.Date.HasValue)
                _tradeDate = Defaults.Date.Value.ToDateTimeLocal();
        }

        if (TargetHolding != null)
            _header = $"Sale holding of {Symbol} under {PfName}";
        else
            _header = $"Sale of {Symbol} as FIFO under {PfName}";
    }

    protected async Task OnFullScreenChanged(bool fullscreen)
    {
        _fullscreen = fullscreen;
        await MudDialog.SetOptionsAsync(MudDialog.Options with { FullScreen = fullscreen });
    }

    protected async Task OnBtnGetCurrencyAsync()
    {
        if (_tradeDate.HasValue == false)
            return;

        decimal? ret = await Pfs.Eod().GetHistoryRateAsync(_marketCurrency, DateOnly.FromDateTime(_tradeDate.Value));

        if (ret.HasValue)
            _currencyRate = decimal.Round(ret.Value, 5);
    }

    private void DlgCancel()
    {
        MudDialog.Close(DialogResult.Cancel());
    }

    private async Task OnBtnSaleAsync()
    {
        if (string.IsNullOrWhiteSpace(PfName) ||
            string.IsNullOrWhiteSpace(_tradeId) ||
            _units < 0.001m ||
            _pricePerUnit < 0.001m ||
            _totalFee < 0 ||
            _currencyRate < 0.001m)
            return;

        string OptPurhaceId = TargetHolding == null ? string.Empty : TargetHolding.PurhaceId;

        // Add-Trade PfName SRef Date Units Price Fee TradeId OptPurhaceId CurrencyRate Note
        string cmd = $"Add-Trade PfName=[{PfName}] SRef=[{Market}${Symbol}] Date=[{_tradeDate.Value.ToYMD()}] Units=[{_units}] Price=[{_pricePerUnit}] " +
                     $"Fee=[{_totalFee}] TradeId=[{_tradeId}] OptPurhaceId=[{OptPurhaceId}]  CurrencyRate=[{_currencyRate}] Note=[{_tradeNote}]";

        Result result = Pfs.Stalker().DoAction(cmd);

        if (result.Ok)
            MudDialog.Close();
        else
            await LaunchDialog.ShowMessageBox("Failed!", (result as FailResult).Message, yesText: "Ok");
    }
}
