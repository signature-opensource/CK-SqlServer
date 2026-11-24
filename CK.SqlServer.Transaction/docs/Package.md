Transaction support for the call contexts of CK.SqlServer: `ISqlTransactionCallContext` and its implementation
`SqlTransactionCallContext`, whose connection controllers can begin nested transactions.

`ISqlConnectionTransactionController.BeginTransaction` returns an `ISqlTransaction` that is committed or rolled back
(`SqlTransactionStatus`).
