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

using Pfs.Types;

namespace PfsUI.Components;

public partial class StockMgmtNote
{
    [Inject] PfsClientAccess Pfs { get; set; }
    [Parameter] public MarketId Market { get; set; }
    [Parameter] public string Symbol { get; set; }

    protected WidgStockSectors _widgStockSectors;

    protected bool _editingMode = false;
    protected string _editingText = new(string.Empty);

    protected string _urlGoogle;
    protected string _urlTradingView;
    protected string _urlYahoo;
    protected string _urlStockTwits;

    protected override void OnParametersSet()
    {
        Note current = Pfs.Account().GetNote($"{Market}${Symbol}");

        if (current != null)
            _editingText = new(current.Get());

        _urlGoogle = GetUrlGoogleFinances();
        _urlTradingView = GetUrlTradingView();
        _urlYahoo = GetUrlYahooFinances();
        _urlStockTwits = GetUrlStockTwits();
    }

    public void OnButtonPress() // Edit or Save depending state... this is called by owner, as it controls buttons
    {
        if (_editingMode) // user has been pressing 'Save' to finish up editing mode
        {
            _editingMode = false;
            Pfs.Account().StoreNote($"{Market}${Symbol}", new Note(_editingText));
        }
        else // On Viewing mode pressing 'Edit' to allow writing/changes
        {
            _editingMode = true;
        }
        StateHasChanged();
    }

    protected string GetUrlGoogleFinances()
    {
        string url = @"https://www.google.com/finance/quote/";

        switch (Market)
        {
            case MarketId.TSX:
                return url + Symbol + ":TSE";

            case MarketId.NYSE:
            case MarketId.NASDAQ:
                return url + Symbol + ":" + Market.ToString();

            case MarketId.AMEX:
                return url + Symbol + ":NYSEAMERICAN";

            case MarketId.OMXH:
                return url + Symbol + ":HEL";
        }
        return string.Empty;
    }

    protected string GetUrlTradingView()
    {
        //TSX:  https://www.tradingview.com/chart/?symbol=TSX%3ATXG

        string url = @"https://www.tradingview.com/chart/?symbol=";

        switch (Market)
        {
            case MarketId.TSX:
            case MarketId.NYSE:
            case MarketId.NASDAQ:
            case MarketId.AMEX:
                return url + Market.ToString() + "%3A" + Symbol;

            case MarketId.OMXH:
                return url + "OMXHEX%3A" + Symbol;
        }
        return string.Empty;
    }

    protected string GetUrlYahooFinances()
    {
        // https://finance.yahoo.com/quote/ABX.TO
        string url = @"https://finance.yahoo.com/quote/";

        switch (Market)
        {
            case MarketId.NYSE:
            case MarketId.NASDAQ:
            case MarketId.AMEX:
                return url + Symbol;

            case MarketId.TSX:
                return url + Symbol + ".TO";

            case MarketId.OMXH:
                return url + Symbol + ".HE";
        }
        return string.Empty;
    }

    protected string GetUrlStockTwits()
    {
        // https://stocktwits.com/symbol/TRIT
        string url = @"https://stocktwits.com/symbol/";

        switch (Market)
        {
            case MarketId.NYSE:
            case MarketId.NASDAQ:
            case MarketId.AMEX:
                return url + Symbol;

            case MarketId.TSX:
                return url + Symbol + ".CA";
        }
        return string.Empty;
    }
}
