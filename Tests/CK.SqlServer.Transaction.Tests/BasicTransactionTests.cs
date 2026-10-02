using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;
using CK.Core;

namespace CK.SqlServer.Transaction.Tests;

[TestFixture]
public class BasicTransactionTests
{
    [SetUp]
    public void EnsureDatabase()
    {
        TestHelper.EnsureDatabase();
    }

    [Test]
    public void opening_twice_a_connection_is_an_error_but_it_can_be_closed_multiple_times()
    {
        using( var c = new SqlConnection( TestHelper.GetConnectionString() ) )
        {
            Util.Invokable( c.Open ).ShouldNotThrow();
            Util.Invokable( c.Open ).ShouldThrow<InvalidOperationException>();
            Util.Invokable( c.Close ).ShouldNotThrow();
            Util.Invokable( c.Close ).ShouldNotThrow();
        }
    }

    [Test]
    public void begin_transaction_on_closed_connection_is_an_invalid_operation()
    {
        using( var c = new SqlConnection( TestHelper.GetConnectionString() ) )
        {
            Util.Invokable( c.BeginTransaction ).ShouldThrow<InvalidOperationException>();
        }
    }

    [Test]
    [SetUICulture( "en-US" )]
    public void nested_transactions_are_not_supported()
    {
        using( var c = TestHelper.CreateOpenedConnection() )
        {
            using( var t1 = c.BeginTransaction() )
            {
                Util.Invokable( c.BeginTransaction ).ShouldThrow<InvalidOperationException>();
            }
        }
    }

    [Test]
    [SetUICulture( "en-US" )]
    public void command_MUST_be_associated_to_the_connection_transaction()
    {
        using( var c = TestHelper.CreateOpenedConnection() )
        using( var cmd = new SqlCommand( "select 1;" ) )
        {
            var t = c.BeginTransaction();
            cmd.Connection = c;
            Util.Invokable( cmd.ExecuteScalar ).ShouldThrow<InvalidOperationException>();
        }
    }

    [Test]
    [SetUICulture( "en-US" )]
    public void connection_and_transaction_must_exactly_match()
    {
        using( var c1 = TestHelper.CreateOpenedConnection() )
        using( var c2 = TestHelper.CreateOpenedConnection() )
        using( var cmd = new SqlCommand( "select 1;" ) )
        {
            var t1 = c1.BeginTransaction();
            cmd.Connection = c2;
            cmd.Transaction = t1;
            Util.Invokable( cmd.ExecuteScalar ).ShouldThrow<InvalidOperationException>();
        }
    }

}
