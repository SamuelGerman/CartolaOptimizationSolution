using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Security;
using System.Text;

namespace CartolaOptimization.Application.Utilities;

public static class TrainingDatasetExcelExporter
{
    private const string SpreadsheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string ContentType => SpreadsheetContentType;

    public static byte[] ExportXlsx<T>(string sheetName, IEnumerable<T> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sheetName);
        ArgumentNullException.ThrowIfNull(rows);

        var properties = GetExportableProperties(typeof(T));
        var dataRows = rows.ToList();

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            CreateEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
            CreateEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
            CreateEntry(archive, "xl/workbook.xml", BuildWorkbookXml(SanitizeSheetName(sheetName)));
            CreateEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml());
            CreateEntry(archive, "xl/styles.xml", BuildStylesXml());
            CreateEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(properties, dataRows));
        }

        return stream.ToArray();
    }

    private static PropertyInfo[] GetExportableProperties(Type type) =>
        type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead)
            .OrderBy(static property => property.MetadataToken)
            .ToArray();

    private static void CreateEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string BuildContentTypesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private static string BuildRootRelationshipsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string BuildWorkbookXml(string sheetName) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="{EscapeXml(sheetName)}" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string BuildWorkbookRelationshipsXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string BuildStylesXml() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="1">
            <font>
              <sz val="11"/>
              <name val="Calibri"/>
            </font>
          </fonts>
          <fills count="1">
            <fill>
              <patternFill patternType="none"/>
            </fill>
          </fills>
          <borders count="1">
            <border>
              <left/>
              <right/>
              <top/>
              <bottom/>
              <diagonal/>
            </border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
          </cellStyleXfs>
          <cellXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
          </cellXfs>
          <cellStyles count="1">
            <cellStyle name="Normal" xfId="0" builtinId="0"/>
          </cellStyles>
        </styleSheet>
        """;

    private static string BuildWorksheetXml<T>(IReadOnlyList<PropertyInfo> properties, IReadOnlyList<T> rows)
    {
        var builder = new StringBuilder();
        builder.Append("""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <sheetViews>
            <sheetView workbookViewId="0">
              <pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>
            </sheetView>
          </sheetViews>
          <sheetData>
        """);

        AppendHeaderRow(builder, properties);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            AppendDataRow(builder, properties, rows[rowIndex]!, rowIndex + 2);
        }

        builder.Append("""
          </sheetData>
          <autoFilter ref="A1:
        """);
        builder.Append(GetCellReference(properties.Count, Math.Max(rows.Count + 1, 1)));
        builder.AppendLine("\"/>");
        builder.Append("""
        </worksheet>
        """);

        return builder.ToString();
    }

    private static void AppendHeaderRow(StringBuilder builder, IReadOnlyList<PropertyInfo> properties)
    {
        builder.AppendLine("""    <row r="1">""");

        for (var columnIndex = 0; columnIndex < properties.Count; columnIndex++)
        {
            AppendInlineStringCell(builder, columnIndex + 1, 1, properties[columnIndex].Name);
        }

        builder.AppendLine("    </row>");
    }

    private static void AppendDataRow<T>(StringBuilder builder, IReadOnlyList<PropertyInfo> properties, T row, int rowNumber)
    {
        builder.Append("    <row r=\"");
        builder.Append(rowNumber);
        builder.AppendLine("\">");

        for (var columnIndex = 0; columnIndex < properties.Count; columnIndex++)
        {
            AppendValueCell(builder, columnIndex + 1, rowNumber, properties[columnIndex].GetValue(row));
        }

        builder.AppendLine("    </row>");
    }

    private static void AppendValueCell(StringBuilder builder, int columnIndex, int rowNumber, object? value)
    {
        switch (value)
        {
            case null:
                AppendInlineStringCell(builder, columnIndex, rowNumber, string.Empty);
                return;
            case bool boolean:
                AppendBooleanCell(builder, columnIndex, rowNumber, boolean);
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                AppendNumberCell(builder, columnIndex, rowNumber, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0");
                return;
            case DateTime dateTime:
                AppendInlineStringCell(builder, columnIndex, rowNumber, dateTime.ToString("O", CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset dateTimeOffset:
                AppendInlineStringCell(builder, columnIndex, rowNumber, dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
                return;
            case IFormattable formattable:
                AppendInlineStringCell(builder, columnIndex, rowNumber, formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            default:
                AppendInlineStringCell(builder, columnIndex, rowNumber, value.ToString() ?? string.Empty);
                return;
        }
    }

    private static void AppendInlineStringCell(StringBuilder builder, int columnIndex, int rowNumber, string value)
    {
        builder.Append("      <c r=\"");
        builder.Append(GetCellReference(columnIndex, rowNumber));
        builder.Append("\" t=\"inlineStr\"><is><t>");
        builder.Append(EscapeXml(value));
        builder.AppendLine("</t></is></c>");
    }

    private static void AppendNumberCell(StringBuilder builder, int columnIndex, int rowNumber, string value)
    {
        builder.Append("      <c r=\"");
        builder.Append(GetCellReference(columnIndex, rowNumber));
        builder.Append("\"><v>");
        builder.Append(EscapeXml(value));
        builder.AppendLine("</v></c>");
    }

    private static void AppendBooleanCell(StringBuilder builder, int columnIndex, int rowNumber, bool value)
    {
        builder.Append("      <c r=\"");
        builder.Append(GetCellReference(columnIndex, rowNumber));
        builder.Append("\" t=\"b\"><v>");
        builder.Append(value ? '1' : '0');
        builder.AppendLine("</v></c>");
    }

    private static string GetCellReference(int columnIndex, int rowNumber) =>
        $"{GetColumnName(columnIndex)}{rowNumber}";

    private static string GetColumnName(int columnIndex)
    {
        var dividend = columnIndex;
        var columnName = new StringBuilder();

        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName.Insert(0, (char)('A' + modulo));
            dividend = (dividend - modulo) / 26;
        }

        return columnName.ToString();
    }

    private static string SanitizeSheetName(string sheetName)
    {
        var invalidChars = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(sheetName.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray()).Trim();
        if (sanitized.Length == 0)
        {
            sanitized = "Dataset";
        }

        return sanitized.Length <= 31 ? sanitized : sanitized[..31];
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
