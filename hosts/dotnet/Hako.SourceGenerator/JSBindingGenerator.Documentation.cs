using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace HakoJS.SourceGenerator;

public partial class JSBindingGenerator
{
    #region TypeScript Documentation Formatting

    private static string FormatTsDoc(string? documentation, Dictionary<string, string>? paramDocs = null,
        string? returnDoc = null, int indent = 2)
    {
        if (string.IsNullOrWhiteSpace(documentation) &&
            (paramDocs == null || paramDocs.Count == 0) &&
            string.IsNullOrWhiteSpace(returnDoc))
            return "";

        var sb = new StringBuilder();
        var indentStr = new string(' ', indent);

        sb.AppendLine($"{indentStr}/**");

        if (!string.IsNullOrWhiteSpace(documentation))
        {
            var lines = documentation.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (!string.IsNullOrWhiteSpace(trimmed))
                    sb.AppendLine($"{indentStr} * {trimmed}");
                else if (i > 0 && i < lines.Length - 1) sb.AppendLine($"{indentStr} *");
            }
        }

        if (paramDocs is { Count: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(documentation))
                sb.AppendLine($"{indentStr} *");

            foreach (var param in paramDocs)
            {
                var paramLines = param.Value.Split('\n');
                var firstLine = true;

                foreach (var line in paramLines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        if (firstLine)
                        {
                            sb.AppendLine($"{indentStr} * @param {param.Key} {trimmed}");
                            firstLine = false;
                        }
                        else
                        {
                            sb.AppendLine($"{indentStr} *   {trimmed}");
                        }
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(returnDoc))
        {
            if (!string.IsNullOrWhiteSpace(documentation) || paramDocs is { Count: > 0 })
                sb.AppendLine($"{indentStr} *");

            var returnLines = returnDoc.Split('\n');
            var firstLine = true;

            foreach (var line in returnLines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    if (firstLine)
                    {
                        sb.AppendLine($"{indentStr} * @returns {trimmed}");
                        firstLine = false;
                    }
                    else
                    {
                        sb.AppendLine($"{indentStr} *   {trimmed}");
                    }
                }
            }
        }

        sb.AppendLine($"{indentStr} */");

        return sb.ToString();
    }

    #endregion

    #region XML Documentation Extraction

    private static string? ExtractXmlDocumentation(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var doc = XDocument.Parse(xml);
            var sections = new List<string>();

            var summary = doc.Descendants("summary").FirstOrDefault();
            if (summary != null)
            {
                var summaryContent = ProcessXmlNode(summary);
                var summaryText = NormalizeWhitespace(summaryContent);
                if (!string.IsNullOrWhiteSpace(summaryText))
                    sections.Add(summaryText);
            }

            var remarks = doc.Descendants("remarks").FirstOrDefault();
            if (remarks != null)
            {
                var remarksContent = ProcessXmlNode(remarks);
                var remarksText = NormalizeWhitespace(remarksContent);
                if (!string.IsNullOrWhiteSpace(remarksText))
                    sections.Add(remarksText);
            }

            var example = doc.Descendants("example").FirstOrDefault();
            if (example != null)
            {
                var exampleContent = ProcessXmlNode(example);
                var exampleText = NormalizeWhitespace(exampleContent);
                if (!string.IsNullOrWhiteSpace(exampleText))
                    sections.Add($"**Example:**\n\n{exampleText}");
            }

            return sections.Count > 0 ? string.Join("\n\n", sections) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ExtractParameterDocs(ISymbol symbol)
    {
        var paramDocs = new Dictionary<string, string>();
        var xml = symbol.GetDocumentationCommentXml();

        if (string.IsNullOrWhiteSpace(xml))
            return paramDocs;

        try
        {
            var doc = XDocument.Parse(xml);

            foreach (var param in doc.Descendants("param"))
            {
                var nameAttr = param.Attribute("name");
                if (nameAttr != null)
                {
                    var paramContent = ProcessXmlNode(param);
                    var paramText = NormalizeWhitespace(paramContent);
                    if (!string.IsNullOrWhiteSpace(paramText))
                        paramDocs[nameAttr.Value] = paramText;
                }
            }

            foreach (var typeParam in doc.Descendants("typeparam"))
            {
                var nameAttr = typeParam.Attribute("name");
                if (nameAttr != null)
                {
                    var typeParamContent = ProcessXmlNode(typeParam);
                    var typeParamText = NormalizeWhitespace(typeParamContent);
                    if (!string.IsNullOrWhiteSpace(typeParamText))
                        paramDocs[nameAttr.Value] = typeParamText;
                }
            }
        }
        catch
        {
        }

        return paramDocs;
    }

    private static string? ExtractReturnDoc(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();

        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            var doc = XDocument.Parse(xml);
            var returns = doc.Descendants("returns").FirstOrDefault();
            if (returns != null)
            {
                var returnContent = ProcessXmlNode(returns);
                var returnText = NormalizeWhitespace(returnContent);
                if (!string.IsNullOrWhiteSpace(returnText))
                    return returnText;
            }
        }
        catch
        {
        }

        return null;
    }

