using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class AlarmTests
{
    [Fact]
    public void Add_UnderAlarm_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=BuyMore"));
        var alarms = s.StockAlarms("NASDAQ$MSFT");
        Assert.Single(alarms);
        Assert.Equal(SAlarmType.Under, alarms[0].AlarmType);
        Assert.Equal(250.00m, alarms[0].Level);
    }

    [Fact]
    public void Add_OverAlarm_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Over SRef=NYSE$KO Level=75.00 Prms= Note=Sell"));
        Assert.Equal(SAlarmType.Over, s.StockAlarms("NYSE$KO")[0].AlarmType);
    }

    [Fact]
    public void Add_DuplicateLevel_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=First"));
        StalkerAssert.Fail(s.DoAction("Add-Alarm Type=Over SRef=NASDAQ$MSFT Level=250.00 Prms= Note=Second"), "duplicate");
    }

    [Fact]
    public void Delete_Alarm_Success()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=Test"));
        StalkerAssert.Ok(s.DoAction("Delete-Alarm SRef=NASDAQ$MSFT Level=250.00"));
        Assert.Empty(s.StockAlarms("NASDAQ$MSFT"));
    }

    [Fact]
    public void DeleteAll_Alarm_RemovesAll()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=A"));
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Over SRef=NASDAQ$MSFT Level=400.00 Prms= Note=B"));
        Assert.Equal(2, s.StockAlarms("NASDAQ$MSFT").Count);
        StalkerAssert.Ok(s.DoAction("DeleteAll-Alarm SRef=NASDAQ$MSFT"));
        Assert.Empty(s.StockAlarms("NASDAQ$MSFT"));
    }

    [Fact]
    public void Edit_Alarm_ChangesLevelAndType()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=Original"));
        StalkerAssert.Ok(s.DoAction("Edit-Alarm Type=Over SRef=NASDAQ$MSFT OldLevel=250.00 NewLevel=300.00 Prms= Note=Edited"));
        var alarms = s.StockAlarms("NASDAQ$MSFT");
        Assert.Single(alarms);
        Assert.Equal(300.00m, alarms[0].Level);
        Assert.Equal(SAlarmType.Over, alarms[0].AlarmType);
    }

    [Fact]
    public void Edit_NonExistent_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Add-Alarm Type=Under SRef=NASDAQ$MSFT Level=250.00 Prms= Note=Test"));
        StalkerAssert.Fail(s.DoAction("Edit-Alarm Type=Over SRef=NASDAQ$MSFT OldLevel=999.00 NewLevel=300.00 Prms= Note="), "find");
    }

    [Fact]
    public void UnderAlarm_IsTriggered()
    {
        var alarm = SAlarm.Create(SAlarmType.Under, 50.00m, "buy", "");
        var eod = new FullEOD() { Close = 48.00m };
        Assert.True(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void OverAlarm_IsTriggered()
    {
        var alarm = SAlarm.Create(SAlarmType.Over, 50.00m, "sell", "");
        var eod = new FullEOD() { Close = 55.00m };
        Assert.True(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void UnderAlarm_NotTriggered_WhenAbove()
    {
        var alarm = SAlarm.Create(SAlarmType.Under, 50.00m, "buy", "");
        var eod = new FullEOD() { Close = 55.00m };
        Assert.False(alarm.IsAlarmTriggered(eod));
    }

    // Trailing Sell % tests

    [Fact]
    public void TrailingSellP_Create_Success()
    {
        string prms = SAlarmTrailingSellP.CreatePrms(10m, 100m);
        var alarm = SAlarm.Create(SAlarmType.TrailingSellP, 100.00m, "trailing", prms);
        Assert.Equal(SAlarmType.TrailingSellP, alarm.AlarmType);
        var tsAlarm = (SAlarmTrailingSellP)alarm;
        Assert.Equal(10m, tsAlarm.DropP);
        Assert.Equal(100m, tsAlarm.High);
    }

    [Fact]
    public void TrailingSellP_Triggers_OnDrop()
    {
        string prms = SAlarmTrailingSellP.CreatePrms(10m, 100m);
        var alarm = SAlarm.Create(SAlarmType.TrailingSellP, 100.00m, "sell", prms);
        // 10% drop from High=100 means trigger at Close <= 90
        var eod = new FullEOD() { Close = 89.00m };
        Assert.True(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void TrailingSellP_NotTrigger_SmallDrop()
    {
        string prms = SAlarmTrailingSellP.CreatePrms(10m, 100m);
        var alarm = SAlarm.Create(SAlarmType.TrailingSellP, 100.00m, "sell", prms);
        // 5% drop - should not trigger
        var eod = new FullEOD() { Close = 95.00m };
        Assert.False(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void TrailingSellP_HighUpdates_ThenTriggers()
    {
        string prms = SAlarmTrailingSellP.CreatePrms(10m, 100m);
        var alarm = SAlarm.Create(SAlarmType.TrailingSellP, 100.00m, "sell", prms);
        // Price goes up to 120 - High should update
        var eod1 = new FullEOD() { Close = 120.00m };
        Assert.False(alarm.IsAlarmTriggered(eod1));
        var tsAlarm = (SAlarmTrailingSellP)alarm;
        Assert.Equal(120.00m, tsAlarm.High);
        // Now 10% drop from 120 = trigger at 108, close at 107
        var eod2 = new FullEOD() { Close = 107.00m };
        Assert.True(alarm.IsAlarmTriggered(eod2));
    }

    // Trailing Buy % tests

    [Fact]
    public void TrailingBuyP_Create_Success()
    {
        string prms = SAlarmTrailingBuyP.CreatePrms(15m, 50m);
        var alarm = SAlarm.Create(SAlarmType.TrailingBuyP, 50.00m, "buy", prms);
        Assert.Equal(SAlarmType.TrailingBuyP, alarm.AlarmType);
        var tbAlarm = (SAlarmTrailingBuyP)alarm;
        Assert.Equal(15m, tbAlarm.RecoverP);
        Assert.Equal(50m, tbAlarm.Low);
    }

    [Fact]
    public void TrailingBuyP_Triggers_OnRecovery()
    {
        string prms = SAlarmTrailingBuyP.CreatePrms(20m, 50m);
        var alarm = SAlarm.Create(SAlarmType.TrailingBuyP, 50.00m, "buy", prms);
        // 20% recovery from Low=50 means trigger at Close >= 60
        var eod = new FullEOD() { Close = 61.00m };
        Assert.True(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void TrailingBuyP_NotTrigger_SmallRecovery()
    {
        string prms = SAlarmTrailingBuyP.CreatePrms(20m, 50m);
        var alarm = SAlarm.Create(SAlarmType.TrailingBuyP, 50.00m, "buy", prms);
        // 10% recovery - should not trigger
        var eod = new FullEOD() { Close = 55.00m };
        Assert.False(alarm.IsAlarmTriggered(eod));
    }

    [Fact]
    public void TrailingBuyP_LowUpdates_ThenTriggers()
    {
        string prms = SAlarmTrailingBuyP.CreatePrms(20m, 50m);
        var alarm = SAlarm.Create(SAlarmType.TrailingBuyP, 50.00m, "buy", prms);
        // Price drops to 40 - Low should update
        var eod1 = new FullEOD() { Close = 40.00m };
        Assert.False(alarm.IsAlarmTriggered(eod1));
        var tbAlarm = (SAlarmTrailingBuyP)alarm;
        Assert.Equal(40.00m, tbAlarm.Low);
        // Now 20% recovery from 40 = trigger at 48, close at 49
        var eod2 = new FullEOD() { Close = 49.00m };
        Assert.True(alarm.IsAlarmTriggered(eod2));
    }

    [Fact]
    public void TrailingSellP_PrmsRoundTrip()
    {
        string prms = SAlarmTrailingSellP.CreatePrms(12.5m, 250.123m);
        var alarm = (SAlarmTrailingSellP)SAlarm.Create(SAlarmType.TrailingSellP, 250.00m, "test", prms);
        // Read back prms via storage format
        string storedPrms = alarm.Prms;
        var alarm2 = (SAlarmTrailingSellP)SAlarm.Create(SAlarmType.TrailingSellP, 250.00m, "test", storedPrms);
        Assert.Equal(alarm.DropP, alarm2.DropP);
        Assert.Equal(alarm.High, alarm2.High);
    }

    [Fact]
    public void TrailingBuyP_PrmsRoundTrip()
    {
        string prms = SAlarmTrailingBuyP.CreatePrms(8.5m, 45.678m);
        var alarm = (SAlarmTrailingBuyP)SAlarm.Create(SAlarmType.TrailingBuyP, 45.00m, "test", prms);
        string storedPrms = alarm.Prms;
        var alarm2 = (SAlarmTrailingBuyP)SAlarm.Create(SAlarmType.TrailingBuyP, 45.00m, "test", storedPrms);
        Assert.Equal(alarm.RecoverP, alarm2.RecoverP);
        Assert.Equal(alarm.Low, alarm2.Low);
    }

    [Fact]
    public void TrailingSellP_DoAction_AddAndVerify()
    {
        var s = StalkerTestFixture.CreateEmpty();
        string prms = SAlarmTrailingSellP.CreatePrms(10m, 100m);
        StalkerAssert.Ok(s.DoAction($"Add-Alarm Type=TrailingSellP SRef=NASDAQ$MSFT Level=100.00 Prms={prms} Note=TrailingStop"));
        var alarms = s.StockAlarms("NASDAQ$MSFT");
        Assert.Single(alarms);
        Assert.Equal(SAlarmType.TrailingSellP, alarms[0].AlarmType);
    }

    [Fact]
    public void TrailingBuyP_DoAction_AddAndVerify()
    {
        var s = StalkerTestFixture.CreateEmpty();
        string prms = SAlarmTrailingBuyP.CreatePrms(15m, 50m);
        StalkerAssert.Ok(s.DoAction($"Add-Alarm Type=TrailingBuyP SRef=NYSE$KO Level=50.00 Prms={prms} Note=Recovery"));
        var alarms = s.StockAlarms("NYSE$KO");
        Assert.Single(alarms);
        Assert.Equal(SAlarmType.TrailingBuyP, alarms[0].AlarmType);
    }
}
