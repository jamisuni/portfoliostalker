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

using System.Collections.Immutable;
using System.Text;

namespace Pfs.Data.Stalker;

// Provides template for each Stalker Action combo of its expected parameters, and their allowed ranges/formattings
internal class StalkerActionTemplate
{
    // string per expected parameter, on order, presenting Name of field and its expected content
    public static string[] Get(StalkerOperation Operation, StalkerElement Element)
    {
        ActionTemplate template = Templates.Where(t => t.Operation == Operation && t.Element == Element).SingleOrDefault();

        if (template == null)
            return null;

        return template.Params.Split(' ');
    }

    protected class ActionTemplate
    {
        public StalkerOperation Operation { get; set; }
        public StalkerElement Element { get; set; }
        public string Params { get; set; }
    };

    public static string Help()
    {
        StringBuilder sb = new();

        foreach (ActionTemplate temp in Templates )
            sb.AppendLine($"{temp.Operation}-{temp.Element} {string.Join(' ', temp.Params.Split(' ').Select(s => s.Split('=')[0]).ToList())}");

        return sb.ToString();
    }

    protected readonly static ImmutableArray<ActionTemplate> Templates = ImmutableArray.Create(new ActionTemplate[]
    {
#region STOCK

        new ActionTemplate()                            // Delete-Stock SRef
        {
            Operation = StalkerOperation.Delete,                                    // DELETE to be failing for any dependency.. and this to be called on UI before delete from StockMeta
            Element = StalkerElement.Stock,                                         // ALSO change all Stalker oout from StalkerErr to Result...
            Params = "SRef=SRef",
        },

        new ActionTemplate()                            // Set-Stock UpdSRef OldSRef
        {
            Operation = StalkerOperation.Set,                                       // SET allows to update all references to specific SREF w new SREF
            Element = StalkerElement.Stock,
            Params = "UpdSRef=SRef OldSRef=SRef",
        },

        new ActionTemplate()                            // Split-Stock SRef SplitFactor
        {
            Operation = StalkerOperation.Split,
            Element = StalkerElement.Stock,
            Params = "SRef=SRef SplitFactor=Decimal:0.01",
        },

        new ActionTemplate()                            // Close-Stock SRef Date Note
        {
            Operation = StalkerOperation.Close,                                     // CLOSE used to close stock from Stalker side, changing market and moving all existing holdings off
            Element = StalkerElement.Stock,
            Params = "SRef=SRef Date=Date Note=String:0:100:StalkerNote",
        },

        new ActionTemplate()                            // DeleteAll-Stock SRef
        {
            Operation = StalkerOperation.DeleteAll,
            Element = StalkerElement.Stock,                                         
            Params = "SRef=SRef",
        },

#endregion

#region PORTFOLIO
            
        new ActionTemplate()                            // Add-Portfolio PfName
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Portfolio,
            Params = "PfName=String:1:20:PfName",
        },

        new ActionTemplate()                            // Edit-Portfolio PfCurrName PfNewName
        {
            Operation = StalkerOperation.Edit,
            Element = StalkerElement.Portfolio,
            Params = "PfCurrName=String:1:20:PfName PfNewName=String:1:20:PfName",
        },

        new ActionTemplate()                            // Delete-Portfolio PfName
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Portfolio,
            Params = "PfName=String:1:20:PfName",
        },

        new ActionTemplate()                            // Top-Portfolio PfName
        {
            Operation = StalkerOperation.Top,
            Element = StalkerElement.Portfolio,
            Params = "PfName=String:1:20:PfName",
        },

        new ActionTemplate()                            // Follow-Portfolio PfName SRef
        {
            Operation = StalkerOperation.Follow,
            Element = StalkerElement.Portfolio,
            Params = "PfName=String:1:20:PfName SRef=SRef",
        },

