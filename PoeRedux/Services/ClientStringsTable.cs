using System.IO;
using System.Text;

using LibBundle3.Nodes;

namespace PoeRedux.Services;

/// <summary>
/// Reads and rewrites rows of data/clientstrings.datc64 by Id. Id and Text are the table's first
/// two columns, both plain strings, so their offsets are known without the rest of the schema:
/// Validates the row layout and string bounds before writing anything.
/// </summary>
public static class ClientStringsTable
{
    public const string Path = "data/balance/clientstrings.datc64";
    private const int IdColumnOffset = 0;
    private const int TextColumnOffset = 8;

    /// <summary>
    /// Rewrites the Text of each given Id to the value a <paramref name="replace"/> callback
    /// returns, or leaves it be when the callback returns null.
    /// </summary>
    /// <returns>
    /// How many of the ids the callback answered for were actually found in the table.
    /// A row already carrying its replacement counts as found: re-applying an already-applied
    /// patch is a legitimate no-op, not a miss. Zero found means every id the caller asked about
    /// is absent, which is the real "the table moved" signal.
    /// </returns>
    public static int Rewrite(FileNode table, Func<string, string, string?> replace,
        IReadOnlyCollection<string>? requiredIds = null)
    {
        var data = table.Record.Read().ToArray();
        if (data.Length < 28)
            throw new InvalidDataException("clientstrings.datc64 is too short.");
        var rows = (int)BitConverter.ToUInt32(data, 0);
        if (rows <= 0)
            throw new InvalidDataException("clientstrings.datc64 has no rows.");
        var separator = IndexOfSeparator(data);
        if ((separator - 4) % rows != 0)
            throw new InvalidDataException("clientstrings.datc64 has an unexpected row layout.");
        var rowWidth = (separator - 4) / rows;
        if (rowWidth < 16)
            throw new InvalidDataException("clientstrings.datc64 rows are too short for Id and Text.");

        var found = 0;
        var foundIds = new HashSet<string>(StringComparer.Ordinal);
        var wrote = false;
        for (var row = 0; row < rows; row++)
        {
            var id = ReadString(data, separator, 4 + row * rowWidth + IdColumnOffset);
            var text = ReadString(data, separator, 4 + row * rowWidth + TextColumnOffset);
            var replacement = replace(id, text);
            if (replacement is null)
                continue;

            found++;
            foundIds.Add(id);
            if (replacement == text)
                continue;

            data = DatWriter.SetString(data, rowWidth, row, TextColumnOffset, replacement);
            // The heap grew, so the separator that later rows' offsets are relative to did not
            // move, but re-deriving it keeps this loop honest if that ever changes.
            separator = IndexOfSeparator(data);
            wrote = true;
        }

        if (requiredIds is not null && requiredIds.Any(id => !foundIds.Contains(id)))
            throw new InvalidDataException("Some supported tooltip line identifiers were not found; no tooltip text was changed.");

        if (wrote)
        {
            BackupManager.RecordOriginal(table.Record);
            table.Record.Write(data);
        }
        return found;
    }

    private static string ReadString(byte[] table, int separator, int cellOffset)
    {
        if (cellOffset < 4 || cellOffset > separator - 8)
            throw new InvalidDataException("clientstrings.datc64 contains an invalid string cell.");
        var offset = separator + (long)BitConverter.ToUInt64(table, cellOffset);
        if (offset < separator + 8 || offset >= table.Length || (offset - separator) % 2 != 0)
            throw new InvalidDataException("clientstrings.datc64 contains an invalid string offset.");
        var end = offset;
        while (end + 1 < table.Length && !(table[end] == 0 && table[end + 1] == 0))
            end += 2;
        if (end + 1 >= table.Length)
            throw new InvalidDataException("clientstrings.datc64 contains an unterminated string.");
        return Encoding.Unicode.GetString(table, (int)offset, (int)(end - offset));
    }

    private static int IndexOfSeparator(byte[] data)
    {
        for (var i = 4; i < data.Length - 8; i++)
        {
            var match = true;
            for (var j = 0; j < 8; j++)
                if (data[i + j] != 0xBB) { match = false; break; }
            if (match)
                return i;
        }
        throw new InvalidDataException("clientstrings.datc64 has no row/data separator.");
    }
}
