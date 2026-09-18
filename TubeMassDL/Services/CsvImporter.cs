using System.Diagnostics;
using System.Text;

namespace TubeMassDL.Services;

/// <summary>
/// Parseo de CSV con dos columnas: NOMBRE,LINK. Soporta comillas (nombres con comas
/// o separador punto y coma) y detecta el separador real. No agrega dependencias.
/// </summary>
public static class CsvImporter
{
    public static (List<(string Name, string Url)> Rows, int Invalid) Parse(string text)
    {
        var rows = new List<(string, string)>();
        int invalid = 0;
        if (string.IsNullOrWhiteSpace(text)) return (rows, invalid);

        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        char sep = DetectSeparator(lines);
        bool seenData = false;

        foreach (var raw in lines)
        {
            var fields = SplitLine(raw, sep);
            if (fields.Count == 1 && string.IsNullOrWhiteSpace(fields[0])) continue;

            if (fields.Count < 2) { invalid++; continue; }

            string name = fields[0].Trim().Trim('"');
            string url = fields[1].Trim().Trim('"');

            if (!seenData && !IsHttpUrl(url))
            {
                seenData = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || !IsHttpUrl(url))
            {
                invalid++;
                continue;
            }

            seenData = true;
            rows.Add((name, url));
        }

        return (rows, invalid);
    }

    private static bool IsHttpUrl(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // ponytail: recuento simple en las primeras líneas; suficiente para distinguir CSV de Excel es (;) vs estándar (,)
    private static char DetectSeparator(string[] lines)
    {
        int commas = 0, semis = 0;
        for (int i = 0; i < lines.Length && i < 3; i++)
        {
            commas += lines[i].Count(c => c == ',');
            semis += lines[i].Count(c => c == ';');
        }
        return semis > commas ? ';' : ',';
    }

    // ponytail: maneja comillas simples; comillas dobles escapadas ("" ) se pierden, caso raro en títulos
    private static List<string> SplitLine(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == sep && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    [Conditional("DEBUG")]
    public static void SelfTest()
    {
        var (rows, invalid) = Parse("NOMBRE,LINK\nT00E01 - Los visitantes,https://fs2.cnubis.com/3hi?download_token=abc&.mp4\n\"Cap. 1, la prueba\",http://x.com/v.mp4\n,https://roto\nhttps://sin-nombre\n\n");
        Debug.Assert(rows.Count == 2, $"rows={rows.Count}");
        Debug.Assert(rows[0].Name == "T00E01 - Los visitantes");
        Debug.Assert(rows[0].Url == "https://fs2.cnubis.com/3hi?download_token=abc&.mp4");
        Debug.Assert(rows[1].Name == "Cap. 1, la prueba");
        Debug.Assert(invalid == 2, $"invalid={invalid}");

        var (rows2, invalid2) = Parse("NOMBRE;LINK\nEpisodio 1;https://x.com/1.mp4\nEpisodio 2;https://x.com/2.mp4");
        Debug.Assert(rows2.Count == 2, $"rows2={rows2.Count}");
        Debug.Assert(rows2[1].Url == "https://x.com/2.mp4");
        Debug.Assert(invalid2 == 0);
    }
}