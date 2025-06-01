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

namespace Pfs.Types;

public record StockMeta(MarketId marketId, string symbol, string name, CurrencyId marketCurrency, string ISIN)
{   // KeepItSimple: Everything else belongs somewhere else! ISIN is optional information as main identity is SREF

    public StockMeta(MarketId marketId, string symbol, string name, CurrencyId marketCurrency) : this(marketId, symbol, name, marketCurrency, string.Empty)
    {
    }

    public StockMeta DeepCopy()
    {
        return new StockMeta(marketId, new string(symbol), new string(name), marketCurrency, new string(ISIN));
    }

    public string GetSRef()
    {
        return $"{marketId}${symbol}";
    }

    public static (MarketId marketId, string symbol) ParseSRef(string sRef)
    {
        string[] split = sRef.Split('$');
        return (marketId: Enum.Parse<MarketId>(split[0]), split[1]);
    }

    public static (MarketId marketId, string symbol) TryParseSRef(string sRef)
    {
        string[] split = sRef.Split('$');

        if (split.Count() != 2 ||
            Enum.TryParse(split[0], out MarketId marketId) == false ||
            Validate.Str(ValidateId.Symbol, split[1]).Fail )
            return (MarketId.Unknown, string.Empty);

        return (marketId, split[1]);
    }

    public static bool IsClosed(string sRef)
    {
        return sRef.Split('$')[0] == MarketId.CLOSED.ToString();
    }
}

public record StockMetaHist(StockMetaHistType Type, string UpdSRef, string OldSRef, DateOnly Date, string Note);

public enum StockMetaHistType
{
    UserMap,
    AddNew,
    UpdName,
    UpdISIN,
    UpdSRef,
    Close,
    Split
}

#if false
public class Symbol
{
    private readonly string _symbol;

    public Symbol(string symbol)
    {
        if (symbol == null)
            throw new ArgumentNullException(nameof(symbol));

        _symbol = symbol;
    }

    public override string ToString()
    {
        return _symbol;
    }
}

public class SRef
{
    private readonly string _sRef;

    public SRef(string sRef)
    {
        if (sRef == null)
            throw new ArgumentNullException(nameof(sRef));

        _sRef = sRef;
    }

    public SRef(MarketId marketId, string symbol)
    {
        _sRef = $"{marketId}${symbol}";
    }

    public override string ToString()
    {
        return _sRef;
    }

    public (MarketId marketId, Symbol symbol) Parse()
    {
        string[] split = _sRef.Split('$');
        return (marketId: Enum.Parse<MarketId>(split[0]), new Symbol(split[1]));
    }
}
#endif
