using System;
using System.Linq;
using System.Data.Common;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Globalization;
using System.IO;
using McMaster.Extensions.CommandLineUtils;

namespace mssql2mysql
{
    //dotnet pack -o ./
    [Command(Description = "Generate mysql script from SQL Server")]
    class Program
    {
        static void Main(string[] args)
        {
            CommandLineApplication.Execute<Program>(args);
        }
        private Type StringType = typeof(string);
        private Type DateTimeType = typeof(DateTime);
        private Type BoolType = typeof(bool);

        [Option(Description = "SQL Server ConnectionString")]
        public string ConnectionString { get; set; }

        [Option(Description = "MySQL Script file name, default: dump.sql")]
        public string FileName { get; set; }

        [Option(Description = "MySQL Schema")]
        public string Schema { get; set; }
        private int OnExecute()
        {
            if (ConnectionString == null)
            {
                return 1;
            }
            FileName = FileName ?? "dump.sql";
            using (FileStream fileStream = new FileStream(FileName, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    writer.WriteLine("/* https://github.com/SeriaWei/mssql2mysql */");
                    writer.WriteLine();
                    writer.WriteLine();
                    if (Schema != null)
                    {
                        writer.WriteLine($"CREATE DATABASE  IF NOT EXISTS `{Schema}` /*!40100 DEFAULT CHARACTER SET latin1 */;");
                        writer.WriteLine($"USE `{Schema}`;");
                        writer.WriteLine();
                    }
                    writer.WriteLine("SET FOREIGN_KEY_CHECKS=0;");
                    foreach (var item in GetTalbes().ToArray())
                    {
                        Console.WriteLine("Generating: " + item);
                        writer.WriteLine(GetTableSchema(item));
                        writer.WriteLine();
                        writer.WriteLine(GetData(item));
                    }
                    writer.WriteLine("SET FOREIGN_KEY_CHECKS=1;");
                }
            }
            GetDbConnection().Close();
            Console.WriteLine("Save to: " + FileName);
            return 0;
        }
        private SqlConnection SqlConnection;
        private SqlConnection GetDbConnection()
        {
            if (SqlConnection == null)
            {
                SqlConnection = new SqlConnection(ConnectionString);
                SqlConnection.Open();
            }
            return SqlConnection;
        }

        private IEnumerable<string> GetTalbes()
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
        private string GetTableSchema(string tableName)
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
                    StringBuilder columnBuilder = new StringBuilder();
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
                        if (columnBuilder.Length > 0)
                        {
                            columnBuilder.AppendLine(",");
                        }
                        columnBuilder.Append($"\t`{reader.GetString(3)}` {dbType} {nullable}");
                    }
                    reader.Close();
                    string primaryKey = GetPrimaryKey(tableName);
                    if (primaryKey != null)
                    {
                        columnBuilder.AppendLine(",");
                        columnBuilder.Append(primaryKey);
                    }
                    string foreignKey = GetForeignKey(tableName);
                    if (foreignKey != null)
                    {
                        columnBuilder.AppendLine(",");
                        columnBuilder.Append(foreignKey);
                    }
                    columnBuilder.AppendLine();
                    columnBuilder.Append(");");
                    builder.Append(columnBuilder.ToString());
                }
            }
            return builder.ToString();
        }
        private string GetDataType(string type, int size, int precision, int? scale)
        {
            if (type == "nvarchar")
            {
                return $"VARCHAR({size / 2}) CHARACTER SET utf8mb4";
            }
            else if (type == "varchar")
            {
                return $"VARCHAR({size}) CHARACTER SET utf8mb4";
            }
            else if (type == "text" || type == "ntext")
            {
                return "LONGTEXT CHARACTER SET utf8mb4";
            }
            else if (type == "char" || type == "nchar")
            {
                return $"CHAR({size}) CHARACTER SET utf8mb4";
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
                return "LONGBLOB";
            }
            else if (type == "money" || type == "smallmoney" || type == "decimal" || type == "numeric")
            {
                return $"DECIMAL ({precision},{scale})";
            }
            else if (type == "float" || type == "real")
            {
                return "FLOAT";
            }
            else if (type == "bit")
            {
                return "TINYINT(1)";
            }
            else if (type == "int identity")
            {
                return "INT AUTO_INCREMENT";
            }
            return type.ToUpper();
        }
        private string GetPrimaryKey(string tableName)
        {
            HashSet<string> primaryKeys = new HashSet<string>();
            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = $"EXEC sys.sp_indexes_rowset '{tableName}'";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        if (reader.GetBoolean(6))
                        {
                            primaryKeys.Add($"`{reader.GetString(17)}`");
                        }
                    }
                }
                reader.Close();
            }
            if (primaryKeys.Count == 0) return null;

            return $"\tPRIMARY KEY ({string.Join(",", primaryKeys)})";
        }
        private string GetForeignKey(string tableName)
        {
            StringBuilder foreignKeys = new StringBuilder();
            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = $"EXEC sys.sp_fkeys @fktable_name='{tableName}'";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        if (foreignKeys.Length > 0)
                        {
                            foreignKeys.AppendLine(",");
                        }
                        foreignKeys.AppendLine($"\tKEY `{reader.GetString(11)}` (`{reader.GetString(7)}`),");
                        foreignKeys.Append($"\tCONSTRAINT `{reader.GetString(11)}` FOREIGN KEY (`{reader.GetString(7)}`) REFERENCES `{reader.GetString(2)}` (`{reader.GetString(3)}`) ON DELETE NO ACTION ON UPDATE NO ACTION");
                    }
                }
                reader.Close();
            }
            if (foreignKeys.Length == 0) return null;
            return foreignKeys.ToString();
        }
        private string GetData(string tableName)
        {
            StringBuilder builder = new StringBuilder();
            List<string> columns = new List<string>();
            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = "EXEC sys.sp_columns '" + tableName + "';";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        columns.Add($"[{reader.GetString(3)}]");
                    }
                }
                reader.Close();
            }

            using (DbCommand dbCommand = GetDbConnection().CreateCommand())
            {
                dbCommand.CommandText = $"SELECT {string.Join(",", columns)} FROM [{tableName}];";
                DbDataReader reader = dbCommand.ExecuteReader();
                if (reader.HasRows)
                {
                    builder.AppendLine($"/*!40000 ALTER TABLE `{tableName}` DISABLE KEYS */;");
                    builder.AppendLine($"INSERT INTO `{tableName}` VALUES");
                    int rowIndex = 0;
                    while (reader.Read())
                    {
                        List<string> values = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            values.Add(FormatValue(reader.GetValue(i)).ToString());
                        }
                        if (rowIndex > 0)
                        {
                            builder.AppendLine(",");
                        }
                        builder.Append($"({string.Join(',', values)})");
                        rowIndex++;
                    }
                    builder.AppendLine(";");
                    builder.AppendLine($"/*!40000 ALTER TABLE `{tableName}` ENABLE KEYS */;");
                }
                reader.Close();
            }
            return builder.ToString();
        }
        private object FormatValue(object value)
        {
            if (value == System.DBNull.Value)
            {
                return "NULL";
            }
            Type valueType = value.GetType();
            if (valueType == StringType)
            {
                return $"'{value.ToString().Replace("'", "''")}'";
            }
            else if (valueType == DateTimeType)
            {
                return $"'{Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}'";
            }
            else if (valueType == BoolType)
            {
                return Convert.ToBoolean(value) ? 1 : 0;
            }
            return value;
        }
    }
}
