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

public class RCDivident // Each PaymentDate is own 'RCDivident' (Note! Only RCStock use cases, general one is RRHoldingDivident)
{
    //  These are main fields those are expected to be identical for same divident

    public DateOnly PaymentDate;

    public decimal PaymentPerUnit;

    public DateOnly ExDivDate;

    public decimal CurrencyRate;

    public CurrencyId Currency;

    // Actual units are collected separate totals for Holdings / Trades

    public decimal HoldingUnits = 0;

    public decimal TradesUnits = 0;         // !!!TODO!!! 'RCDivident' seams too targeted to one use case, RECHECK -> rename? move as sub of RCStock?

    public decimal HcPaymentPerUnit {  get {  return PaymentPerUnit * CurrencyRate; } }
    public decimal HcTotalHoldingDiv { get { return HcPaymentPerUnit * HoldingUnits; } }
    public decimal HcTotalTradeDiv { get { return HcPaymentPerUnit * TradesUnits; } }
    public decimal HcTotalDiv { get { return HcTotalHoldingDiv + HcTotalTradeDiv; } }

    protected RCDivident() { }

    public RCDivident(SHolding.Divident divident)   
    {
        PaymentDate = divident.PaymentDate;
        PaymentPerUnit = divident.PaymentPerUnit;
        ExDivDate = divident.ExDivDate;
        CurrencyRate = divident.CurrencyRate;
        Currency = divident.Currency;               // !!!TODO!!! plus missing here 'HoldingUnits' so cant do general use
    }
}

public record RCTotalHcDivident(decimal HcDiv, decimal HcInvested); // !!!TODO!!! move also under RCStock and rename RCStockTotalHcDiv
