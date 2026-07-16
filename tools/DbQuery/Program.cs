using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0 ? args[0] : Path.Combine(
    AppContext.BaseDirectory,
    "../../../../../src/UserQuotaApi.API/quota.db");

dbPath = Path.GetFullPath(dbPath);

if (!File.Exists(dbPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Database not found: {dbPath}");
    Console.WriteLine("Usage: dotnet run --project tools/DbQuery [path/to/quota.db]");
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"Connected to: {dbPath}");
Console.ResetColor();

using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

PrintTable(conn, "Users",  "SELECT Id, Name, Email, CreatedAt FROM Users ORDER BY Id");
PrintTable(conn, "Quotas", "SELECT Id, UserId, ConsumedCount, MaxRequests, LastConsumedAt, Version FROM Quotas ORDER BY UserId");

return 0;

static void PrintTable(SqliteConnection conn, string title, string sql)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkCyan;
    Console.WriteLine($"┌─ {title} {'─' + new string('─', Math.Max(0, 50 - title.Length))}┐");
    Console.ResetColor();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();

    // Print header
    var cols = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToList();
    var widths = cols.Select(c => c.Length).ToList();

    var rows = new List<List<string>>();
    while (reader.Read())
    {
        var row = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString()!)
            .ToList();
        for (var i = 0; i < widths.Count; i++)
            widths[i] = Math.Max(widths[i], row[i].Length);
        rows.Add(row);
    }

    if (rows.Count == 0)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  (no rows)");
        Console.ResetColor();
        return;
    }

    var sep = "  +" + string.Join("+", widths.Select(w => new string('-', w + 2))) + "+";
    Console.WriteLine(sep);
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine("  |" + string.Join("|", cols.Select((c, i) => $" {c.PadRight(widths[i])} ")) + "|");
    Console.ResetColor();
    Console.WriteLine(sep);
    foreach (var row in rows)
        Console.WriteLine("  |" + string.Join("|", row.Select((v, i) => $" {v.PadRight(widths[i])} ")) + "|");
    Console.WriteLine(sep);
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  {rows.Count} row(s)");
    Console.ResetColor();
}
