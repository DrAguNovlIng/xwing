namespace Xwing.Tests;

[CollectionDefinition("SQL Server Integration Tests")]
public sealed class SqlServerCollectionDefinition : ICollectionFixture<SqlServerFixture>;
