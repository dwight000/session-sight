using System.Text;
using Xunit.Abstractions;

namespace SessionSight.FunctionalTests.Fixtures;

internal static class GoldenTestHelpers
{
    internal static byte[] CreatePdfDocument(string noteContent, int maxLines = 42)
    {
        var lines = WrapTextForPdf(noteContent, maxLineLength: 95)
            .Take(maxLines)
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add("(empty)");
        }

        var streamBuilder = new StringBuilder();
        streamBuilder.AppendLine("BT");
        streamBuilder.AppendLine("/F1 11 Tf");
        streamBuilder.AppendLine("14 TL");
        streamBuilder.AppendLine("50 760 Td");

        for (var i = 0; i < lines.Count; i++)
        {
            streamBuilder.Append('(')
                .Append(EscapePdfString(lines[i]))
                .AppendLine(") Tj");

            if (i < lines.Count - 1)
            {
                streamBuilder.AppendLine("T*");
            }
        }

        streamBuilder.AppendLine("ET");
        var streamContent = streamBuilder.ToString();
        var streamLength = Encoding.ASCII.GetByteCount(streamContent);

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {streamLength} >>\nstream\n{streamContent}endstream"
        };

        using var memory = new MemoryStream();
        var offsets = new List<long> { 0 };

        WriteAscii(memory, "%PDF-1.4\n");

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(memory.Position);
            WriteAscii(memory, $"{i + 1} 0 obj\n");
            WriteAscii(memory, objects[i]);
            WriteAscii(memory, "\nendobj\n");
        }

        var xrefPosition = memory.Position;
        WriteAscii(memory, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(memory, "0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(memory, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(memory, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        WriteAscii(memory, $"startxref\n{xrefPosition}\n%%EOF");

        return memory.ToArray();
    }

    private static IEnumerable<string> WrapTextForPdf(string content, int maxLineLength)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        foreach (var paragraph in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var line = new StringBuilder();

            foreach (var word in words)
            {
                var safeWord = ToAscii(word);
                if (line.Length == 0)
                {
                    line.Append(safeWord);
                    continue;
                }

                if (line.Length + 1 + safeWord.Length > maxLineLength)
                {
                    yield return line.ToString();
                    line.Clear();
                    line.Append(safeWord);
                }
                else
                {
                    line.Append(' ').Append(safeWord);
                }
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }

    private static string ToAscii(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(ch <= 0x7F ? ch : '?');
        }

        return builder.ToString();
    }

    private static string EscapePdfString(string line) =>
        line
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static void WriteAscii(Stream stream, string content)
    {
        var bytes = Encoding.ASCII.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}

internal sealed class PreviewTracker
{
    private readonly string _directory;
    private readonly int _maxFiles;
    private readonly object _lock = new();
    private readonly string _runStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    private bool _directoryReset;
    private int _savedCount;

    internal PreviewTracker(string directory, int maxFiles = 5)
    {
        _directory = directory;
        _maxFiles = maxFiles;
    }

    internal void TrySavePreviewPdf(string noteId, byte[] pdfBytes, ITestOutputHelper output)
    {
        string? outputPath = null;

        lock (_lock)
        {
            if (!_directoryReset)
            {
                Directory.CreateDirectory(_directory);
                foreach (var existingFile in Directory.GetFiles(_directory, "*.pdf", SearchOption.TopDirectoryOnly))
                {
                    File.Delete(existingFile);
                }

                _savedCount = 0;
                _directoryReset = true;
            }

            if (_savedCount >= _maxFiles)
            {
                return;
            }

            var fileName = $"{_runStamp}-{_savedCount + 1:D2}-{noteId}.pdf";
            outputPath = Path.Combine(_directory, fileName);
            File.WriteAllBytes(outputPath, pdfBytes);
            _savedCount++;
        }

        output.WriteLine($"Saved golden preview PDF: {outputPath}");
    }
}
