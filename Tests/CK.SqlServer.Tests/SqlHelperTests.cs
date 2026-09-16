using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Data;
using Microsoft.Data.SqlClient;
using static CK.Testing.SqlServerTestHelper;

namespace CK.SqlServer.Tests;

[TestFixture]
public class SqlHelperTests
{
    [TestCase( 1, SqlDbType.Int, "1" )]
    [TestCase( "a\'b", SqlDbType.NVarChar, "N'a''b'" )]
    [TestCase( null, SqlDbType.NVarChar, "null" )]
    [TestCase( 0.0, SqlDbType.Float, "0" )]
    [TestCase( 0.012, SqlDbType.Float, "0.012" )]
    [TestCase( "special:DBNull.Value", SqlDbType.NVarChar, "null" )]
    [TestCase( "special:DateTime", SqlDbType.DateTime, "convert( DateTime, '2016-11-05T20:00:43', 126 )" )]
    [TestCase( "special:DateTime", SqlDbType.DateTime2, "'2016-11-05T20:00:43.0000000'" )]
    [TestCase( "special:Time", SqlDbType.Time, "'01:02:03.0123456'" )]
    [TestCase( "special:Guid", SqlDbType.UniqueIdentifier, "{63f7ff58-3101-4099-a18f-6d749b1748c8}" )]
    [TestCase( new byte[] { }, SqlDbType.VarBinary, "0x" )]
    [TestCase( new byte[] { 16 }, SqlDbType.VarBinary, "0x10" )]
    [TestCase( new byte[] { 0x10, 0xFF, 0x01 }, SqlDbType.VarBinary, "0x10FF01" )]
    public void SqlHelper_SqlValue_works( object value, SqlDbType dbType, string result )
    {
        Guid g = new Guid( "63F7FF58-3101-4099-A18F-6D749B1748C8" );
        if( value is string s )
        {
            if( s == "special:DBNull.Value" ) value = DBNull.Value;
            if( s == "special:DateTime" ) value = new DateTime( 2016, 11, 5, 20, 0, 43 );
            if( s == "special:Guid" ) value = g;
            if( s == "special:Time" ) value = new TimeSpan( 1, 2, 3 ).Add( TimeSpan.FromTicks( 123456 ) );
        }
        Assert.That( SqlHelper.SqlValue( value, dbType ), Is.EqualTo( result ) );
    }

    [SetUp]
    public void EnsureDatabase()
    {
        TestHelper.EnsureDatabase();
        TestHelper.ExecuteScripts( "if not exists(select 1 from sys.schemas where name = 'CK') exec('create schema CK');" );
    }

    [Test]
    public void SqlHelper_IsUtcMinValue_and_IsUtcMaxValue_works_for_datetime2()
    {
        using( var con = TestHelper.CreateOpenedConnection() )
        using( var cmd = new SqlCommand() )
        {
            cmd.Connection = con;

            cmd.CommandText = "select convert( datetime2(2), '00010101' )";
            object oMinDate = cmd.ExecuteScalar();
            oMinDate.ShouldBeOfType<DateTime>();
            oMinDate.ShouldBe( Util.UtcMinValue );
            SqlHelper.IsUtcMinValue( (DateTime)oMinDate ).ShouldBeTrue();

            cmd.CommandText = "select convert( datetime2(7), '99991231 23:59:59.9999999' )";
            object oMaxDate = cmd.ExecuteScalar();
            oMaxDate.ShouldBeOfType<DateTime>();
            oMaxDate.ShouldBe( Util.UtcMaxValue );
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 7 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 6 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 5 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 4 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 3 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 2 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 1 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 0 ).ShouldBeTrue();
            Should.Throw<ArgumentOutOfRangeException>( () => SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, -1 ) );
            Should.Throw<ArgumentOutOfRangeException>( () => SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate, 8 ) );

            cmd.CommandText = "select convert( datetime2(2), '99991231 23:59:59.9999999' )";
            object oMaxDate2 = cmd.ExecuteScalar();
            oMaxDate2.ShouldBeOfType<DateTime>();
            oMaxDate2.ShouldNotBe( Util.UtcMaxValue, "Unfortunately, 99991231 23:59:59.99 is NOT the same..." );
            oMaxDate2.ShouldBe( Util.UtcMaxValue.AddTicks( -99999 ) );
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 7 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 6 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 5 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 4 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 3 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 2 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 1 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate2, 0 ).ShouldBeTrue();

            cmd.CommandText = "select convert( datetime2(0), '99991231 23:59:59.9999999' )";
            object oMaxDate0 = cmd.ExecuteScalar();
            oMaxDate0.ShouldBeOfType<DateTime>();
            oMaxDate0.ShouldNotBe( Util.UtcMaxValue, "Unfortunately, 99991231 23:59:59.99 is NOT the same..." );
            oMaxDate0.ShouldBe( SqlHelper.UtcMaxValuePrecision0 );
            SqlHelper.UtcMaxValuePrecision0.ShouldBe( Util.UtcMaxValue.AddTicks( -9999999 ) );

            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0 ).ShouldBeTrue();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 7 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 6 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 5 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 4 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 3 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 2 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 1 ).ShouldBeFalse();
            SqlHelper.IsUtcMaxValue( (DateTime)oMaxDate0, 0 ).ShouldBeTrue();
        }
    }


    [Test]
    public void how_SqlHelper_RemoveSensitiveInformations_works()
    {
        // This is what is done: Password and User ID are removed.
        var s = "Server=.;Database=test;User ID=toto;Password=pwd";
        var c = new SqlConnectionStringBuilder( s );
        c["Password"] = null;
        c["User ID"] = null;
        c.ToString().ShouldBe( "Data Source=.;Initial Catalog=test" );

        // The SqlHelper.RemoveSensitiveInformations( string connectionString ) must be protected.
        Util.Invokable( () => new SqlConnectionStringBuilder( "something totally fucked." ) ).ShouldThrow<ArgumentException>();


        SqlHelper.RemoveSensitiveInformations( "Server=14.247.78.98; Pwd=pouf; uid=user; database=mydb" )
                 .ShouldBe( "Data Source=14.247.78.98;Initial Catalog=mydb" );

        SqlHelper.RemoveSensitiveInformations( null )
                 .ShouldStartWith( "ArgumentException: " );

        SqlHelper.RemoveSensitiveInformations( "something totally fucked" )
                 .ShouldStartWith( "ArgumentException: " );

    }

}
