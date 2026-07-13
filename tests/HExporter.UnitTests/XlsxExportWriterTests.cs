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
}
