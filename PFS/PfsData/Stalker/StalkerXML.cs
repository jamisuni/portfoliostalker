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

using System.Globalization;
using System.Xml.Linq;

using Pfs.Types;

namespace Pfs.Data.Stalker;

public class StalkerXML
{
    public static string ExportXml(StalkerData stalkerData, List<string> symbols = null)
    {
        XElement rootPFS = new XElement("PFS");

        // PORTFOLIO

        XElement allPfElem = new XElement("Portfolios");
        rootPFS.Add(allPfElem);

        foreach (SPortfolio pf in stalkerData.Portfolios())
        {
            XElement myPfElem = new XElement("Portfolio");
            myPfElem.SetAttributeValue("Name", pf.Name);
            allPfElem.Add(myPfElem);

            List<string> pfAllStocks = 
            [
                .. stalkerData.PortfolioSRefs(pf.Name),             // !!!CODE!!! way to compose list<t> w sublists
                .. pf.StockOrders.Select(o => o.SRef).ToList(),
                .. pf.StockHoldings.Select(h => h.SRef).ToList(),
                .. pf.StockTrades.Select(t => t.SRef).ToList(),
            ];

            foreach (string sRef in pfAllStocks.Distinct())
            {
                var splitSRef = StockMeta.ParseSRef(sRef);

                if (symbols != null && symbols.Contains(splitSRef.symbol) == false)
                    continue;

                // XML! Under Portfolio has list of all stocks it has data / references
                XElement pfSRefElem = new XElement(splitSRef.symbol);
                pfSRefElem.SetAttributeValue("MarketId", splitSRef.marketId);
                if (stalkerData.PortfolioSRefs(pf.Name).Contains(sRef))
                    pfSRefElem.SetAttributeValue("Tracking", true);
                else // so each stock goes xml, but user can keep painfull memories off from list
                    pfSRefElem.SetAttributeValue("Tracking", false);

                myPfElem.Add(pfSRefElem);

                // Stock Orders

                IReadOnlyCollection<SOrder> orders = stalkerData.PortfolioOrders(pf.Name, sRef);

                if (orders.Count > 0)
                {
                    XElement pfStockOrderElem = new XElement("Orders");
                    pfSRefElem.Add(pfStockOrderElem);

                    foreach (SOrder order in orders)
                    {
                        XElement myPfOrElem = new XElement(order.Type.ToString());
                        myPfOrElem.SetAttributeValue("Units", order.Units);
                        myPfOrElem.SetAttributeValue("Price", order.PricePerUnit);
                        myPfOrElem.SetAttributeValue("LDate", order.LastDate.ToYMD());
                        pfStockOrderElem.Add(myPfOrElem);
                    }
                }

                // Stock Holdings (and dividents)

                IReadOnlyCollection<SHolding> holdings = stalkerData.PortfolioHoldings(pf.Name, sRef);

                if (holdings.Count > 0)
                {
                    XElement pfStockHoldingElem = new XElement("Holdings");
                    pfSRefElem.Add(pfStockHoldingElem);

                    foreach (SHolding holding in holdings)
                    {
                        XElement myPfShElem = new XElement("Purhace");
                        myPfShElem.SetAttributeValue("PId", holding.PurhaceId);
                        myPfShElem.SetAttributeValue("Units", holding.Units);
                        myPfShElem.SetAttributeValue("PPrice", holding.PricePerUnit);
                        myPfShElem.SetAttributeValue("PFee", holding.FeePerUnit);
                        myPfShElem.SetAttributeValue("PDate", holding.PurhaceDate.ToYMD());
                        myPfShElem.SetAttributeValue("PNote", holding.PurhaceNote);
                        myPfShElem.SetAttributeValue("OrigUnits", holding.OriginalUnits);

                        if (holding.CurrencyRate != 1)
                            myPfShElem.SetAttributeValue("PRate", holding.CurrencyRate);

                        if (holding.AnyDividents())
                        {
                            foreach (SHolding.Divident divident in holding.Dividents)
                            {
                                XElement myShDivElem = new XElement("Divident");
                                myShDivElem.SetAttributeValue("PayPerUnit", divident.PaymentPerUnit);
                                myShDivElem.SetAttributeValue("ExDivDate", divident.ExDivDate.ToYMD());
                                myShDivElem.SetAttributeValue("PaymentDate", divident.PaymentDate.ToYMD());
                                myShDivElem.SetAttributeValue("Currency", divident.Currency.ToString());

                                if (divident.CurrencyRate != 1)
                                    myShDivElem.SetAttributeValue("Rate", divident.CurrencyRate);

                                myPfShElem.Add(myShDivElem);
                            }
                        }

                        pfStockHoldingElem.Add(myPfShElem);
                    }
                }

                // Stock Trades (and dividents)

                IReadOnlyCollection<SHolding> trades = stalkerData.PortfolioTrades(pf.Name, sRef);

                if (trades.Count > 0)
                {
                    XElement pfStockTradeElem = new XElement("Trades");
                    pfSRefElem.Add(pfStockTradeElem);

                    foreach (SHolding trade in trades)
                    {
                        XElement myPfStElem = new XElement("Sale");
                        myPfStElem.SetAttributeValue("PId", trade.PurhaceId);
                        myPfStElem.SetAttributeValue("Units", trade.Units);
                        myPfStElem.SetAttributeValue("PPrice", trade.PricePerUnit);
                        myPfStElem.SetAttributeValue("PFee", trade.FeePerUnit);
                        myPfStElem.SetAttributeValue("PDate", trade.PurhaceDate.ToYMD());
                        myPfStElem.SetAttributeValue("PNote", trade.PurhaceNote);
                        myPfStElem.SetAttributeValue("OrigUnits", trade.OriginalUnits);
                        // record Sale(string TradeId, DateOnly SaleDate, decimal PricePerUnit, decimal FeePerUnit, decimal CurrencyRate, string TradeNote)
                        myPfStElem.SetAttributeValue("SId", trade.Sold.TradeId);
                        myPfStElem.SetAttributeValue("SPrice", trade.Sold.PricePerUnit);
                        myPfStElem.SetAttributeValue("SFee", trade.Sold.FeePerUnit);
                        myPfStElem.SetAttributeValue("SDate", trade.Sold.SaleDate.ToYMD());
                        myPfStElem.SetAttributeValue("SNote", trade.Sold.TradeNote);

                        if (trade.CurrencyRate != 1)
                            myPfStElem.SetAttributeValue("PRate", trade.CurrencyRate);

                        if (trade.Sold.CurrencyRate != 1)
                            myPfStElem.SetAttributeValue("SRate", trade.CurrencyRate);

                        if (trade.AnyDividents())
                        {
                            foreach (SHolding.Divident divident in trade.Dividents)
                            {
                                XElement myShDivElem = new XElement("Divident");
                                myShDivElem.SetAttributeValue("PayPerUnit", divident.PaymentPerUnit);
                                myShDivElem.SetAttributeValue("ExDivDate", divident.ExDivDate.ToYMD());
                                myShDivElem.SetAttributeValue("PaymentDate", divident.PaymentDate.ToYMD());
                                myShDivElem.SetAttributeValue("Currency", divident.Currency.ToString());

                                if (divident.CurrencyRate != 1)
                                    myShDivElem.SetAttributeValue("Rate", divident.CurrencyRate);

                                myPfStElem.Add(myShDivElem);
                            }
                        }

                        pfStockTradeElem.Add(myPfStElem);
                    }
                }
            }
        }

        // STOCK's

        XElement allStElem = new XElement("Stocks");
        rootPFS.Add(allStElem);

        foreach (SStock stock in stalkerData.Stocks())
        {
            var splitSRef = StockMeta.ParseSRef(stock.SRef);

            if (symbols != null && symbols.Contains(splitSRef.symbol) == false)
                continue;

            XElement myStElem = new XElement(splitSRef.symbol);
            myStElem.SetAttributeValue("MarketId", splitSRef.marketId);
            allStElem.Add(myStElem);

            string[] sectors = stalkerData.GetStockSectors(stock.SRef);

            if (sectors.Any(s => !string.IsNullOrWhiteSpace(s)))
            {
                for ( int s = 0; s < SSector.MaxSectors; s++ )
                {
                    if (string.IsNullOrWhiteSpace(sectors[s]))
                        continue;

                    (string sectorName, string[] fieldNames) sector = stalkerData.GetSector(s);

                    XElement stockSectorElem = new XElement("Sector");
                    stockSectorElem.SetAttributeValue("Sector", sector.sectorName);
                    stockSectorElem.SetAttributeValue("Field", sectors[s]);
                    myStElem.Add(stockSectorElem);
                }
            }

            if (stock.Alarms.Count() > 0)
            {
                foreach (SAlarm alarm in stock.Alarms)
                {
                    XElement myStSaElem = new XElement("Alarm");
                    myStSaElem.SetAttributeValue("Type", alarm.AlarmType);
                    myStSaElem.SetAttributeValue("Level", alarm.Level);
                    myStSaElem.SetAttributeValue("Prms", alarm.Prms);
                    myStSaElem.SetAttributeValue("Note", alarm.Note);
                    myStElem.Add(myStSaElem);
                }
            }
        }

        // SEGMENT's

        XElement allSectorsElem = new XElement("Sectors");
        rootPFS.Add(allSectorsElem);

        for ( int s = 0; s < SSector.MaxSectors; s++ )
        {
            (string sectorName, string[] fieldNames) sector = stalkerData.GetSector(s);

            if (string.IsNullOrWhiteSpace(sector.sectorName))
                continue;

            XElement sectorElem = new XElement($"S{s}");
            sectorElem.SetAttributeValue("Name", sector.sectorName);
            allSectorsElem.Add(sectorElem);

            for ( int f = 0; f < SSector.MaxFields; f++ )
            {
                if (string.IsNullOrWhiteSpace(sector.fieldNames[f]))
                    continue;

                sectorElem.SetAttributeValue($"F{f}", sector.fieldNames[f]);
            }
        }

        return rootPFS.ToString();
    }

