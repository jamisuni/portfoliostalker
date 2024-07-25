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

// RR ala ReportRecords expand from RC on place where just need things more FE ready
// or like here have different type subcases to be presented with one RCellDivident handler

public abstract class RRDivident
{
    public decimal ViewHcDivP { get { return decimal.Round(HcDiv / HcInvested * 100, 1); } }

    public decimal ViewMcDiv { get { return McDiv.ToVR(); } }

    public decimal ViewHcDiv { get { return HcDiv.ToVR(); } }

    public virtual decimal McDiv { get; }
    public virtual decimal HcDiv { get; }
    public virtual decimal McInvested { get; }
    public virtual decimal HcInvested { get; }
}

public class RRHoldingDivident : RRDivident
{
    // Important! Do not create this if doesnt have all required information

    public decimal Units { get; internal set; }
    public DateOnly ExDivDate { get; internal set; }
    public DateOnly PaymentDate { get; internal set; }
    public decimal PaymentPerUnit { get; internal set; }
    public decimal HcPaymentPerUnit { get; internal set; }

    public override decimal McDiv { get { return Units * PaymentPerUnit; } }
    public override decimal HcDiv { get { return Units * HcPaymentPerUnit; } }
    public override decimal McInvested { get { return McHoldingInvested; } }
    public override decimal HcInvested { get { return HcHoldingInvested; } }

    internal decimal McHoldingInvested;
    internal decimal HcHoldingInvested;
    
    public RRHoldingDivident(SHolding holding, SHolding.Divident div)
    {
        Units = holding.Units;
        ExDivDate = div.ExDivDate;
        PaymentDate = div.PaymentDate;
        PaymentPerUnit = div.PaymentPerUnit;
        HcPaymentPerUnit = div.PaymentPerUnit * div.CurrencyRate;
        McHoldingInvested = holding.McInvested;
        HcHoldingInvested = holding.HcInvested;
    }

    public static List<RRHoldingDivident> Create(SHolding holding)
    {
        List<RRHoldingDivident> ret = new();

        foreach (SHolding.Divident div in holding.Dividents.Reverse<SHolding.Divident>())
            ret.Add(new RRHoldingDivident(holding, div));

        return ret;
    }
}

public class RRTotalDivident : RRDivident
{
    public override decimal McDiv { get { return -1; } }
    public override decimal HcDiv { get { return _hcDiv; } }
    public override decimal McInvested { get { return -1; } }
    public override decimal HcInvested { get { return _hcInvested; } }

    protected decimal _hcDiv;
    protected decimal _hcInvested;

    public RRTotalDivident(List<RRDivident> divs)
    {
        if (divs == null || divs.Count == 0)
            throw new InvalidProgramException("RRTotalDivident: Shouldnt initialized wo content1");

        _hcDiv = 0;
        _hcInvested = 0;

        foreach (RRDivident div in divs)
        {
            _hcDiv += div.HcDiv;
            _hcInvested += div.HcInvested;
        }
    }

    public RRTotalDivident(SHolding holding)
    {
        if (holding.Dividents == null || holding.Dividents.Count == 0)
            throw new InvalidProgramException("RRTotalDivident: Shouldnt initialized wo content2");

        _hcDiv = 0;

        foreach (SHolding.Divident div in holding.Dividents)
        {
            _hcDiv += div.HcPaymentPerUnit * holding.Units;
        }
        _hcInvested = holding.HcInvested;
    }

    public RRTotalDivident(decimal hcDiv, decimal hcInvested)
    {
        _hcDiv = hcDiv;
        _hcInvested = hcInvested;
    }

    public RRTotalDivident(RCTotalHcDivident div)
    {
        _hcDiv = div.HcDiv;
        _hcInvested = div.HcInvested;
    }
}