        new ActionTemplate()                            // Unfollow-Portfolio PfName SRef
        {
            Operation = StalkerOperation.Unfollow,
            Element = StalkerElement.Portfolio,
            Params = "PfName=String:1:20:PfName SRef=SRef",
        },

#endregion

#region HOLDING

        new ActionTemplate()                            // Add-Holding PfName SRef PurhaceId Date Units Price Fee CurrencyRate Note
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Holding,
            Params = "PfName=String:1:20:PfName SRef=SRef PurhaceId=PurhaceId Date=Date Units=Decimal:0.01 Price=Decimal:0.01 "+
                     "Fee=Decimal:0.00 CurrencyRate=Decimal:0.001 Note=String:0:100:StalkerNote",
        },

        new ActionTemplate()                            // Edit-Holding PurhaceId Date Units Price Fee CurrencyRate Note
        {
            Operation = StalkerOperation.Edit,
            Element = StalkerElement.Holding,
            Params = "PurhaceId=PurhaceId Date=Date Units=Decimal:0.01 Price=Decimal:0.01 Fee=Decimal:0.00 " +
                     "CurrencyRate=Decimal:0.001 Note=String:0:100:StalkerNote",
        },

        new ActionTemplate()                            // Note-Holding PurhaceId Note
        {
            Operation = StalkerOperation.Note,
            Element = StalkerElement.Holding,
            Params = "PurhaceId=PurhaceId Note=String:0:100:StalkerNote",
        },

        new ActionTemplate()                            // Delete-Holding PurhaceId
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Holding,
            Params = "PurhaceId=PurhaceId",
        },

        new ActionTemplate()                            // Round-Holding PfName SRef Units
        {
            Operation = StalkerOperation.Round,
            Element = StalkerElement.Holding,
            Params = "PfName=String:1:20:PfName SRef=SRef Units=Decimal:0.01",
        },

#endregion

#region TRADE / SALE

        new ActionTemplate()                            // Add-Trade PfName SRef Date Units Price Fee TradeId OptPurhaceId CurrencyRate Note
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Trade,
            Params = "PfName=String:1:20:PfName SRef=SRef Date=Date Units=Decimal:0.01 Price=Decimal:0.01 "+
                     "Fee=Decimal:0.00 TradeId=TradeId OptPurhaceId=String:0:50 CurrencyRate=Decimal:0.001 Note=String:0:100:StalkerNote"
        },

        new ActionTemplate()                            // Note-Trade TradeId Note
        {
            Operation = StalkerOperation.Note,
            Element = StalkerElement.Trade,
            Params = "TradeId=TradeId Note=String:0:100:StalkerNote",
        },

        new ActionTemplate()                            // Delete-Trade TradeId
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Trade,
            Params = "TradeId=TradeId",
        },

#endregion

#region ORDER

        new ActionTemplate()                            // Add-Order PfName Type SRef Units Price LastDate
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Order,
            Params = "PfName=String:1:20:PfName Type=StockOrderType SRef=SRef Units=Decimal:0.01 Price=Decimal:0.01 LastDate=Date",
        },

        new ActionTemplate()                            // Edit-Order PfName Type SRef EditedPrice Units Price LastDate
        {
            Operation = StalkerOperation.Edit,
            Element = StalkerElement.Order,
            Params = "PfName=String:1:20:PfName Type=StockOrderType SRef=SRef EditedPrice=Decimal:0.01 Units=Decimal:0.01 Price=Decimal:0.01 LastDate=Date",
        },

        new ActionTemplate()                            // Delete-Order PfName SRef Price
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Order,
            Params = "PfName=String:1:20:PfName SRef=SRef Price=Decimal:0.01",
        },

        new ActionTemplate()                            // Set-Order PfName SRef Price
        {
            Operation = StalkerOperation.Set,
            Element = StalkerElement.Order,
            Params = "PfName=String:1:20:PfName SRef=SRef Price=Decimal:0.01",
        },

#endregion

