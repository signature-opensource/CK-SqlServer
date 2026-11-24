Types and helpers to call SQL Server: the call contexts that cache and control the database connections, a detailed
exception, and data reading helpers.

The main types are `ISqlCallContext` and its implementation `SqlStandardCallContext`, `ISqlConnectionController`,
`SqlDetailedException` (that shows the actual parameter values), `SqlDataRow` and `SqlHelper`.
