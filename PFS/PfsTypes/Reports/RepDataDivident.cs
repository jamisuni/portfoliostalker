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

public class RepDataDivident
{
    public decimal HcTotalInvested { get; set; }   // Per remaing holdings original investment as HomeCurrency
    public decimal HcTotalValuation { get; set; }  // Valuation of holdings per latest EOD rates
    public decimal HcEstAnnualDivident { get; set; }    // Estimation of dividents received annually for currency holdings
    public decimal HcTotalInvestedDividentP { get; set; }
    public decimal HcTotalValuationDividentP { get; set; }

    public Dictionary<DateOnly, decimal> HcTotalMonthly { get; set; } = new(); // Note! This is Months, so always enforce day to 1st of month!

    public List<Payment> LastPayments { get; set; } = new();

    public class Payment
    {
        public StockMeta StockMeta { get; set; } = null;

        public DateOnly ExDivDate { get; set; }

        public DateOnly PaymentDate { get; set; }

        public decimal PayPerUnit { get; set; }

        public decimal Units { get; set; }

        public decimal HcPayPerUnit { get; set; }

        public CurrencyId Currency { get; set; }
    }
}