    public class Imported
    {
        public List<SPortfolio> Portfolios = new();

        public List<SStock> Stocks = new();

        public SSector[] Sectors = new SSector[SSector.MaxSectors];
    }

    public static Result<Imported> ImportXml(string xml)
    {
        Imported ret = new Imported();

        XDocument xmlDoc = XDocument.Parse(xml);
        XElement rootPFS = xmlDoc.Elements("PFS").First();

        // SEGMENT's

        foreach (XElement sectorElem in rootPFS.Element("Sectors").Elements())
        {
            int sectorPos = int.Parse(sectorElem.Name.ToString().Substring(1));

            ret.Sectors[sectorPos] = new SSector((string)sectorElem.Attribute("Name"));

            for (int f = 0; f < SSector.MaxFields; f++)
            {
                if (sectorElem.Attribute($"F{f}") == null)
                    continue;

                ret.Sectors[sectorPos].FieldNames[f] = (string)sectorElem.Attribute($"F{f}");
            }
        }

        // STOCK's

        foreach (XElement myStElem in rootPFS.Element("Stocks").Elements())
        {
            MarketId marketId = (MarketId)Enum.Parse(typeof(MarketId), (string)myStElem.Attribute("MarketId"));
            string symbol = myStElem.Name.ToString();

            SStock sstock = new SStock($"{marketId}${symbol}");
            ret.Stocks.Add(sstock);

            foreach (XElement underStockElem in myStElem.Elements())
            {
                if (underStockElem.Name.ToString() == "Sector")
                {
                    string sector = (string)underStockElem.Attribute("Sector");
                    string field = (string)underStockElem.Attribute("Field");

                    int sectorPos = Array.IndexOf(ret.Sectors, ret.Sectors.FirstOrDefault(s => s.Name == sector));

                    if (sectorPos == -1)
                        continue;

                    int fieldPos = Array.IndexOf(ret.Sectors[sectorPos].FieldNames, ret.Sectors[sectorPos].FieldNames.FirstOrDefault(s => s == field));

                    if (fieldPos == -1)
                        continue;

                    sstock.Sectors[sectorPos] = fieldPos;
                }
                else if (underStockElem.Name.ToString() == "Alarm")
                {
                    SAlarmType aType = (SAlarmType)Enum.Parse(typeof(SAlarmType), (string)underStockElem.Attribute("Type"));
                    decimal aLevel = (decimal)underStockElem.Attribute("Level");
                    string aPrms = (string)underStockElem.Attribute("Prms");
                    string aNote = (string)underStockElem.Attribute("Note");

                    sstock.Alarms.Add(SAlarm.Create(aType, aLevel, aNote, aPrms));
                }
            }
        }

        // PORTFOLIO's

        foreach (XElement myPfElem in rootPFS.Element("Portfolios").Elements("Portfolio"))
        {
            string pfName = (string)myPfElem.Attribute("Name");
            SPortfolio pf = new SPortfolio() { Name = pfName };
            ret.Portfolios.Add(pf);

            foreach ( XElement pfSRefElem in myPfElem.Elements())
            {
                MarketId marketId = (MarketId)Enum.Parse(typeof(MarketId), (string)pfSRefElem.Attribute("MarketId"));
                string symbol = pfSRefElem.Name.ToString();

                if ((bool)pfSRefElem.Attribute("Tracking"))
                    pf.SRefs.Add($"{marketId}${symbol}");

                XElement ordersElement = pfSRefElem.Element("Orders");

                if (ordersElement != null)
                {
                    foreach (XElement stockOrderElem in ordersElement.Elements())
                    {
                        SOrder order = new SOrder()
                        {
                            Type = (SOrder.OrderType)Enum.Parse(typeof(SOrder.OrderType), (string)stockOrderElem.Name.ToString()),
                            SRef = $"{marketId}${symbol}",
                            Units = (decimal)stockOrderElem.Attribute("Units"),
                            PricePerUnit = (decimal)stockOrderElem.Attribute("Price"),
                            LastDate = DateOnly.ParseExact((string)stockOrderElem.Attribute("LDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                            FillDate = null,
                        };
                        pf.StockOrders.Add(order);
                    }
                }

                XElement holdingsElement = pfSRefElem.Element("Holdings");

                if ( holdingsElement != null )
                {
                    foreach (XElement stockHoldingsElem in holdingsElement.Elements())
                    {
                        SHolding holding = new SHolding()
                        {
                            SRef = $"{marketId}${symbol}",
                            PurhaceId = (string)stockHoldingsElem.Attribute("PId"),
                            Units = (decimal)stockHoldingsElem.Attribute("Units"),
                            PricePerUnit = (decimal)stockHoldingsElem.Attribute("PPrice"),
                            FeePerUnit = (decimal)stockHoldingsElem.Attribute("PFee"),
                            PurhaceDate = DateOnly.ParseExact((string)stockHoldingsElem.Attribute("PDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                            OriginalUnits = (decimal)stockHoldingsElem.Attribute("OrigUnits"),
                            PurhaceNote = (string)stockHoldingsElem.Attribute("PNote"),
                            CurrencyRate = stockHoldingsElem.Attribute("PRate") != null ? (decimal)stockHoldingsElem.Attribute("PRate") : 1,
                            Sold = null,
                            Dividents = new(),
                        };

                        //                                 myShDivElem.SetAttributeValue("Currency", divident.Currency.ToString());


                        foreach (XElement stockHoldingDivElem in stockHoldingsElem.Elements("Divident"))
                        {
                            decimal PaymentPerUnit = (decimal)stockHoldingDivElem.Attribute("PayPerUnit");
                            DateOnly ExDivDate = DateOnly.ParseExact((string)stockHoldingDivElem.Attribute("ExDivDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                            DateOnly PaymentDate = DateOnly.ParseExact((string)stockHoldingDivElem.Attribute("PaymentDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                            decimal CurrencyRate = stockHoldingDivElem.Attribute("Rate") != null ? (decimal)stockHoldingDivElem.Attribute("Rate") : 1;
                            CurrencyId Currency = CurrencyId.Unknown;
                            if (stockHoldingDivElem.Attribute("Currency") != null)
                                Currency = Enum.Parse<CurrencyId>((string)stockHoldingDivElem.Attribute("Currency"));

                            holding.Dividents.Add(new SHolding.Divident(PaymentPerUnit, ExDivDate, PaymentDate, CurrencyRate, Currency));
                        }
                        pf.StockHoldings.Add(holding);
                    }
                }

                XElement tradesElement = pfSRefElem.Element("Trades");

                if (tradesElement != null)
                {
                    foreach (XElement stockTradesElem in tradesElement.Elements())
                    {
                        SHolding trade = new SHolding()
                        {
                            SRef = $"{marketId}${symbol}",
                            PurhaceId = (string)stockTradesElem.Attribute("PId"),
                            Units = (decimal)stockTradesElem.Attribute("Units"),
                            PricePerUnit = (decimal)stockTradesElem.Attribute("PPrice"),
                            FeePerUnit = (decimal)stockTradesElem.Attribute("PFee"),
                            PurhaceDate = DateOnly.ParseExact((string)stockTradesElem.Attribute("PDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                            OriginalUnits = (decimal)stockTradesElem.Attribute("OrigUnits"),
                            PurhaceNote = (string)stockTradesElem.Attribute("PNote"),
                            CurrencyRate = stockTradesElem.Attribute("PRate") != null ? (decimal)stockTradesElem.Attribute("PRate") : 1,
                            Sold = null,
                            Dividents = new(),
                        };

                        string TradeId = (string)stockTradesElem.Attribute("SId");
                        decimal SPricePerUnit = (decimal)stockTradesElem.Attribute("SPrice");
                        decimal SFee = (decimal)stockTradesElem.Attribute("SFee");
                        string SNote = (string)stockTradesElem.Attribute("SNote");
                        decimal SRate = stockTradesElem.Attribute("SRate") != null ? (decimal)stockTradesElem.Attribute("SRate") : 1;
                        DateOnly SDate = DateOnly.ParseExact((string)stockTradesElem.Attribute("SDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);

                        trade.Sold = new SHolding.Sale(TradeId, SDate, SPricePerUnit, SFee, SRate, SNote);

                        foreach (XElement stockTradeDivElem in stockTradesElem.Elements("Divident"))
                        {
                            decimal PaymentPerUnit = (decimal)stockTradeDivElem.Attribute("PayPerUnit");
                            DateOnly ExDivDate = DateOnly.ParseExact((string)stockTradeDivElem.Attribute("ExDivDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                            DateOnly PaymentDate = DateOnly.ParseExact((string)stockTradeDivElem.Attribute("PaymentDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                            decimal CurrencyRate = stockTradeDivElem.Attribute("Rate") != null ? (decimal)stockTradeDivElem.Attribute("Rate") : 1;
                            CurrencyId Currency = CurrencyId.Unknown;
                            if (stockTradeDivElem.Attribute("Currency") != null)
                                Currency = Enum.Parse<CurrencyId>((string)stockTradeDivElem.Attribute("Currency"));

                            trade.Dividents.Add(new SHolding.Divident(PaymentPerUnit, ExDivDate, PaymentDate, CurrencyRate, Currency));
                        }
                        pf.StockTrades.Add(trade);
                    }
                }
            }
        }

        return new OkResult<Imported>(ret);
    }
}
