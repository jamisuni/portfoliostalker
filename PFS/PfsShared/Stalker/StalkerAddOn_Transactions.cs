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

namespace Pfs.Shared.Stalker;

// Provides generic Validate & Convert a external Brokers 'ExtTransaction' records to Stalker Action's format
public class StalkerAddOn_Transactions
{
    // Does check & complete transactions information to make sure everything required is available
    public static bool Validate(Transaction ta, StalkerDoCmd stalkerDoCmd)      // !!!TODO!!!
    {
#if false
        ta.Status = Transaction.ProcessingStatus.Acceptable;

        switch ( ta.Type )
        {
            case TaType.Buy:
            case TaType.Sell:
            case TaType.Divident:
            {
                    if (ta.Units <= 0)
                        ta.Status = Transaction.ProcessingStatus.Err_UnitAmount;
                    
                    else if (ta.AmountPerUnit <= 0)
                        ta.Status = Transaction.ProcessingStatus.Err_PricePerUnit;

                    else if (ta.Fee < 0)
                        ta.Status = Transaction.ProcessingStatus.Err_Fee;
                }
                break;

            default:
                ta.Status = Transaction.ProcessingStatus.Err_UnknownType;
                break;
        }

        if (ta.Status != Transaction.ProcessingStatus.Acceptable)
            return false;

        switch (ta.Type)
        {
            case TaType.Buy:
                {
                    if (stalkerDoCmd.IsPurhaceId(ta.UniqueId) )
                        ta.Status = Transaction.ProcessingStatus.Duplicate;
                }
                break;

            case TaType.Sell:
                {
                    if (stalkerDoCmd.IsTradeId(ta.UniqueId) )
                        ta.Status = Transaction.ProcessingStatus.Duplicate;
                }
                break;

            case TaType.Divident:
                // Divident is not that easy, as not uniqueId base and we do not know company for sure yet
                // so that part needs to be handled later when actually trying to save it to company!
                break;
        }

        if (ta.Status != BtAction.Acceptable)
            return false;
#endif
        return true;
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
