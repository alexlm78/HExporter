using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Export.Xlsx;
using MiniExcelLibs;

namespace HExporter.UnitTests;

public class XlsxExportWriterTests
{
    private static ColumnSchema[] Schema(params string[] names)
        => names.Select((n, i) => new ColumnSchema(i, n, typeof(object), "OBJ")).ToArray();

    [Fact]
    public async Task Writes_streaming_xlsx_with_headers_and_rows()
    {
        var schema = Schema("id", "name");
        var rows = new object?[][] { new object?[] { 1, "ana" }, new object?[] { 2, "beto" } };

        using var ms = new MemoryStream();
        await using (var w = new XlsxExportWriter(ms, new ExportOptions()))
        {
            await w.BeginAsync(schema, default);
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }

        ms.Position = 0;
        var read = ms.Query(useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();
        read.Should().HaveCount(2);
        read[0]["id"].Should().Be(1d);
        read[0]["name"].Should().Be("ana");
        read[1]["name"].Should().Be("beto");
    }

    [Fact]
    public async Task NewSheet_strategy_rolls_over_when_row_limit_exceeded()
    {
        var schema = Schema("id");
        var options = new ExportOptions
        {
            Xlsx = new XlsxOptions { SheetName = "Datos", RowLimitStrategy = XlsxRowLimitStrategy.NewSheet }
        };

        using var ms = new MemoryStream();
        await using (var w = new XlsxExportWriter(ms, options) { MaxRowsPerSheetOverride = 3 })
        {
            await w.BeginAsync(schema, default);
            var rows = Enumerable.Range(1, 7).Select(i => new object?[] { i }).ToArray();
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }

        ms.Position = 0;
        var sheetNames = ms.GetSheetNames();
        sheetNames.Should().BeEquivalentTo(["Datos", "Datos_2", "Datos_3"]);

        var sheet1 = ms.Query(useHeaderRow: true, sheetName: "Datos").Cast<IDictionary<string, object>>().ToList();
        var sheet2 = ms.Query(useHeaderRow: true, sheetName: "Datos_2").Cast<IDictionary<string, object>>().ToList();
        var sheet3 = ms.Query(useHeaderRow: true, sheetName: "Datos_3").Cast<IDictionary<string, object>>().ToList();

        sheet1.Should().HaveCount(3);
        sheet2.Should().HaveCount(3);
        sheet3.Should().HaveCount(1);
    }

    [Fact]
    public async Task Fail_strategy_still_throws_when_row_limit_exceeded()
    {
        var schema = Schema("id");
        var options = new ExportOptions
        {
            Xlsx = new XlsxOptions { RowLimitStrategy = XlsxRowLimitStrategy.Fail }
        };

        using var ms = new MemoryStream();
        await using var w = new XlsxExportWriter(ms, options) { MaxRowsPerSheetOverride = 2 };
        await w.BeginAsync(schema, default);
        var rows = Enumerable.Range(1, 3).Select(i => new object?[] { i }).ToArray();
        var reader = new FakeRecordReader(schema, rows);

        var act = async () =>
        {
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
