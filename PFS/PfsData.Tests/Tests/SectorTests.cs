using Pfs.Types;
using PfsData.Tests.Helpers;
using Xunit;

namespace PfsData.Tests.Tests;

public class SectorTests
{
    [Fact]
    public void Set_Sector_CreateNew()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        var sector = s.GetSector(0);
        Assert.Equal("Industry", sector.sectorName);
    }

    [Fact]
    public void Set_Sector_UpdateName()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=OldName"));
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=NewName"));
        Assert.Equal("NewName", s.GetSector(0).sectorName);
    }

    [Fact]
    public void Edit_Sector_SetFieldName()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech"));
        Assert.Equal("Tech", s.GetSector(0).fieldNames[0]);
    }

    [Fact]
    public void Delete_Sector_Field_ClearsStockRefs()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=0 FieldId=0"));
        var sectors = s.GetStockSectors("NASDAQ$MSFT");
        Assert.Equal("Tech", sectors[0]);
        StalkerAssert.Ok(s.DoAction("Delete-Sector SectorId=0 FieldId=0"));
        sectors = s.GetStockSectors("NASDAQ$MSFT");
        Assert.Null(sectors[0]);
    }

    [Fact]
    public void DeleteAll_Sector_RemovesSectorAndRefs()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=1 SectorName=Region"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=1 FieldId=0 FieldName=Europe"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=XETRA$SAP SectorId=1 FieldId=0"));
        StalkerAssert.Ok(s.DoAction("DeleteAll-Sector SectorId=1"));
        Assert.Equal(string.Empty, s.GetSector(1).sectorName);
    }

    [Fact]
    public void Follow_Sector_AssignsStockToField()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=0 FieldId=0"));
        Assert.Equal("Tech", s.GetStockSectors("NASDAQ$MSFT")[0]);
    }

    [Fact]
    public void Unfollow_Sector_RemovesAssignment()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Ok(s.DoAction("Set-Sector SectorId=0 SectorName=Industry"));
        StalkerAssert.Ok(s.DoAction("Edit-Sector SectorId=0 FieldId=0 FieldName=Tech"));
        StalkerAssert.Ok(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=0 FieldId=0"));
        StalkerAssert.Ok(s.DoAction("Unfollow-Sector SRef=NASDAQ$MSFT SectorId=0"));
        Assert.Null(s.GetStockSectors("NASDAQ$MSFT")[0]);
    }

    [Fact]
    public void Follow_UninitializedSector_Fails()
    {
        var s = StalkerTestFixture.CreateEmpty();
        StalkerAssert.Fail(s.DoAction("Follow-Sector SRef=NASDAQ$MSFT SectorId=2 FieldId=0"), "uninitialized");
    }
}
