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

public record RCGrowth // Rule! Dont create this if dont have all information.. all fields must get set or coding error!
{
    public decimal Units;
    public decimal McInvested;
    public decimal HcInvested;
    public decimal McClosePrice;
    public decimal HcClosePrice;     // LatestEOD, SalesPrice, etc what Growth is calculated against

    public decimal McAvrgPrice;
    public int McGrowthP = 0;
    public int McGrowthAmount;

    public decimal HcAvrgPrice;
    public int HcGrowthP = 0;
    public int HcGrowthAmount;
    public int HcValuation;

    public RCGrowth(decimal Units, decimal McInvested, decimal HcInvested, decimal McClosePrice, decimal HcClosePrice)
    {
        this.Units = Units;
        this.McInvested = McInvested;
        this.HcInvested = HcInvested;
        this.McClosePrice = McClosePrice;
        this.HcClosePrice = HcClosePrice;
        Recalc();
    }

    public RCGrowth(SHolding holding, decimal mcClosePrice, decimal latestConversionRate)
    {
        Units = holding.Units;
        McInvested = holding.McInvested;
        HcInvested = holding.HcInvested;
        McClosePrice = mcClosePrice;
        HcClosePrice = mcClosePrice * latestConversionRate;
        Recalc();
    }

    protected void Recalc()
    {
        McAvrgPrice = McInvested / Units;

        if (McAvrgPrice > 0.01m)
            McGrowthP = (int)((McClosePrice - McAvrgPrice) / McAvrgPrice * 100);

        McGrowthAmount = (int)(McClosePrice * Units - McInvested);

        HcAvrgPrice = HcInvested / Units;

        if (HcAvrgPrice > 0.01m)
            HcGrowthP = (int)((HcClosePrice - HcAvrgPrice) / HcAvrgPrice * 100);

        HcValuation = (int)(HcClosePrice * Units);

        HcGrowthAmount = (int)(HcClosePrice * Units - HcInvested);
    }
}
