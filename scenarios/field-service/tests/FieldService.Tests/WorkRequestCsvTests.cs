using FieldService.Data.Entities;
using FieldService.Data.Services;

namespace FieldService.Tests;

public class WorkRequestCsvTests
{
    [Fact]
    public void Parses_valid_rows_and_maps_priority_codes()
    {
        var csv = string.Join("\n",
            "Account;Reference;Site;Summary;Description;Priority",
            "acme;PO-1001;12 Market Street;Leaking tap;\"Kitchen, 2nd floor; drips \"\"constantly\"\"\";P2",
            "HARBOUR;;Dock 3;Door stuck;;Emergency",
            "");

        var result = WorkRequestCsv.Parse(new StringReader(csv));

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Requests.Count);
        var first = result.Requests[0];
        Assert.Equal("ACME", first.CustomerAccount);
        Assert.Equal("PO-1001", first.CustomerReference);
        Assert.Equal("Kitchen, 2nd floor; drips \"constantly\"", first.Description);
        Assert.Equal(WorkOrderPriority.High, first.Priority);
        Assert.Equal("Import", first.Source);
        Assert.Null(result.Requests[1].CustomerReference);
        Assert.Equal(WorkOrderPriority.Emergency, result.Requests[1].Priority);
    }

    [Fact]
    public void Rejects_bad_rows_but_keeps_good_ones()
    {
        var csv = """
            Account;Reference;Site;Summary;Description;Priority
            ACME;R1;Site A;Fix light;;P9
            ACME;R2;;Fix light;;P3
            ACME;R3;Site C
            ACME;R4;Site D;Fix door;;
            """;

        var result = WorkRequestCsv.Parse(new StringReader(csv));

        Assert.Single(result.Requests);
        Assert.Equal(WorkOrderPriority.Normal, result.Requests[0].Priority);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Rejects_unknown_header()
    {
        var result = WorkRequestCsv.Parse(new StringReader("A,B,C\n1,2,3\n"));

        Assert.Empty(result.Requests);
        Assert.Single(result.Errors);
    }
}
