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

using Pfs.Types;

namespace Pfs.Data.Stalker;

// Provides generic Validate & Convert a external Brokers 'ExtTransaction' records to Stalker Action's format
public class StalkerAddOn_Transactions
{
    // Validates transaction fields and checks for duplicates. Returns (errMsg, isDuplicate).
    // errMsg is empty if valid. isDuplicate is true if UniqueId already exists in stalker data.
    public static (string ErrMsg, bool IsDuplicate) Validate(Transaction ta, StalkerDoCmd stalkerDoCmd)
    {
        // Field validation
        string validErr = ta.IsValid();
        if (string.IsNullOrEmpty(validErr) == false)
            return (validErr, false);

        // Duplicate check
        switch (ta.Action)
        {
            case TaType.Buy:
                if (string.IsNullOrEmpty(ta.UniqueId) == false && stalkerDoCmd.IsPurhaceId(ta.UniqueId))
                    return (string.Empty, true);
                break;

            case TaType.Sell:
                if (string.IsNullOrEmpty(ta.UniqueId) == false && stalkerDoCmd.IsTradeId(ta.UniqueId))
                    return (string.Empty, true);
                break;

            case TaType.Divident:
                // Divident duplicate check is handled later when saving to company
                break;
        }

        return (string.Empty, false);
    }

    // Assumes everything is Validated, so this creates actual Stalker compatible Action commands, return empty for error
    public static string Convert(Transaction ta, StockMeta stockMeta, string pfName)
    {
        switch (ta.Action)
        {
            case TaType.Buy:

                return $"Add-Holding PfName=[{pfName}] SRef=[{stockMeta.GetSRef()}] PurhaceId=[{ta.UniqueId}]  Date=[{ta.RecordDate.ToYMD()}] " +
                       $"Units=[{ta.Units}] Price=[{ta.McAmountPerUnit}] Fee=[{ta.McFee}] CurrencyRate=[{ta.CurrencyRate}] Note=[{ta.Note}]";

            case TaType.Sell:

                return $"Add-Trade PfName=[{pfName}] SRef=[{stockMeta.GetSRef()}] Date=[{ta.RecordDate.ToString("yyyy-MM-dd")}] Units=[{ta.Units}] Price=[{ta.McAmountPerUnit}] " +
                       $"Fee=[{ta.McFee}] TradeId=[{ta.UniqueId}] OptPurhaceId=[] CurrencyRate=[{ta.CurrencyRate}] Note=[{ta.Note}]";

            case TaType.Divident:

                return $"Add-Divident PfName=[{pfName}] SRef=[{stockMeta.GetSRef()}] OptPurhaceId=[] OptTradeId=[] ExDivDate=[{ta.RecordDate.ToString("yyyy-MM-dd")}] " +
                       $"PaymentDate=[{ta.PaymentDate.ToString("yyyy-MM-dd")}] Units=[{ta.Units}] PaymentPerUnit=[{ta.McAmountPerUnit}] CurrencyRate=[{ta.CurrencyRate}] " +
                       $"Currency=[{ta.Currency}]";

            case TaType.Round:

                return $"Round-Holding PfName=[{pfName}] SRef=[{stockMeta.GetSRef()}] Units=[{ta.Units}]";

            case TaType.Close:

                return $"Close-Stock SRef=[{stockMeta.GetSRef()}] Date=[{ta.RecordDate.ToString("yyyy-MM-dd")}] Note=[{ta.Note}]";
        }
        return string.Empty;
    }
}
