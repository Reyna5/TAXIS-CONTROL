using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ControlTaxi.Desktop.Services;

public sealed record ExportTable(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed class DesktopExportService
{
    public byte[] BuildCsv(ExportTable table)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", table.Headers.Select(EscapeCsv)));
        foreach (var row in table.Rows)
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public byte[] BuildExcelWorkbook(params ExportTable[] tables)
    {
        if (tables.Length == 0)
            tables = [new ExportTable("Reporte", [], [])];

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", BuildContentTypes(tables.Length));
            AddEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(tables.Length));
            AddEntry(archive, "xl/workbook.xml", BuildWorkbook(tables));
            AddEntry(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
                  <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0"/></cellXfs>
                </styleSheet>
                """);

            for (var i = 0; i < tables.Length; i++)
                AddEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(tables[i]));
        }

        return stream.ToArray();
    }

    public byte[] BuildPdf(IEnumerable<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 11 Tf");
        content.AppendLine("42 780 Td");
        foreach (var line in lines.Take(42))
        {
            content.Append('(').Append(EscapePdf(line)).AppendLine(") Tj");
            content.AppendLine("0 -17 Td");
        }
        content.AppendLine("ET");

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}endstream"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects.Select((value, index) => new { value, index }))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(obj.index + 1).AppendLine(" 0 obj");
            pdf.AppendLine(obj.value);
            pdf.AppendLine("endobj");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {objects.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
            pdf.AppendLine($"{offset:0000000000} 00000 n ");
        pdf.AppendLine("trailer");
        pdf.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xref.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    public async Task SaveAsync(string path, byte[] content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(path, content);
    }

    private static string BuildContentTypes(int sheetCount)
    {
        var builder = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            """);
        for (var i = 1; i <= sheetCount; i++)
            builder.AppendLine($"  <Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        builder.AppendLine("</Types>");
        return builder.ToString();
    }

    private static string BuildWorkbookRelationships(int sheetCount)
    {
        var builder = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            """);
        for (var i = 1; i <= sheetCount; i++)
            builder.AppendLine($"  <Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        builder.AppendLine($"  <Relationship Id=\"rId{sheetCount + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        builder.AppendLine("</Relationships>");
        return builder.ToString();
    }

    private static string BuildWorkbook(IReadOnlyList<ExportTable> tables)
    {
        var builder = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
            """);
        for (var i = 0; i < tables.Count; i++)
            builder.AppendLine($"    <sheet name=\"{XmlEscape(SafeSheetName(tables[i].Name))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        builder.AppendLine("  </sheets>");
        builder.AppendLine("</workbook>");
        return builder.ToString();
    }

    private static string BuildWorksheet(ExportTable table)
    {
        var builder = new StringBuilder("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
            """);
        builder.AppendLine(BuildRow(1, table.Headers, header: true));
        for (var i = 0; i < table.Rows.Count; i++)
            builder.AppendLine(BuildRow(i + 2, table.Rows[i], header: false));
        builder.AppendLine("  </sheetData>");
        builder.AppendLine("</worksheet>");
        return builder.ToString();
    }

    private static string BuildRow(int rowIndex, IReadOnlyList<string> values, bool header)
    {
        var builder = new StringBuilder($"    <row r=\"{rowIndex}\">");
        for (var i = 0; i < values.Count; i++)
        {
            var cell = $"{ColumnName(i + 1)}{rowIndex}";
            var style = header ? " s=\"1\"" : string.Empty;
            builder.Append($"<c r=\"{cell}\" t=\"inlineStr\"{style}><is><t>{XmlEscape(values[i])}</t></is></c>");
        }
        builder.Append("</row>");
        return builder.ToString();
    }

    private static string ColumnName(int index)
    {
        var dividend = index;
        var name = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            dividend = (dividend - modulo) / 26;
        }
        return name;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }

    private static string EscapePdf(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    private static string XmlEscape(string? value) => SecurityElementEscape(value ?? string.Empty);

    private static string SecurityElementEscape(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

    private static string SafeSheetName(string value)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Reporte" : cleaned[..Math.Min(cleaned.Length, 31)];
    }
}
