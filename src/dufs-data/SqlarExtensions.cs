using System.Data;
using System.Data.SQLite;

namespace dufs_data;

public static class SqlarExtensions
{
    public static void Deconstruct(this SQLiteCommand cmd,
                                   out SQLiteParameter name,
                                   out SQLiteParameter mode,
                                   out SQLiteParameter mtime,
                                   out SQLiteParameter sz,
                                   out SQLiteParameter data)
    {
        name = cmd.Parameters.Add("@name", DbType.String);
        mode = cmd.Parameters.Add("@mode", DbType.Int32);
        mtime = cmd.Parameters.Add("@mtime", DbType.Int64);
        sz = cmd.Parameters.Add("@sz", DbType.Int64);
        data = cmd.Parameters.Add("@data", DbType.Binary);
    }
}
