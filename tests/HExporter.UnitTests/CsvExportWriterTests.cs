using System.Text;
using FluentAssertions;
using HExporter.Core.Models;
using HExporter.Export.Csv;

namespace HExporter.UnitTests;

public class CsvExportWriterTests
{
    private static ColumnSchema[] Schema(params string[] names)
        => names.Select((n, i) => new ColumnSchema(i, n, typeof(object), "OBJ")).ToArray();

    private static async Task<string> WriteAsync(ColumnSchema[] schema, object?[][] rows, ExportOptions opt)
    {
        using var ms = new MemoryStream();
        await using (var w = new CsvExportWriter(ms, opt))
        {
            await w.BeginAsync(schema, default);
            var reader = new FakeRecordReader(schema, rows);
            while (await reader.ReadAsync(default)) w.WriteRow(reader);
            await w.EndAsync(default);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    [Fact]
    public async Task Writes_headers_and_rows()
    {
        var csv = await WriteAsync(Schema("id", "name"),
            new object?[][] { new object?[] { 1, "ana" }, new object?[] { 2, "beto" } },
            new ExportOptions());
        csv.Should().Be("id,name\n1,ana\n2,beto\n");
    }

    [Fact]
    public async Task Null_becomes_empty_cell()
    {
        var csv = await WriteAsync(Schema("a", "b"),
            new object?[][] { new object?[] { null, "x" } }, new ExportOptions());
        csv.Should().Be("a,b\n,x\n");
    }

    [Fact]
    public async Task Quotes_values_with_delimiter_quote_or_newline()
    {
        var csv = await WriteAsync(Schema("v"),
            new object?[][]
            {
                new object?[] { "a,b" },
                new object?[] { "he said \"hi\"" },
                new object?[] { "line1\nline2" }
            },
            new ExportOptions());
        csv.Should().Be("v\n\"a,b\"\n\"he said \"\"hi\"\"\"\n\"line1\nline2\"\n");
    }

    [Fact]
    public async Task Uses_fixed_culture_for_numbers_and_dates()
    {
        var opt = new ExportOptions { CultureName = "en-US", DateFormat = "yyyy-MM-dd" };
        var csv = await WriteAsync(Schema("n", "d"),
            new object?[][] { new object?[] { 1234.5m, new DateTime(2026, 1, 31) } }, opt);
        csv.Should().Be("n,d\n1234.5,2026-01-31\n");
    }

    [Fact]
    public async Task No_headers_option_omits_header_row()
    {
        var csv = await WriteAsync(Schema("id"),
            new object?[][] { new object?[] { 7 } },
            new ExportOptions { IncludeHeaders = false });
        csv.Should().Be("7\n");
    }

    [Fact]
    public async Task Custom_delimiter()
    {
        var opt = new ExportOptions { Csv = new CsvOptions { Delimiter = ";" } };
        var csv = await WriteAsync(Schema("a", "b"),
            new object?[][] { new object?[] { 1, 2 } }, opt);
        csv.Should().Be("a;b\n1;2\n");
    }
}
