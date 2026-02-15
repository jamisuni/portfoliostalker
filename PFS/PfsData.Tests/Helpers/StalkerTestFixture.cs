using Pfs.Data.Stalker;

namespace PfsData.Tests.Helpers;

public static class StalkerTestFixture
{
    public static StalkerDoCmd CreateEmpty()
    {
        var stalker = new StalkerDoCmd();
        stalker.Init();
        return stalker;
    }

    public static StalkerDoCmd CreatePopulated()
    {
        var s = CreateEmpty();

        // 3 portfolios
        s.DoAction("Add-Portfolio PfName=Main");
        s.DoAction("Add-Portfolio PfName=Growth");
        s.DoAction("Add-Portfolio PfName=Dividend");

        // Follow stocks in portfolios
        s.DoAction("Follow-Portfolio PfName=Main SRef=NASDAQ$MSFT");
        s.DoAction("Follow-Portfolio PfName=Main SRef=NYSE$KO");
        s.DoAction("Follow-Portfolio PfName=Main SRef=TSX$ENB");
        s.DoAction("Follow-Portfolio PfName=Growth SRef=NASDAQ$NVDA");
        s.DoAction("Follow-Portfolio PfName=Growth SRef=XETRA$SAP");
        s.DoAction("Follow-Portfolio PfName=Dividend SRef=NYSE$KO");
        s.DoAction("Follow-Portfolio PfName=Dividend SRef=TSX$ENB");

        // Holdings in Main - MSFT bought 2023
        s.DoAction("Add-Holding PfName=Main SRef=NASDAQ$MSFT PurhaceId=M001 Date=2023-03-15 Units=10 Price=280.50 Fee=9.95 CurrencyRate=1.35 Note=Initial");
        s.DoAction("Add-Holding PfName=Main SRef=NASDAQ$MSFT PurhaceId=M002 Date=2023-09-20 Units=5 Price=330.00 Fee=4.95 CurrencyRate=1.36 Note=AddMore");

        // Holdings in Main - KO
        s.DoAction("Add-Holding PfName=Main SRef=NYSE$KO PurhaceId=K001 Date=2022-06-10 Units=50 Price=62.50 Fee=9.95 CurrencyRate=1.32 Note=DivStock");

        // Holdings in Growth - NVDA
        s.DoAction("Add-Holding PfName=Growth SRef=NASDAQ$NVDA PurhaceId=N001 Date=2023-01-10 Units=20 Price=148.50 Fee=9.95 CurrencyRate=1.34 Note=AIPlay");

        // Holdings in Dividend - ENB
        s.DoAction("Add-Holding PfName=Dividend SRef=TSX$ENB PurhaceId=E001 Date=2022-01-15 Units=100 Price=54.20 Fee=0 CurrencyRate=1 Note=Pipeline");
        s.DoAction("Add-Holding PfName=Dividend SRef=TSX$ENB PurhaceId=E002 Date=2023-06-01 Units=50 Price=48.75 Fee=0 CurrencyRate=1 Note=DipBuy");

        // Orders
        s.DoAction("Add-Order PfName=Main Type=Buy SRef=NASDAQ$MSFT Units=5 Price=250.00 LastDate=2026-12-31");
        s.DoAction("Add-Order PfName=Growth Type=Sell SRef=NASDAQ$NVDA Units=10 Price=900.00 LastDate=2026-06-30");

        // Alarms
        s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=BuyMore");
        s.DoAction("Add-Alarm Type=Over SRef=NASDAQ$NVDA Level=800.00 Prms= Note=TakeProfit");

        // Sectors
        s.DoAction("Set-Sector SectorId=0 SectorName=Industry");
        s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech");
        s.DoAction("Edit-Sector SectorId=0 FieldId=1 FieldName=Energy");
        s.DoAction("Edit-Sector SectorId=0 FieldId=2 FieldName=Consumer");
        s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=0 FieldId=0");
        s.DoAction("Follow-Sector SRef=NASDAQ$NVDA SectorId=0 FieldId=0");
        s.DoAction("Follow-Sector SRef=TSX$ENB SectorId=0 FieldId=1");
        s.DoAction("Follow-Sector SRef=NYSE$KO SectorId=0 FieldId=2");

        // Trade: partial sale of 5 NVDA from Growth
        s.DoAction("Add-Trade PfName=Growth SRef=NASDAQ$NVDA Date=2024-06-15 Units=5 Price=450.00 Fee=9.95 TradeId=T001 OptPurhaceId=N001 CurrencyRate=1.36 Note=TakeProfit");

        // Dividends: 3 quarterly KO dividends in Main
        s.DoAction("Add-Divident PfName=Main SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-03-01 PaymentDate=2024-04-01 Units=50 PaymentPerUnit=0.485 CurrencyRate=1.32 Currency=USD");
        s.DoAction("Add-Divident PfName=Main SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-06-01 PaymentDate=2024-07-01 Units=50 PaymentPerUnit=0.485 CurrencyRate=1.33 Currency=USD");
        s.DoAction("Add-Divident PfName=Main SRef=NYSE$KO OptPurhaceId= OptTradeId= ExDivDate=2024-09-01 PaymentDate=2024-10-01 Units=50 PaymentPerUnit=0.485 CurrencyRate=1.34 Currency=USD");

        // Dividend: 1 ENB dividend in Dividend portfolio (covers both holdings: 100+50=150 units)
        s.DoAction("Add-Divident PfName=Dividend SRef=TSX$ENB OptPurhaceId= OptTradeId= ExDivDate=2024-03-15 PaymentDate=2024-04-15 Units=150 PaymentPerUnit=0.915 CurrencyRate=1 Currency=CAD");

        // 2nd sector: Geography with NorthAmerica/Europe, assign MSFT and SAP
        s.DoAction("Set-Sector SectorId=1 SectorName=Geography");
        s.DoAction("Edit-Sector SectorId=1 FieldId=0 FieldName=NorthAmerica");
        s.DoAction("Edit-Sector SectorId=1 FieldId=1 FieldName=Europe");
        s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=1 FieldId=0");
        s.DoAction("Follow-Sector SRef=XETRA$SAP SectorId=1 FieldId=1");

        return s;
    }
}
