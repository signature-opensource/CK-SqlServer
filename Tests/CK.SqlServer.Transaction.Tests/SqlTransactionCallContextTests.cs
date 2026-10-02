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
public class SqlTransactionCallContextTests
{
    [SetUp]
    public void EnsureDatabase()
    {
        TestHelper.EnsureDatabase();
    }

    [Test]
    public void transaction_controller_implicitly_opens_the_connection()
    {
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            BeginTranAndCommit( controller );
            controller.Connection.State.ShouldBe( ConnectionState.Closed );

            // Explicit openening.
            using( controller.ExplicitOpen() )
            {
                BeginTranAndCommit( controller );
                controller.Connection.State.ShouldBe( ConnectionState.Open );
            }
            controller.Connection.State.ShouldBe( ConnectionState.Closed );
        }

        static void BeginTranAndCommit( ISqlConnectionTransactionController controller )
        {
            ISqlTransaction tran = controller.BeginTransaction();
            controller.Connection.State.ShouldBe( ConnectionState.Open );
            tran.IsNested.ShouldBeFalse();
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            tran.Commit();
            tran.Status.ShouldBe( SqlTransactionStatus.Committed );
            Util.Invokable( tran.Dispose ).ShouldNotThrow();
        }
    }

    [Test]
    public void basic_transaction_test()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            var messageId = DoCommitTest( controller, 0, 0 );
            messageId.ShouldBe( 1, "One message has been created." );
            messageId = DoRollbackAllAndDisposeTest( controller, 0, messageId );
            messageId.ShouldBe( 3, "Two messages have been created and canceled." );
        }
    }

    [Test]
    public void basic_nested_transaction_test_with_ExplicitOpen_and_continue_on_the_same_connection()
    {
        // create table dbo.tTranTest( Id int identity(1,1) primary key, Msg varchar(50) not null );
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            // The connection is maintained opened.
            controller.ExplicitOpen();
            DoCommitTest( controller, tranCount: 0, messageId: 0 ).ShouldBe( 1, "messageId from 0 to 1." );
            using( var tran = controller.BeginTransaction() )
            {
                DoCommitTest( controller, 1, 1 ).ShouldBe( 2, "messageId from 1 to 2." );
                DoCommitTest( controller, 1, 2 ).ShouldBe( 3, "messageId from 2 to 3." );
                using( var tran2 = controller.BeginTransaction() )
                {
                    DoCommitTest( controller, 2, 3 ).ShouldBe( 4, "messageId from 3 to 4." );
                    DoRollbackAllAndDisposeTest( controller, 2, 4 ).ShouldBe( 6, "messageId from 4 to 6." );
                    controller.TransactionCount.ShouldBe( 0 );
                    tran2.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                }
                tran.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
            }
            controller.Connection.State.ShouldBe( ConnectionState.Open );
            DoCommitTest( controller, tranCount: 0, messageId: 6 )
                .ShouldBe( 7, "messageId from 6 to 7 (The identity is out of transaction!)." );
            ReadMessage( controller, 1 ).ShouldNotBeNull( "Done before the transaction." );
            ReadMessage( controller, 2 ).ShouldBeNull( "in rollbacked transaction." );
            ReadMessage( controller, 3 ).ShouldBeNull();
            ReadMessage( controller, 4 ).ShouldBeNull();
            ReadMessage( controller, 5 ).ShouldBeNull();
            ReadMessage( controller, 6 ).ShouldBeNull();
            ReadMessage( controller, 7 ).ShouldNotBeNull( "Done after the transaction." );
        }
    }

    [Test]
    public void basic_nested_transaction_test_with_ExplicitOpen()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            var open1 = controller.ExplicitOpen();
            DoCommitTest( controller, 0, 0 ).ShouldBe( 1, "messageId from 0 to 1." );
            using( var tran = controller.BeginTransaction() )
            {
                DoCommitTest( controller, 1, 1 ).ShouldBe( 2, "messageId from 1 to 2." );
                // Since a transaction has been opened, we can dispose the explicit opening:
                // The SqlTransactionCallContext.BeginTransaction has implicitly opened the connection.
                open1.Dispose();
                DoCommitTest( controller, 1, 2 ).ShouldBe( 3, "messageId from 2 to 3." );
                using( var tran2 = controller.BeginTransaction() )
                {
                    DoCommitTest( controller, 2, 3 ).ShouldBe( 4, "messageId from 3 to 4." );
                    DoRollbackAllAndDisposeTest( controller, 2, 4 ).ShouldBe( 6, "messageId from 4 to 6." );
                    controller.TransactionCount.ShouldBe( 0 );
                    tran2.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                }
                tran.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
            }
        }
    }

    [Test]
    public void basic_nested_transaction_test()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            DoCommitTest( controller, 0, 0 ).ShouldBe( 1, "messageId from 0 to 1." );
            using( var tran = controller.BeginTransaction() )
            {
                DoCommitTest( controller, 1, 1 ).ShouldBe( 2, "messageId from 1 to 2." );
                DoCommitTest( controller, 1, 2 ).ShouldBe( 3, "messageId from 2 to 3." );
                using( var tran2 = controller.BeginTransaction() )
                {
                    DoCommitTest( controller, 2, 3 ).ShouldBe( 4, "messageId from 3 to 4." );
                    DoRollbackAllAndDisposeTest( controller, 2, 4 ).ShouldBe( 6, "messageId from 4 to 6." );
                    controller.TransactionCount.ShouldBe( 0 );
                    tran2.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                }
                tran.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
            }
        }
    }

    [Test]
    public void nested_transaction_with_levels()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            controller.Connection.State.ShouldBe( ConnectionState.Closed );
            controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Unspecified );
            using( var tran0 = controller.BeginTransaction() )
            {
                controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadCommitted );
                DoCommitTest( controller, tranCount: 1, messageId: 0 ).ShouldBe( 1, "messageId from 0 to 1." );
                using( var tran1 = controller.BeginTransaction( IsolationLevel.Serializable ) )
                {
                    controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Serializable );
                    DoCommitTest( controller, tranCount: 2, messageId: 1 ).ShouldBe( 2, "messageId from 1 to 2." );
                    controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Serializable );

                    using( var tran2 = controller.BeginTransaction( IsolationLevel.ReadUncommitted ) )
                    {
                        DoCommitTest( controller, tranCount: 3, messageId: 2 ).ShouldBe( 3, "messageId from 2 to 3." );
                        controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadUncommitted );
                        DoRollbackAllAndDisposeTest( controller, tranCount: 3, messageId: 3 ).ShouldBe( 5, "messageId from 3 to 5." );
                        controller.TransactionCount.ShouldBe( 0 );
                        tran2.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                    }
                    tran1.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                }
                tran0.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
            }
        }
    }

    [Test]
    public async Task nested_transaction_with_levels_asynchronous_Async()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            controller.Connection.State.ShouldBe( ConnectionState.Closed );
            controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Unspecified );
            using( var tran0 = controller.BeginTransaction() )
            {
                controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadCommitted );
                (await DoCommitTestAsync( controller, tranCount: 1, messageId: 0 )).ShouldBe( 1, "messageId from 0 to 1." );
                using( var tran1 = controller.BeginTransaction( IsolationLevel.Serializable ) )
                {
                    controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Serializable );
                    (await DoCommitTestAsync( controller, tranCount: 2, messageId: 1 )).ShouldBe( 2, "messageId from 1 to 2." );
                    controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Serializable );

                    using( var tran2 = controller.BeginTransaction( IsolationLevel.ReadUncommitted ) )
                    {
                        (await DoCommitTestAsync( controller, tranCount: 3, messageId: 2 )).ShouldBe( 3, "messageId from 2 to 3." );
                        controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadUncommitted );
                        (await DoRollbackAllAndDisposeTestAsync( controller, tranCount: 3, messageId: 3 )).ShouldBe( 5, "messageId from 3 to 5." );
                        controller.TransactionCount.ShouldBe( 0 );
                        tran2.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                    }
                    tran1.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
                }
                tran0.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
            }
        }
    }


    [Test]
    public void default_isolation_level_ReadCommitted_is_restored()
    {
        ResetTranTestTable();
        using( var ctx = new SqlTransactionCallContext( TestHelper.Monitor ) )
        {
            var controller = ctx.GetConnectionController( TestHelper.GetConnectionString() );
            controller.Connection.State.ShouldBe( ConnectionState.Closed );
            controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Unspecified );
            using( controller.ExplicitOpen() )
            {
                controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadCommitted );
                using( var tran = controller.BeginTransaction( IsolationLevel.Serializable ) )
                {
                    controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Serializable );
                }
                controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.ReadCommitted );
            }
            controller.GetCurrentIsolationLevel().ShouldBe( IsolationLevel.Unspecified );
        }
    }

    static int DoCommitTest( ISqlConnectionTransactionController controller, int tranCount, int messageId )
    {
        var message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( tranCount + 1 );
            AddMessage( controller, message ).ShouldBe( ++messageId );
            ReadMessage( controller, messageId ).ShouldBe( message );
            tran.Commit();
            controller.TransactionCount.ShouldBe( tranCount );
            tran.Status.ShouldBe( SqlTransactionStatus.Committed );
        }
        ReadMessage( controller, messageId ).ShouldBe( message );
        return messageId;
    }

    static async Task<int> DoCommitTestAsync( ISqlConnectionTransactionController controller, int tranCount, int messageId )
    {
        var message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( tranCount + 1 );
            (await AddMessageAsync( controller, message )).ShouldBe( ++messageId );
            (await ReadMessageAsync( controller, messageId )).ShouldBe( message );
            tran.Commit();
            controller.TransactionCount.ShouldBe( tranCount );
            tran.Status.ShouldBe( SqlTransactionStatus.Committed );
        }
        (await ReadMessageAsync( controller, messageId )).ShouldBe( message );
        return messageId;
    }

    static int DoRollbackAllAndDisposeTest( ISqlConnectionTransactionController controller, int tranCount, int messageId )
    {
        var message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( tranCount + 1 );
            AddMessage( controller, message ).ShouldBe( ++messageId );
            ReadMessage( controller, messageId ).ShouldBe( message );
            tran.RollbackAll();
            controller.TransactionCount.ShouldBe( 0 );
            tran.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
        }
        ReadMessage( controller, 2 ).ShouldBeNull();
        message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( 1 );
            AddMessage( controller, message ).ShouldBe( ++messageId );
            ReadMessage( controller, messageId ).ShouldBe( message );
        }
        controller.TransactionCount.ShouldBe( 0 );
        ReadMessage( controller, messageId ).ShouldBeNull();
        return messageId;
    }

    static async Task<int> DoRollbackAllAndDisposeTestAsync( ISqlConnectionTransactionController controller, int tranCount, int messageId )
    {
        var message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( tranCount + 1 );
            (await AddMessageAsync( controller, message )).ShouldBe( ++messageId );
            (await ReadMessageAsync( controller, messageId )).ShouldBe( message );
            tran.RollbackAll();
            controller.TransactionCount.ShouldBe( 0 );
            tran.Status.ShouldBe( SqlTransactionStatus.Rollbacked );
        }
        (await ReadMessageAsync( controller, 2 )).ShouldBeNull();
        message = Guid.NewGuid().ToString();
        using( var tran = controller.BeginTransaction() )
        {
            tran.Status.ShouldBe( SqlTransactionStatus.Opened );
            controller.TransactionCount.ShouldBe( 1 );
            (await AddMessageAsync( controller, message )).ShouldBe( ++messageId );
            (await ReadMessageAsync( controller, messageId )).ShouldBe( message );
        }
        controller.TransactionCount.ShouldBe( 0 );
        (await ReadMessageAsync( controller, messageId )).ShouldBeNull();
        return messageId;
    }


    static readonly SqlCommand _resetCommand;
    static readonly SqlCommand _addMessageCommand;
    static readonly SqlCommand _readMessageCommand;

    static SqlTransactionCallContextTests()
    {
        _resetCommand = new SqlCommand( $"if object_id('dbo.tTranTest') is not null drop table dbo.tTranTest; create table dbo.tTranTest( Id int identity(1,1) primary key, Msg varchar(50) not null );" );
        _addMessageCommand = new SqlCommand( $"insert into dbo.tTranTest( Msg ) values( @Msg ); select cast(SCOPE_IDENTITY() as int);" );
        _addMessageCommand.Parameters.Add( "@Msg", SqlDbType.VarChar, 50 );
        _readMessageCommand = new SqlCommand( "select Msg from dbo.tTranTest where Id = @Id" );
        _readMessageCommand.Parameters.Add( "@Id", SqlDbType.Int );
    }

    static void ResetTranTestTable()
    {
        using( var con = TestHelper.CreateOpenedConnection() )
        {
            _resetCommand.Connection = con;
            _resetCommand.ExecuteNonQuery();
            _resetCommand.Connection = null;
        }
    }

    static int AddMessage( ISqlConnectionController c, string msg )
    {
        _addMessageCommand.Parameters[0].Value = msg;
        return (int)c.ExecuteScalar( _addMessageCommand );
    }

    static async Task<int> AddMessageAsync( ISqlConnectionController c, string msg )
    {
        _addMessageCommand.Parameters[0].Value = msg;
        return (int)await c.ExecuteScalarAsync( _addMessageCommand );
    }

    static string ReadMessage( ISqlConnectionController c, int id )
    {
        _readMessageCommand.Parameters[0].Value = id;
        return (string)c.ExecuteScalar( _readMessageCommand );
    }
    static async Task<string> ReadMessageAsync( ISqlConnectionController c, int id )
    {
        _readMessageCommand.Parameters[0].Value = id;
        return (string)await c.ExecuteScalarAsync( _readMessageCommand );
    }


}
