# Microsoft SQL Server to MySQL
This script is used to generate mysql script(table and data) from microsoft sql server

## Install
This is a .NET Core Global Tool and .NET Core SDK is required https://dotnet.microsoft.com/download
```
dotnet tool install --global mssql2mysql
```
## Generate MySQL Script
Generate mysql script with MSSQL connection string
```
mssql2mysql -c "Server=(local);Database=ZKEACMS;User Id=sa;Password=sa;MultipleActiveResultSets=true;"
```

Use `-f` to specify the output filename
```
mssql2mysql -f dump.sql -c "Server=(local);Database=ZKEACMS;User Id=sa;Password=sa;"
```

Use `-s` if you want to include create schema
```
mssql2mysql -s ZKEACMS -c "Server=(local);Database=ZKEACMS;User Id=sa;Password=sa;"
```
