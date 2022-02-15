#if MICROSOFT_SQLITE
using Microsoft.Data.Sqlite;
using LiteCommand = Microsoft.Data.Sqlite.SqliteCommand;
using LiteParameter = Microsoft.Data.Sqlite.SqliteParameter;
#elif OFFICIAL_SQLITE
using System.Data;
using LiteCommand = System.Data.SQLite.SQLiteCommand;
using LiteParameter = System.Data.SQLite.SQLiteParameter;
#endif

namespace Sqlar;

public static class SqlarExtensions
{
    public static void Deconstruct(this LiteCommand cmd,
                                   out LiteParameter name,
                                   out LiteParameter mode,
                                   out LiteParameter mtime,
                                   out LiteParameter sz,
                                   out LiteParameter data)
    {
#if MICROSOFT_SQLITE
        name = cmd.Parameters.Add("@name", SqliteType.Text);
        mode = cmd.Parameters.Add("@mode", SqliteType.Integer);
        mtime = cmd.Parameters.Add("@mtime", SqliteType.Integer);
        sz = cmd.Parameters.Add("@sz", SqliteType.Integer);
        data = cmd.Parameters.Add("@data", SqliteType.Blob);
#elif OFFICIAL_SQLITE
        name = cmd.Parameters.Add("@name", DbType.String);
        mode = cmd.Parameters.Add("@mode", DbType.Int32);
        mtime = cmd.Parameters.Add("@mtime", DbType.Int64);
        sz = cmd.Parameters.Add("@sz", DbType.Int64);
        data = cmd.Parameters.Add("@data", DbType.Binary);
#endif
    }
}
