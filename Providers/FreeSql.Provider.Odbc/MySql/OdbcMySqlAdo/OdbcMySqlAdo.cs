﻿using FreeSql.Internal;
using FreeSql.Internal.Model;
using SafeObjectPool;
using System;
using System.Collections;
using System.Data.Common;
using System.Data.Odbc;
using System.Text;
using System.Threading;

namespace FreeSql.Odbc.MySql
{
    class OdbcMySqlAdo : FreeSql.Internal.CommonProvider.AdoProvider
    {

        public OdbcMySqlAdo() : base(DataType.OdbcMySql) { }
        public OdbcMySqlAdo(CommonUtils util, string masterConnectionString, string[] slaveConnectionStrings, Func<DbConnection> connectionFactory) : base(DataType.OdbcMySql)
        {
            base._util = util;
            if (connectionFactory != null)
            {
                MasterPool = new FreeSql.Internal.CommonProvider.DbConnectionPool(DataType.OdbcMySql, connectionFactory);
                return;
            }
            if (!string.IsNullOrEmpty(masterConnectionString))
                MasterPool = new OdbcMySqlConnectionPool("主库", masterConnectionString, null, null);
            if (slaveConnectionStrings != null)
            {
                foreach (var slaveConnectionString in slaveConnectionStrings)
                {
                    var slavePool = new OdbcMySqlConnectionPool($"从库{SlavePools.Count + 1}", slaveConnectionString, () => Interlocked.Decrement(ref slaveUnavailables), () => Interlocked.Increment(ref slaveUnavailables));
                    SlavePools.Add(slavePool);
                }
            }
        }
        static DateTime dt1970 = new DateTime(1970, 1, 1);
        public override object AddslashesProcessParam(object param, Type mapType, ColumnInfo mapColumn)
        {
            if (param == null) return "NULL";
            if (mapType != null && mapType != param.GetType() && (param is IEnumerable == false || mapType.IsArrayOrList()))
                param = Utils.GetDataReaderValue(mapType, param);
            if (param is bool || param is bool?)
                return (bool)param ? 1 : 0;
            else if (param is string || param is char)
                return string.Concat("'", param.ToString().Replace("'", "''"), "'");
            else if (param is Enum)
                return string.Concat("'", param.ToString().Replace("'", "''"), "'"); //((Enum)val).ToInt64();
            else if (decimal.TryParse(string.Concat(param), out var trydec))
                return param;
            else if (param is DateTime || param is DateTime?)
                return string.Concat("'", ((DateTime)param).ToString("yyyy-MM-dd HH:mm:ss.fff"), "'");
            else if (param is TimeSpan || param is TimeSpan?)
                return ((TimeSpan)param).Ticks / 10;
            else if (param is IEnumerable) 
                return AddslashesIEnumerable(param, mapType, mapColumn);

            return string.Concat("'", param.ToString().Replace("'", "''"), "'");
        }

        protected override DbCommand CreateCommand()
        {
            return new OdbcCommand();
        }

        protected override void ReturnConnection(IObjectPool<DbConnection> pool, Object<DbConnection> conn, Exception ex)
        {
            var rawPool = pool as OdbcMySqlConnectionPool;
            if (rawPool != null) rawPool.Return(conn, ex);
            else pool.Return(conn);
        }

        protected override DbParameter[] GetDbParamtersByObject(string sql, object obj) => _util.GetDbParamtersByObject(sql, obj);
    }
}
