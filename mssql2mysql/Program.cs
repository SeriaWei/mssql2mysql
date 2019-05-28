using System;
using System.Linq;
using System.Data.Common;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Data;

namespace mssql2mysql
{
    class Program
    {
        static void Main(string[] args)
        {
            foreach (var item in GetTalbes().ToArray())
            {
                Console.WriteLine(item);
                string s = GetTableSchema(item);
            }
        }
        static SqlConnection SqlConnection;
        static SqlConnection GetDbConnection()
        {
            if (SqlConnection == null)
            {
                SqlConnection = new SqlConnection("Data Source=WAYNEWEI-SZ;Initial Catalog=KingstonSiteDB_Admin;User Id=sa;Password=sa;");
                SqlConnection.Open();
            }
            return SqlConnection;
        }

        static IEnumerable<string> GetTalbes()
        {
            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = "EXEC sys.sp_tables NULL,'dbo',NULL,'''TABLE'''";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        yield return reader.GetString(2);
                    }
                }
                reader.Close();
            }
        }
        static string GetTableSchema(string tableName)
        {
            StringBuilder builder = new StringBuilder();
            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = "EXEC sys.sp_columns '" + tableName + "';";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    builder.AppendLine("DROP TABLE IF EXISTS `" + tableName + "`;");
                    builder.AppendLine("CREATE TABLE `" + tableName + "` (");
                    while (reader.Read())
                    {
                        string nullable;
                        if (reader.GetInt16(10) == 1)
                        {
                            nullable = " NULL";
                        }
                        else { nullable = " NOT NULL"; }
                        int size = reader.GetInt32(7);
                        int precision = reader.GetInt32(6);
                        int? scale = null;
                        if (reader.GetValue(8) != System.DBNull.Value)
                        {
                            scale = reader.GetInt16(8);
                        }
                        string dbType = GetDataType(reader.GetString(5), size, precision, scale);
                        builder.AppendLine($"\t`{reader.GetString(3)}` {dbType} {nullable},");
                    }
                    builder.AppendLine(");");
                }
                reader.Close();
            }
            return builder.ToString();
        }
        static string GetDataType(string type, int size, int precision, int? scale)
        {
            if (type == "varchar" || type == "nvarchar")
            {
                if (size > 255)
                    return "TEXT";
                else
                    return $"VARCHAR({size})";
            }
            else if (type == "text" || type == "ntext")
            {
                return "TEXT";
            }
            else if (type == "char" || type == "nchar")
            {
                return $"CHAR({size})";
            }
            else if (type == "int" || type == "int8")
            {
                return "INT";
            }
            else if (type == "datetime" || type == "smalldatetime")
            {
                return "DATETIME";
            }
            else if (type == "image" || type == "binary" || type == "varbinary")
            {
                return "BLOB";
            }
            else if (type == "money" || type == "smallmoney" || type == "decimal" || type == "numeric")
            {
                return $"DECIMAL ({precision},{scale})";
            }
            else if (type == "float" || type == "real")
            {
                return "FLOAT";
            }
            else if (type == "float" || type == "real")
            {
                return "FLOAT";
            }
            else if (type == "int identity")
            {
                return "INT AUTO_INCREMENT";
            }
            return type.ToUpper();
        }
    }
}
