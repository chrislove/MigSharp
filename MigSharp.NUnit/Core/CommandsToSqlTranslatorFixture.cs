﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FakeItEasy;
using JetBrains.Annotations;
using MigSharp.Core;
using MigSharp.Core.Entities;
using MigSharp.Providers;
using NUnit.Framework;

namespace MigSharp.NUnit.Core
{
    [TestFixture, Category("smoke")]
    internal class CommandsToSqlTranslatorFixture
    {
        private static readonly Dictionary<string, Tuple<Action<IDatabase>, Action<IProvider>>> Cases = new Dictionary<string, Tuple<Action<IDatabase>, Action<IProvider>>>
            {
                {
                    "CreateTableInDefaultSchema",
                    new Tuple<Action<IDatabase>, Action<IProvider>>(
                        db => db.CreateTable("Table")
                                .WithPrimaryKeyColumn("Id", DbType.Int32),
                        provider => A.CallTo(() => provider.CreateTable(A<TableName>.That.Matches(n => n.Name == "Table" && n.Schema == null), A<IEnumerable<CreatedColumn>>._, A<string>._)).MustHaveHappened())
                },
                {
                    "CreateTableInCustomSchema", // make sure that the schema name is passed to the provider
                    new Tuple<Action<IDatabase>, Action<IProvider>>(
                        db => db.Schemata["Schema"]
                                  .CreateTable("Table")
                                  .WithPrimaryKeyColumn("Id", DbType.Int32),
                        provider => A.CallTo(() => provider.CreateTable(A<TableName>.That.Matches(n => n.Name == "Table" && n.Schema == "Schema"), A<IEnumerable<CreatedColumn>>._, A<string>._)).MustHaveHappened())
                },
                {
                    "StoringVarialbesShouldNotMakeDifference",
                    new Tuple<Action<IDatabase>, Action<IProvider>>(
                        db =>
                            {
                                IExistingTable table1 = db.Tables["Table1"];
                                table1.AddNotNullableColumn("Column1", DbType.Int32);
                                db.Tables["Table2"].AddNotNullableColumn("Column1", DbType.Int32);
                                table1.AddNotNullableColumn("Column2", DbType.Int32);
                            },
                        provider =>
                            {
                                A.CallTo(() => provider.AddColumn(A<TableName>.That.Matches(t => t.Name == "Table1"), A<Column>.That.Matches(c => c.Name == "Column1"))).MustHaveHappened()
                                .Then(A.CallTo(() => provider.AddColumn(A<TableName>.That.Matches(t => t.Name == "Table2"), A<Column>.That.Matches(c => c.Name == "Column1"))).MustHaveHappened())
                                .Then(A.CallTo(() => provider.AddColumn(A<TableName>.That.Matches(t => t.Name == "Table1"), A<Column>.That.Matches(c => c.Name == "Column2"))).MustHaveHappened());
                            })
                },
            };

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [TestCaseSource("GetCases")]
        public void TestProviderArgumentsAreCorrect(Action<IDatabase> arrangeDatabase, Action<IProvider> assertProvider)
        {
            // arrange
            var provider = A.Fake<IProvider>();
            CommandsToSqlTranslator translator = new CommandsToSqlTranslator(provider);
            var context = A.Fake<IRuntimeContext>();
            var database = new Database(context);
            arrangeDatabase(database);

            // act
            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            translator.TranslateToSql(database, context).ToList();

            // assert
            assertProvider(provider);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [UsedImplicitly]
        private static IEnumerable<TestCaseData> GetCases()
        {
            return Cases.Select(c =>
                {
                    var data = new TestCaseData(c.Value.Item1, c.Value.Item2);
                    data.SetName(c.Key);
                    return data;
                });
        }
    }
}