#region DIVIDENT

        // Add w OptPurhaceId or OptPurhaceId+OptTradeId targets specific holding/trade... without those its automagical

        new ActionTemplate()                            // Add-Divident PfName SRef OptPurhaceId OptTradeId ExDivDate PaymentDate Units PaymentPerUnit CurrencyRate Currency
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Divident,
            Params = "PfName=String:1:20:PfName SRef=SRef OptPurhaceId=String:0:50 OptTradeId=String:0:50 ExDivDate=Date PaymentDate=Date Units=Decimal:0.01 "+
                     "PaymentPerUnit=Decimal:0.01 CurrencyRate=Decimal:0.001 Currency=CurrencyId",
        },

        new ActionTemplate()                            // DeleteAll-Divident PfName SRef ExDivDate
        {
            Operation = StalkerOperation.DeleteAll,
            Element = StalkerElement.Divident,
            Params = "PfName=String:1:20:PfName SRef=SRef ExDivDate=Date",
        },

        new ActionTemplate()                            // Delete-Divident PfName SRef ExDivDate PurhaceId
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Divident,
            Params = "PfName=String:1:20:PfName SRef=SRef ExDivDate=Date PurhaceId=PurhaceId",
        },
#endregion

#region ALARM

        new ActionTemplate()                            // Add-Alarm Type SRef Level Prms Note
        {
            Operation = StalkerOperation.Add,
            Element = StalkerElement.Alarm,
            Params = "Type=StockAlarmType SRef=SRef Level=Decimal:0.01 Prms=String:0:100 Note=String:0:100",
        },

        new ActionTemplate()                            // Delete-Alarm SRef Level
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Alarm,
            Params = "SRef=SRef Level=Decimal:0.01",
        },

        new ActionTemplate()                            // DeleteAll-Alarm SRef
        {
            Operation = StalkerOperation.DeleteAll,
            Element = StalkerElement.Alarm,
            Params = "SRef=SRef",
        },

        new ActionTemplate()                            // Edit-Alarm Type SRef OldLevel NewLevel Prms Note
        {
            Operation = StalkerOperation.Edit,
            Element = StalkerElement.Alarm,
            Params = "Type=StockAlarmType SRef=SRef OldLevel=Decimal:0.01 NewLevel=Decimal:0.01 Prms=String:0:100 Note=String:0:100",
        },

#endregion

#region SECTOR

        // Operates whole Sector

        new ActionTemplate()                            // Set-Sector SectorId SectorName
        {
            Operation = StalkerOperation.Set,
            Element = StalkerElement.Sector,
            Params = "SectorId=SectorId SectorName=SectorName",                  // SSector.MaxNameLen
        },

        new ActionTemplate()                            // DeleteAll-Sector SectorId
        {
            Operation = StalkerOperation.DeleteAll,
            Element = StalkerElement.Sector,
            Params = "SectorId=SectorId",
        },

        // Following ones focus Sector's FIELDS

        new ActionTemplate()                            // Edit-Sector SectorId FieldId FieldName
        {
            Operation = StalkerOperation.Edit,
            Element = StalkerElement.Sector,
            Params = "SectorId=SectorId FieldId=FieldId FieldName=FieldName",
        },

        new ActionTemplate()                            // Delete-Sector SectorId FieldId
        {
            Operation = StalkerOperation.Delete,
            Element = StalkerElement.Sector,
            Params = "SectorId=SectorId FieldId=FieldId",
        },

        // And finally assigning Stock's field to specific sector

        new ActionTemplate()                            // Follow-Sector SRef SectorId FieldId
        {
            Operation = StalkerOperation.Follow,
            Element = StalkerElement.Sector,
            Params = "SRef=SRef SectorId=SectorId FieldId=FieldId",
        },

        new ActionTemplate()                            // Unfollow-Sector SRef SectorId
        {
            Operation = StalkerOperation.Unfollow,
            Element = StalkerElement.Sector,
            Params = "SRef=SRef SectorId=SectorId",
        },

#endregion
    });
}
