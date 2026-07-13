using System.Globalization;
using System.Text;

namespace HExporter.Core.Models;

/// <summary>Opciones comunes + específicas por formato. Ver docs/05-configuration.md.</summary>
public sealed class ExportOptions
{
    public bool IncludeHeaders { get; init; } = true;
    public int FlushEveryRows { get; init; } = 10_000;
    public int FileBufferBytes { get; init; } = 128 * 1024;
    public string CultureName { get; init; } = "en-US";
    public string DateFormat { get; init; } = "yyyy-MM-dd HH:mm:ss";

    public CsvOptions Csv { get; init; } = new();
    public XlsxOptions Xlsx { get; init; } = new();

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);
}

public enum EncodingKind { Utf8, Utf8Bom, Latin1 }

public sealed class CsvOptions
{
    public string Delimiter { get; init; } = ",";
    public EncodingKind Encoding { get; init; } = EncodingKind.Utf8;

    public Encoding ResolveEncoding() => Encoding switch
    {
        EncodingKind.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        EncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        EncodingKind.Latin1 => System.Text.Encoding.Latin1,
        _ => new UTF8Encoding(false)
    };
}

public enum XlsxRowLimitStrategy { Fail, NewSheet }

public sealed class XlsxOptions
{
    /// <summary>Límite duro del formato XLSX: 1.048.576 filas por hoja.</summary>
    public const int MaxRowsPerSheet = 1_048_576;

    public string SheetName { get; init; } = "Datos";
    public XlsxRowLimitStrategy RowLimitStrategy { get; init; } = XlsxRowLimitStrategy.Fail;
}
