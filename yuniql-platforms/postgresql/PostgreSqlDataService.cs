﻿using System;
using System.Collections.Generic;
using System.Data;
using Yuniql.Extensibility;
using Npgsql;
using System.Collections;

namespace Yuniql.PostgreSql
{
    ///<inheritdoc/>
    public class PostgreSqlDataService : IDataService
    {
        private string _connectionString;
        private readonly ITraceService _traceService;

        ///<inheritdoc/>
        public PostgreSqlDataService(ITraceService traceService)
        {
            this._traceService = traceService;
        }

        ///<inheritdoc/>
        public bool IsTransactionalDdlSupported => true;

        ///<inheritdoc/>
        public bool IsSchemaSupported { get; } = true;

        ///<inheritdoc/>
        public bool IsBatchSqlSupported { get; } = false;

        ///<inheritdoc/>
        public bool IsUpsertSupported => false;

        ///<inheritdoc/>
        public string TableName { get; set; } = "__yuniqldbversion";

        ///<inheritdoc/>
        public string SchemaName { get; set; } = "public";

        ///<inheritdoc/>
        public void Initialize(string connectionString)
        {
            this._connectionString = connectionString;
        }

        ///<inheritdoc/>
        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        ///<inheritdoc/>
        public IDbConnection CreateMasterConnection()
        {
            var masterConnectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString);
            masterConnectionStringBuilder.Database = "postgres";

            return new NpgsqlConnection(masterConnectionStringBuilder.ConnectionString);
        }

        ///<inheritdoc/>
        public List<string> BreakStatements(string sqlStatementRaw)
        {
            return new List<string> { sqlStatementRaw };
        }

        ///<inheritdoc/>
        public ConnectionInfo GetConnectionInfo()
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_connectionString);
            return new ConnectionInfo { DataSource = connectionStringBuilder.Host, Database = connectionStringBuilder.Database };
        }

        ///<inheritdoc/>
        public string GetSqlForCheckIfDatabaseExists()
            => @"
SELECT 1 from pg_database WHERE datname = '${YUNIQL_DB_NAME}';
            ";

        ///<inheritdoc/>
        public string GetSqlForCreateDatabase()
            => @"
CREATE DATABASE ""${YUNIQL_DB_NAME}"";
            ";

        ///<inheritdoc/>
        public string GetSqlForCreateSchema()
            => @"
CREATE SCHEMA ""${YUNIQL_SCHEMA_NAME}"";
            ";

        ///<inheritdoc/>
        public string GetSqlForCheckIfDatabaseConfigured()
            => @"
SELECT 1 FROM pg_tables WHERE  tablename = '${YUNIQL_TABLE_NAME}';
            ";

        ///<inheritdoc/>
        public string GetSqlForConfigureDatabase()
            => @"
CREATE TABLE ${YUNIQL_SCHEMA_NAME}.${YUNIQL_TABLE_NAME}(
    sequence_id  SMALLSERIAL PRIMARY KEY NOT NULL,
    version VARCHAR(512) NOT NULL,
    applied_on_utc TIMESTAMP NOT NULL DEFAULT(current_timestamp AT TIME ZONE 'UTC'),
    applied_by_user VARCHAR(32) NOT NULL DEFAULT(user),
    applied_by_tool VARCHAR(32) NOT NULL,
    applied_by_tool_version VARCHAR(16) NOT NULL,
    status VARCHAR(32) NOT NULL,
    duration_ms INTEGER NOT NULL,
    failed_script_path VARCHAR(4000) NULL,
    failed_script_error VARCHAR(4000) NULL,
    additional_artifacts VARCHAR(4000) NULL,
    CONSTRAINT ix___yuniqldbversion UNIQUE(version)
);
            ";

        ///<inheritdoc/>
        public string GetSqlForGetCurrentVersion()
            => @"
SELECT version FROM ${YUNIQL_SCHEMA_NAME}.${YUNIQL_TABLE_NAME} WHERE status = 'Successful' ORDER BY sequence_id DESC LIMIT 1;
            ";

        ///<inheritdoc/>
        public string GetSqlForGetAllVersions()
            => @"
SELECT sequence_id, version, applied_on_utc, applied_by_user, applied_by_tool, applied_by_tool_version, status, duration_ms, failed_script_path, failed_script_error, additional_artifacts 
FROM ${YUNIQL_SCHEMA_NAME}.${YUNIQL_TABLE_NAME} ORDER BY version ASC;
            ";

        ///<inheritdoc/>
        public string GetSqlForInsertVersion()
            => @"
INSERT INTO ${YUNIQL_SCHEMA_NAME}.${YUNIQL_TABLE_NAME} (version, applied_by_tool, applied_by_tool_version, status, duration_ms, failed_script_path, failed_script_error, additional_artifacts) 
VALUES ('${YUNIQL_VERSION}', '${YUNIQL_APPLIED_BY_TOOL}', '${YUNIQL_APPLIED_BY_TOOL_VERSION}', '${YUNIQL_STATUS}', '${YUNIQL_DURATION_MS}', '${YUNIQL_FAILED_SCRIPT_PATH}', '${YUNIQL_FAILED_SCRIPT_ERROR}', '${YUNIQL_ADDITIONAL_ARTIFACTS}');
            ";

        ///<inheritdoc/>
        public string GetSqlForUpdateVersion()
            => @"
UPDATE ${YUNIQL_SCHEMA_NAME}.${YUNIQL_TABLE_NAME}
SET 
    applied_on_utc          =  current_timestamp AT TIME ZONE 'UTC',
    applied_by_user         =  user,
    applied_by_tool         = '${YUNIQL_APPLIED_BY_TOOL}', 
    applied_by_tool_version = '${YUNIQL_APPLIED_BY_TOOL_VERSION}', 
    status                  = '${YUNIQL_STATUS}', 
    duration_ms             = '${YUNIQL_DURATION_MS}', 
    failed_script_path      = '${YUNIQL_FAILED_SCRIPT_PATH}', 
    failed_script_error     = '${YUNIQL_FAILED_SCRIPT_ERROR}', 
    additional_artifacts    = '${YUNIQL_ADDITIONAL_ARTIFACTS}'
WHERE
    version                 = '${YUNIQL_VERSION}';
            ";

        ///<inheritdoc/>
        public string GetSqlForUpsertVersion()
            => throw new NotSupportedException("Not supported for the target platform");

        public bool UpdateDatabaseConfiguration(IDbConnection dbConnection, ITraceService traceService = null, string metaSchemaName = null, string metaTableName = null)
        {
            //no need to update tracking table as the structure has no been changed so far
            return false;
        }

        ///<inheritdoc/>
        public bool TryParseErrorFromException(Exception exception, out string result)
        {
            result = null;
            try
            {
                if (exception is PostgresException sqlException)
                {
                    var dataList = new List<string>();
                    foreach (DictionaryEntry item in sqlException.Data)
                        dataList.Add($"{item.Key}: {item.Value}");

                    result = $"(0x{sqlException.ErrorCode:X}) Error {sqlException.Message}. Exception data: {string.Join(", ", dataList)}";
                    return true;
                }
            }
            catch (Exception) { return false; }
            return false;
        }
    }
}