    #endregion

    #region XML to Markdown Conversion

    private static string ProcessXmlNode(XElement element)
    {
        var result = new StringBuilder();

        foreach (var node in element.Nodes())
            if (node is XText textNode)
                result.Append(textNode.Value);
            else if (node is XElement childElement)
                result.Append(ProcessXmlElement(childElement));

        return result.ToString();
    }

    private static string ProcessXmlElement(XElement element)
    {
        switch (element.Name.LocalName.ToLowerInvariant())
        {
            case "c":
                return $"`{element.Value.Trim()}`";

            case "code":
                var codeContent = element.Value.Trim('\r', '\n');
                return $"\n```\n{codeContent}\n```\n";

            case "b":
            case "strong":
                return $"**{ProcessXmlNode(element)}**";

            case "i":
            case "em":
                return $"*{ProcessXmlNode(element)}*";

            case "u":
                return $"<u>{ProcessXmlNode(element)}</u>";

            case "br":
                return "\n";

            case "para":
                var paraContent = ProcessXmlNode(element).Trim();
                return $"\n\n{paraContent}\n\n";

            case "paramref":
            case "typeparamref":
                var refName = element.Attribute("name")?.Value;
                return refName != null ? $"`{refName}`" : "";

            case "see":
                return ProcessSeeElement(element);

            case "seealso":
                return ProcessSeeAlsoElement(element);

            case "list":
                return ProcessListElement(element);

            case "a":
                var href = element.Attribute("href")?.Value;
                var linkText = element.Value.Trim();
                if (!string.IsNullOrEmpty(href))
                    return $"[{linkText}]({href})";
                return linkText;

            default:
                return ProcessXmlNode(element);
        }
    }

    private static string ProcessSeeElement(XElement element)
    {
        var cref = element.Attribute("cref")?.Value;
        var href = element.Attribute("href")?.Value;
        var langword = element.Attribute("langword")?.Value;
        var linkText = element.Value.Trim();

        if (!string.IsNullOrEmpty(href))
        {
            var text = string.IsNullOrEmpty(linkText) ? href : linkText;
            return $"[{text}]({href})";
        }

        if (!string.IsNullOrEmpty(langword)) return $"`{langword}`";

        if (!string.IsNullOrEmpty(cref))
        {
            var referenceName = ExtractTypeNameFromCref(cref);
            var text = string.IsNullOrEmpty(linkText) ? referenceName : linkText;
            return $"`{text}`";
        }

        return linkText;
    }

    private static string ProcessSeeAlsoElement(XElement element)
    {
        var cref = element.Attribute("cref")?.Value;
        var href = element.Attribute("href")?.Value;
        var linkText = element.Value.Trim();

        if (!string.IsNullOrEmpty(href))
        {
            var text = string.IsNullOrEmpty(linkText) ? href : linkText;
            return $"[{text}]({href})";
        }

        if (!string.IsNullOrEmpty(cref))
        {
            var referenceName = ExtractTypeNameFromCref(cref);
            var text = string.IsNullOrEmpty(linkText) ? referenceName : linkText;
            return $"`{text}`";
        }

        return linkText;
    }

    private static string ProcessListElement(XElement element)
    {
        var listType = element.Attribute("type")?.Value ?? "bullet";
        var result = new StringBuilder("\n");

        var items = element.Elements("item").ToList();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var term = item.Element("term")?.Value.Trim();
            var description = item.Element("description");
            var descText = description != null ? ProcessXmlNode(description).Trim() : "";

            string prefix;
            switch (listType.ToLowerInvariant())
            {
                case "number":
                    prefix = $"{i + 1}.";
                    break;
                case "table":
                    if (i == 0)
                    {
                        result.AppendLine();
                        result.AppendLine("| Term | Description |");
                        result.AppendLine("|------|-------------|");
                    }

                    result.AppendLine($"| {term ?? ""} | {descText} |");
                    continue;
                default:
                    prefix = "-";
                    break;
            }

            if (!string.IsNullOrEmpty(term))
                result.AppendLine($"{prefix} **{term}**: {descText}");
            else
                result.AppendLine($"{prefix} {descText}");
        }

        result.AppendLine();
        return result.ToString();
    }

    private static string ExtractTypeNameFromCref(string? cref)
    {
        if (string.IsNullOrEmpty(cref)) return "";

        var withoutPrefix = cref.Contains(':') ? cref.Substring(cref.IndexOf(':') + 1) : cref;
        var parts = withoutPrefix?.Split('.');
        var simpleName = parts[parts.Length - 1];

        var parenIndex = simpleName.IndexOf('(');
        if (parenIndex > 0)
            simpleName = simpleName.Substring(0, parenIndex);

        simpleName = simpleName.Replace("`", "").Replace("{", "<").Replace("}", ">");

        return simpleName;
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var normalizedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            normalizedLines.Add(trimmedLine);
        }

        var result = string.Join("\n", normalizedLines);
        result = result.Trim('\n');

        while (result.Contains("\n\n\n"))
            result = result.Replace("\n\n\n", "\n\n");

        return result;
    }

    #endregion
}