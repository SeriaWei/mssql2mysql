# Microsoft SQL Server to MySQL
This script is used to generate mysql script from microsoft sql server

You should install the tool at first
```
dotnet tool install --global mssql2mysql
```

Them you can generate mysql script
```
mssql2mysql -c "Server=(local);Database=ZKEACMS;User Id=sa;Password=sa;MultipleActiveResultSets=true;"
```
