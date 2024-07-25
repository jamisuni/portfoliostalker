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

public class SOrder
{
    public OrderType Type { get; set; }

    public string SRef { get; set; }                        // "MarketId$SYMBOL"
    
    public decimal Units { get; set; }

    public decimal PricePerUnit { get; set; }               // Multiple orders w same PricePerUnit for one specific stock are NOT allowed, not even different types (as pricePerUnit is used as reference ID)

    public DateOnly LastDate { get; set; }

    public DateOnly? FillDate { get; set; }                 // Per days price order should be done on market, used as alarm

    public SOrder DeepCopy()
    {
        SOrder ret = (SOrder)this.MemberwiseClone();        // Works as deep as long no complex tuff
        return ret;
    }

    // negative when getting closer, so -1 is almost triggered... 
    public decimal TriggerDistP(decimal eodClose)
    {
        if (Type == OrderType.Buy)
            return (PricePerUnit - eodClose) / PricePerUnit * 100;
        else
            return (eodClose - PricePerUnit) / PricePerUnit * 100;
    }

    public enum OrderType : int
    {
        Unknown = 0,
        Buy,
        Sell,
    }
}
