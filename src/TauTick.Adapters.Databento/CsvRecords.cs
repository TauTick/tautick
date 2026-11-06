using System.Text;
using TauTick.MarketData;

namespace TauTick.Adapters.Databento;

internal sealed class CsvInputException(ImportError error) : Exception(error.Message)
{
    public ImportError Error { get; } = error;
}

internal sealed record CsvRecord(long Line, string[] Fields);

// A strict CSV tokenizer: quoting is independent of the schema and physical line counting.
internal sealed class CsvRecords(StreamReader reader)
{
    private readonly char[] _buffer = new char[4096];
    private int _position;
    private int _length;
    private bool _firstCharacter = true;
    private bool _previousCr;
    private bool _skipLf;
    public long Line { get; private set; } = 1;

    public async Task<CsvRecord?> ReadAsync(CancellationToken token)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var start = Line;
        var quoted = false;
        var afterQuote = false;
        var hasInput = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (_position == _length)
            {
                _length = await reader.ReadAsync(_buffer.AsMemory(), token).ConfigureAwait(false);
                _position = 0;
                if (_length == 0)
                {
                    if (quoted)
                        throw Invalid("Unclosed quoted CSV field.", start);
                    if (!hasInput)
                        return null;
                    fields.Add(field.ToString());
                    return new CsvRecord(start, fields.ToArray());
                }
            }

            var c = _buffer[_position++];
            if (_firstCharacter)
            {
                _firstCharacter = false;
                if (c == '\uFEFF') // Optional UTF-8 BOM, only at the beginning of this input.
                    continue;
            }
            if (c == '\r' || (c == '\n' && !_previousCr))
                Line++;
            _previousCr = c == '\r';
            if (_skipLf)
            {
                _skipLf = false;
                if (c == '\n')
                    continue;
            }
            hasInput = true;
            if (quoted)
            {
                if (c == '"')
                {
                    quoted = false;
                    afterQuote = true;
                }
                else
                    field.Append(c);
            }
            else if (afterQuote && c == '"')
            {
                field.Append('"');
                quoted = true;
                afterQuote = false;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                if (fields.Count >= 10)
                    throw Invalid("Expected exactly ten CSV fields.", start);
                field.Clear();
                afterQuote = false;
            }
            else if (c is '\r' or '\n')
            {
                _skipLf = c == '\r';
                fields.Add(field.ToString());
                return new CsvRecord(start, fields.ToArray());
            }
            else if (afterQuote)
                throw Invalid("Only a delimiter or line ending may follow a closing quote.", start);
            else if (c == '"')
            {
                if (field.Length != 0)
                    throw Invalid("A quote must begin a CSV field; embedded quotes must be doubled.", start);
                quoted = true;
            }
            else
                field.Append(c);

            if (field.Length > 4096)
                throw Invalid("A CSV field exceeds the supported 4096-character limit.", start);
        }
    }

    private static CsvInputException Invalid(string message, long row) =>
        new(new ImportError(ImportErrorKind.Format, message, row));
}
