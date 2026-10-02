using Shouldly;
using NUnit.Framework;
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;

namespace CK.SqlServer.Tests;

[TestFixture]
public class SqlConnectionExtensionTests
{
    [SetUp]
    public void EnsureDatabase()
    {
        TestHelper.EnsureDatabase();
        TestHelper.ExecuteScripts( "if not exists(select 1 from sys.schemas where name = 'CK') exec('create schema CK');" );
    }

    [Test]
    public void SqlConnection_EnsureOpen_extension()
    {
        using( var oCon = new SqlConnection( TestHelper.GetConnectionString() ) )
        {
            oCon.State.ShouldBe( ConnectionState.Closed );
            using( var disposer = oCon.EnsureOpen() )
            {
                disposer.ShouldNotBeNull();
                oCon.EnsureOpen().ShouldBeNull();
            }
            oCon.State.ShouldBe( ConnectionState.Closed );
        }

    }

    [Test]
    public async Task SqlConnection_EnsureOpen_extension_Async()
    {
        using( var oCon = new SqlConnection( TestHelper.GetConnectionString() ) )
        {
            oCon.State.ShouldBe( ConnectionState.Closed );
            using( var disposer = await oCon.EnsureOpenAsync() )
            {
                disposer.ShouldNotBeNull();
                (await oCon.EnsureOpenAsync()).ShouldBeNull();
            }
            oCon.State.ShouldBe( ConnectionState.Closed );
        }
    }
}
