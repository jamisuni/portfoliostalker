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

// Presents ownership of specific Stock under specific Portfolio (if partially sold then split to multiple parts w 'Sold' set)
public class SHolding
{
    public string SRef { get; set; }                // "MarketId$SYMBOL"

    public string PurhaceId { get; set; }           // User given or system generated unique identifier for each holding (cant be edited)

    public decimal Units { get; set; }              // Owned unit amount (after trade this is updated to sale amount)

    public decimal PricePerUnit { get; set; }

    public decimal FeePerUnit { get; set; }

    public DateOnly PurhaceDate { get; set; }

    public decimal OriginalUnits { get; set; }      // provides also easy way to know if some are sold straight from holding (yes thats need)

    public decimal CurrencyRate { get; set; } = 1;

    public string PurhaceNote { get; set; }         // Custom note related to 'purhace feelings'

    public decimal McPriceWithFeePerUnit { get { return PricePerUnit + FeePerUnit; } }
    public decimal HcPriceWithFeePerUnit { get { return (PricePerUnit + FeePerUnit) * CurrencyRate; } }

    public Sale Sold { get; set; } = null;

    // Decision! Holding tracks each divident that is payed for it, there is no more any generic divident structure!
    public List<Divident> Dividents { get; set; }   // Each and every divident payed toward these shares

    public SHolding()
    {
        Dividents = new();
    }

    public decimal McInvested { get { return McPriceWithFeePerUnit * Units; } }

    public decimal HcInvested { get { return McInvested * CurrencyRate; } }

    public record Sale(string TradeId, DateOnly SaleDate, decimal PricePerUnit, decimal FeePerUnit, decimal CurrencyRate, string TradeNote)
    {
        public decimal McPriceWithFeePerUnit { get { return PricePerUnit - FeePerUnit; } }
        public decimal HcPriceWithFeePerUnit { get { return (PricePerUnit - FeePerUnit) * CurrencyRate; } }

        public Sale NewWithUpdatedNote(string updTradeNote)
        {
            return new Sale(TradeId, SaleDate, PricePerUnit, FeePerUnit, CurrencyRate, updTradeNote);
        }
    }

    /* !!!DOCUMENT!!! SHolding.Divident's ""CurrencyId Currency"" field
     * - Added Currency to dividents, as at least many CAD stocks pay USD base dividents (optional field, can leave Unknown)
     * - Allows UI to show mcPerUnit w correct U$, but really means that any total's or % is only shown with homeCurrency
     * - Both broker import and AddDivident should define it when calling Stalker
     */

    public record Divident(decimal PaymentPerUnit, DateOnly ExDivDate, DateOnly PaymentDate, decimal CurrencyRate, CurrencyId Currency)
    {
        public decimal HcPaymentPerUnit { get { return PaymentPerUnit * CurrencyRate; } }
    }

    public SHolding DeepCopy()
    {
        SHolding ret = (SHolding)MemberwiseClone(); // Works as deep as long no complex tuff

        ret.Dividents = new();
        foreach (Divident d in Dividents)
            ret.Dividents.Add(new(d.PaymentPerUnit, d.ExDivDate, d.PaymentDate, d.CurrencyRate, d.Currency));

        if (Sold != null)
            ret.Sold = new Sale(Sold.TradeId, Sold.SaleDate, Sold.PricePerUnit, Sold.FeePerUnit, Sold.CurrencyRate, Sold.TradeNote);

        return ret;
    }
}
