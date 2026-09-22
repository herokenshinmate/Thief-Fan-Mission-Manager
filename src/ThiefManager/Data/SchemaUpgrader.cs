using Microsoft.Data.Sqlite;

namespace ThiefManager.Data;

/// <summary>
/// The app uses Database.EnsureCreated() rather than EF Core migrations, which only
/// builds the schema for a brand-new database file. Existing databases from earlier
/// versions of the app need new columns added manually so upgrading the app doesn't
/// lose the user's existing catalog.
/// </summary>
public static class SchemaUpgrader
{
    public static void EnsureColumns(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        AddColumnIfMissing(connection, "FanMissions", "InstallStatus", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "FanMissions", "ArchivePath", "TEXT NULL");
        AddColumnIfMissing(connection, "Settings", "Thief1DownloadsFolder", "TEXT NULL");
        AddColumnIfMissing(connection, "Settings", "Thief2DownloadsFolder", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "ThiefGuildUrl", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "ThiefGuildLookupDismissed", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string columnDefinition)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = $"PRAGMA table_info({table})";
            using var reader = checkCommand.ExecuteReader();
            while (reader.Read())
            {
                var existingColumn = reader.GetString(1);
                if (string.Equals(existingColumn, column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition}";
        alterCommand.ExecuteNonQuery();
    }
}
