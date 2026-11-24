using System.Globalization;

namespace CodeAnalyzer.UI;

public sealed record SourcePreview(string Text, string LineNumbers)
{
    public static SourcePreview ForSource(string source, int firstLine)
    {
        var text = new List<string>();
        var numbers = new List<string>();
        Append(text, numbers, source, firstLine);
        return new(source, string.Join(Environment.NewLine, numbers));
    }

    public static SourcePreview ForType(TypeInfo type, string prefix)
    {
        var text = new List<string>();
        var numbers = new List<string>();
        if(prefix.Length > 0)
        {
            Append(text, numbers, prefix.TrimEnd('\r', '\n'), null);
            Append(text, numbers, "", null);
        }
        if(type.SourceParts.Count == 0) Append(text, numbers, type.Source, type.Line);
        else
            foreach(var part in type.SourceParts)
            {
                if(part != type.SourceParts[0]) Append(text, numbers, "", null);
                Append(text, numbers, $"// {part.FilePath}:{part.Line}", null);
                Append(text, numbers, part.Source, part.Line);
            }
        return new(string.Join(Environment.NewLine, text), string.Join(Environment.NewLine, numbers));
    }

    private static void Append(List<string> text, List<string> numbers, string source, int? firstLine)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for(var index = 0; index < lines.Length; index++)
        {
            text.Add(lines[index]);
            numbers.Add(firstLine is int line ? (line + index).ToString(CultureInfo.InvariantCulture) : "");
        }
    }
}