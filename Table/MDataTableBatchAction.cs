﻿using System;
using System.Collections.Generic;
using System.Text;
using CYQ.Data.SQL;
using CYQ.Data.Tool;

using System.Data.SqlClient;
using System.Reflection;
using System.Data;
using System.Data.SqlTypes;
using System.IO;

//using Oracle.DataAccess.Client;


namespace CYQ.Data.Table
{
    /// <summary>
    /// 批量操作。
    /// </summary>
    internal class MDataTableBatchAction
    {
        /// <summary>
        /// 联合主键
        /// </summary>
        List<int> jointPrimaryIndex;
        MDataTable mdt, sourceTable;
        internal DalType dalTypeTo = DalType.None;
        internal string database = string.Empty;
        string _Conn = string.Empty;
        /// <summary>
        /// 内部操作对象（需要同一个事务处理）。
        /// </summary>
        private DbBase _dalHelper;

        public MDataTableBatchAction(MDataTable mTable)
        {
            Init(mTable, string.Empty);
        }
        public MDataTableBatchAction(MDataTable mTable, string conn)
        {
            Init(mTable, conn);
        }
        private void Init(MDataTable mTable, string conn)
        {
            if (mTable.Columns == null || mTable.Columns.Count == 0)
            {
                Error.Throw("MDataTable's columns can't be null or columns'length can't be zero");
            }
            if (string.IsNullOrEmpty(mTable.TableName))
            {
                Error.Throw("MDataTable's tablename can't  null or empty");
            }
            mdt = sourceTable = mTable;

            if (mdt.TableName.IndexOfAny(new char[] { '(', ')' }) > -1)
            {
                mdt.TableName = mdt.TableName.Substring(mdt.TableName.LastIndexOf(')') + 1).Trim();
            }

            _Conn = !string.IsNullOrEmpty(conn) ? conn : mTable.Conn;
            if (!DBTool.ExistsTable(mdt.TableName, _Conn, out dalTypeTo, out database))
            {
                DBTool.CreateTable(mdt.TableName, mdt.Columns, _Conn);
            }
            MDataColumn column = DBTool.GetColumns(mdt.TableName, _Conn);
            FixTable(column);//
            if (mdt.Columns.Count == 0)
            {
                Error.Throw("After fix table columns, length can't be zero");
            }
            SetDbBaseForTransaction();
        }
        private void SetDbBaseForTransaction()
        {
            if (mdt.DynamicData != null)
            {
                if (mdt.DynamicData is MProc)
                {
                    _dalHelper = ((MProc)mdt.DynamicData).dalHelper;
                }
                else if (mdt.DynamicData is MAction)
                {
                    _dalHelper = ((MAction)mdt.DynamicData).dalHelper;
                }
            }
        }
        /// <summary>
        /// 进行列修正（只有移除 和 修正类型，若无主键列，则新增主键列）
        /// </summary>
        private void FixTable(MDataColumn column)
        {
            if (column.Count > 0)
            {
                bool tableIsChange = false;
                for (int i = mdt.Columns.Count - 1; i >= 0; i--)
                {
                    if (!column.Contains(mdt.Columns[i].ColumnName))//没有此列
                    {
                        if (!tableIsChange)
                        {
                            mdt = mdt.Clone();//列需要变化时，克隆一份，不变更原有数据。
                            tableIsChange = true;
                        }
                        mdt.Columns.RemoveAt(i);
                    }
                    else
                    {
                        MCellStruct ms = column[mdt.Columns[i].ColumnName];//新表的字段
                        Type valueType = mdt.Columns[i].ValueType;//存档的字段的值的原始类型。
                        bool isChangeType = mdt.Columns[i].SqlType != ms.SqlType;
                        mdt.Columns[i].Load(ms);
                        if (isChangeType)
                        {
                            //修正数据的数据类型。
                            foreach (MDataRow row in mdt.Rows)
                            {
                                row[i].FixValue();//重新自我赋值修正数据类型。
                            }
                        }

                    }
                }
                //主键检测，若没有，则补充主键
                if (column.JointPrimary != null && column.JointPrimary.Count > 0)
                {
                    if (!mdt.Columns.Contains(column[0].ColumnName) && (column[0].IsPrimaryKey || column[0].IsAutoIncrement))
                    {
                        MCellStruct ms = column[0].Clone();
                        mdt = mdt.Clone();//列需要变化时，克隆一份，不变更原有数据。
                        ms.MDataColumn = null;
                        mdt.Columns.Insert(0, ms);
                    }
                }
            }
        }
        /// <summary>
        /// 设置联合主键
        /// </summary>
        /// <param name="jointPrimaryKeys">联合主键</param>
        internal void SetJoinPrimaryKeys(object[] jointPrimaryKeys)
        {
            if (jointPrimaryKeys != null && jointPrimaryKeys.Length > 0)
            {
                int index = -1;
                jointPrimaryIndex = new List<int>();
                foreach (object o in jointPrimaryKeys) // 检测列名是否存在，不存在则抛异常
                {
                    index = mdt.Columns.GetIndex(Convert.ToString(o));
                    if (index == -1)
                    {
                        Error.Throw("table " + mdt.TableName + " not exist the column name : " + Convert.ToString(o));
                    }
                    else
                    {
                        if (!jointPrimaryIndex.Contains(index))
                        {
                            jointPrimaryIndex.Add(index);
                        }
                    }
                }
            }
        }
        internal MDataCell[] GetJoinPrimaryCell(MDataRow row)
        {
            MDataCell[] cells = null;
            if (jointPrimaryIndex != null && jointPrimaryIndex.Count > 0)
            {
                cells = new MDataCell[jointPrimaryIndex.Count];
                for (int i = 0; i < jointPrimaryIndex.Count; i++)
                {
                    cells[i] = row[jointPrimaryIndex[i]];
                }
            }
            else
            {
                cells = row.JointPrimaryCell.ToArray();
            }
            return cells;
        }

        internal bool Insert(bool keepID)
        {
            try
            {
                if (dalTypeTo == DalType.MsSql)
                {
                    return MsSqlBulkCopyInsert(keepID);
                }
                else if (dalTypeTo == DalType.Oracle && keepID && OracleDal.isUseOdpNet == 1 && _dalHelper == null)
                {
                    return OracleBulkCopyInsert();
                }
                else if (dalTypeTo == DalType.MySql && IsAllowMySqlBulkCopy())
                {
                    return MySqlBulkCopyInsert(keepID);
                }
                else
                {
                    if (dalTypeTo == DalType.Txt || dalTypeTo == DalType.Xml)
                    {
                        NoSqlAction.ResetStaticVar();
                    }
                    return NomalInsert(keepID);
                }
            }
            catch (Exception err)
            {
                if (err.InnerException != null)
                {
                    err = err.InnerException;
                }
                sourceTable.DynamicData = err;
                Log.WriteLogToTxt(err);
                return false;
            }
        }
        internal bool MsSqlBulkCopyInsert(bool keepID)
        {
            try
            {
                CheckGUIDAndDateTime(DalType.MsSql);
                // string a, b, c;
                string conn = AppConfig.GetConn(_Conn);// DAL.DalCreate.GetConnString(_Conn, out a, out b, out c);
                SqlTransaction sqlTran = null;
                SqlConnection con = null;
                if (_dalHelper != null)
                {
                    sqlTran = _dalHelper._tran as SqlTransaction;
                    con = _dalHelper.Con as SqlConnection;
                    _dalHelper.OpenCon(null);//如果未开启，则开启
                }
                else
                {
                    con = new SqlConnection(conn);
                    con.Open();
                }

                using (SqlBulkCopy sbc = new SqlBulkCopy(con, (keepID ? SqlBulkCopyOptions.KeepIdentity : SqlBulkCopyOptions.Default) | SqlBulkCopyOptions.FireTriggers, sqlTran))
                {
                    sbc.BatchSize = 100000;
                    sbc.DestinationTableName = SqlFormat.Keyword(mdt.TableName, DalType.MsSql);
                    sbc.BulkCopyTimeout = AppConfig.DB.CommandTimeout;
                    foreach (MCellStruct column in mdt.Columns)
                    {
                        sbc.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    }
                    sbc.WriteToServer(mdt);
                }
                if (_dalHelper == null)
                {
                    con.Close();
                    con = null;
                }
                return true;
            }
            catch (Exception err)
            {
                sourceTable.DynamicData = err;
                Log.WriteLogToTxt(err);
            }
            return false;
        }
        internal bool OracleBulkCopyInsert()
        {
            CheckGUIDAndDateTime(DalType.Oracle);
            string conn = DalCreate.FormatConn(DalType.Oracle, AppConfig.GetConn(_Conn));
            Assembly ass = OracleDal.GetAssembly();
            object sbc = ass.CreateInstance("Oracle.DataAccess.Client.OracleBulkCopy", false, BindingFlags.CreateInstance, null, new object[] { conn }, null, null);
            Type sbcType = sbc.GetType();
            try
            {

                sbcType.GetProperty("BatchSize").SetValue(sbc, 100000, null);
                sbcType.GetProperty("BulkCopyTimeout").SetValue(sbc, AppConfig.DB.CommandTimeout, null);
                sbcType.GetProperty("DestinationTableName").SetValue(sbc, SqlFormat.Keyword(mdt.TableName, DalType.Oracle), null);
                PropertyInfo cInfo = sbcType.GetProperty("ColumnMappings");
                object cObj = cInfo.GetValue(sbc, null);
                MethodInfo addMethod = cInfo.PropertyType.GetMethods()[4];
                foreach (MCellStruct column in mdt.Columns)
                {
                    addMethod.Invoke(cObj, new object[] { column.ColumnName, column.ColumnName });
                }

                sbcType.GetMethods()[4].Invoke(sbc, new object[] { mdt });

                return true;
            }
            catch (Exception err)
            {
                if (err.InnerException != null)
                {
                    err = err.InnerException;
                }
                sourceTable.DynamicData = err;
                Log.WriteLogToTxt(err);
                return false;
            }
            finally
            {
                sbcType.GetMethod("Dispose").Invoke(sbc, null);
            }
            //using (Oracle.DataAccess.Client.OracleBulkCopy sbc = new OracleBulkCopy(conn, OracleBulkCopyOptions.Default))
            //{
            //    sbc.BatchSize = 100000;
            //    sbc.DestinationTableName = mdt.TableName;
            //    foreach (MCellStruct column in mdt.Columns)
            //    {
            //        sbc.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            //    }
            //    sbc.WriteToServer(mdt);
            //}
            //return true;



        }
        bool IsAllowMySqlBulkCopy()
        {
            foreach (MCellStruct st in mdt.Columns)
            {
                switch (DataType.GetGroup(st.SqlType))
                {
                    case 999:
                    case 3://bool型也会有问题
                        return false;
                }
            }
            try
            {
                string path = Path.GetTempPath() + "t.t";
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
                else
                {
                    File.Create(path).Close();//检测文件夹的读写权限
                    File.Delete(path);
                }
            }
            catch
            {

                return false;
            }
            return true;
        }
        internal bool MySqlBulkCopyInsert(bool keepID)
        {
            bool fillGUID = CheckGUIDAndDateTime(DalType.MySql);
            string conn = DalCreate.FormatConn(DalType.MySql, AppConfig.GetConn(_Conn));
            bool isNeedCreateDal = (_dalHelper == null);
            if (isNeedCreateDal)
            {
                _dalHelper = DalCreate.CreateDal(conn);
                _dalHelper.isAllowInterWriteLog = false;
            }
            string path = MDataTableToFile(mdt, fillGUID ? true : keepID);
            string sql = string.Format(SqlCreate.MySqlBulkCopySql, path, SqlFormat.Keyword(mdt.TableName, DalType.MySql),
                AppConst.SplitChar, SqlCreate.GetColumnName(mdt.Columns, keepID, DalType.MySql));

            try
            {
                if (_dalHelper.ExeNonQuery(sql, false) != -2)
                {
                    return true;
                }

            }
            catch (Exception err)
            {
                if (err.InnerException != null)
                {
                    err = err.InnerException;
                }
                sourceTable.DynamicData = err;
                Log.WriteLogToTxt(err);
            }
            finally
            {
                if (isNeedCreateDal)
                {
                    _dalHelper.Dispose();
                    _dalHelper = null;
                }
                // File.Delete(path);
            }
            return false;
        }
        private static string MDataTableToFile(MDataTable dt, bool keepID)
        {
            string path = Path.GetTempPath() + dt.TableName + ".txt";
            using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                MCellStruct ms;
                string value;
                foreach (MDataRow row in dt.Rows)
                {
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        ms = dt.Columns[i];
                        if (!keepID && ms.IsAutoIncrement)
                        {
                            continue;
                        }
                        else
                        {
                            value = row[i].ToString();
                            if (ms.SqlType == SqlDbType.Bit && value != "1")
                            {
                                value = (value.ToLower() == "true") ? "1" : "0";
                            }
                            value = value.Replace("\\", "\\\\");//处理转义符号
                            sw.Write(value);
                        }

                        if (i != dt.Columns.Count - 1)//不是最后一个就输出
                        {
                            sw.Write(AppConst.SplitChar);
                        }
                    }
                    sw.WriteLine();
                }
            }
            if (Path.DirectorySeparatorChar == '\\')
            {
                path = path.Replace(@"\", @"\\");
            }
            return path;
        }
        #region 注释掉
        /*
        internal bool SybaseBulkCopyInsert()
        {

            // string a, b, c;
            string conn = DalCreate.FormatConn(DalType.Sybase, AppConfig.GetConn(_Conn));

            using (Sybase.Data.AseClient.AseBulkCopy sbc = new Sybase.Data.AseClient.AseBulkCopy(conn, Sybase.Data.AseClient.AseBulkCopyOptions.KeepIdentity))
            {
                sbc.BatchSize = 100000;
                sbc.DestinationTableName = mdt.TableName;
                foreach (MCellStruct column in mdt.Columns)
                {
                    Sybase.Data.AseClient.AseBulkCopyColumnMapping ac = new Sybase.Data.AseClient.AseBulkCopyColumnMapping();
                    ac.SourceColumn = ac.DestinationColumn = column.ColumnName;
                    sbc.ColumnMappings.Add(ac);
                }
                sbc.WriteToServer(mdt.ToDataTable());
            }
            return true;


            //Assembly ass = SybaseDal.GetAssembly();

            //object sbc = ass.CreateInstance("Sybase.Data.AseClient.AseBulkCopy", false, BindingFlags.CreateInstance, null, new object[] { conn }, null, null);

            //Type sbcType = sbc.GetType();
            //try
            //{

            //    sbcType.GetProperty("BatchSize").SetValue(sbc, 100000, null);
            //    sbcType.GetProperty("DestinationTableName").SetValue(sbc, SqlFormat.Keyword(mdt.TableName, DalType.Sybase), null);
            //    PropertyInfo cInfo = sbcType.GetProperty("ColumnMappings");
            //    object cObj = cInfo.GetValue(sbc, null);
            //    MethodInfo addMethod = cInfo.PropertyType.GetMethods()[2];
            //    foreach (MCellStruct column in mdt.Columns)
            //    {
            //        object columnMapping = ass.CreateInstance("Sybase.Data.AseClient.AseBulkCopyColumnMapping", false, BindingFlags.CreateInstance, null, new object[] { column.ColumnName, column.ColumnName }, null, null);
            //        addMethod.Invoke(cObj, new object[] { columnMapping });
            //    }
            //    //Oracle.DataAccess.Client.OracleBulkCopy ttt = sbc as Oracle.DataAccess.Client.OracleBulkCopy;
            //    //ttt.WriteToServer(mdt);
            //    sbcType.GetMethods()[14].Invoke(sbc, new object[] { mdt.ToDataTable() });
            //    return true;
            //}
            //catch (Exception err)
            //{
            //    Log.WriteLogToTxt(err);
            //    return false;
            //}
            //finally
            //{
            //    sbcType.GetMethod("Dispose").Invoke(sbc, null);
            //}
        } 
        */
        #endregion
        internal bool NomalInsert(bool keepID)
        {
            bool result = true;
            using (MAction action = new MAction(mdt.TableName, _Conn))
            {
                DbBase sourceHelper = action.dalHelper;
                action.SetAopOff();
                if (_dalHelper != null)
                {
                    action.dalHelper = _dalHelper;
                }
                else
                {
                    action.BeginTransation();//事务由外部控制
                }
                action.dalHelper.IsAllowRecordSql = false;//屏蔽SQL日志记录
                if (keepID)
                {
                    action.SetIdentityInsertOn();
                }
                MDataRow row;
                for (int i = 0; i < mdt.Rows.Count; i++)
                {
                    row = mdt.Rows[i];
                    action.ResetTable(row, false);
                    action.Data.SetState(1, BreakOp.Null);
                    result = action.Insert(InsertOp.None);
                    sourceTable.RecordsAffected = i;
                    if (!result)
                    {
                        string msg = "Error On : MDataTable.AcceptChanges.Insert." + mdt.TableName + " : [" + row.PrimaryCell.Value + "] : " + action.DebugInfo;
                        sourceTable.DynamicData = msg;
                        Log.WriteLogToTxt(msg);
                        break;
                    }
                }
                if (keepID)
                {
                    action.SetIdentityInsertOff();
                }
                if (_dalHelper == null)
                {
                    action.EndTransation();
                }
                action.dalHelper.IsAllowRecordSql = true;//恢复SQL日志记录
                action.dalHelper = sourceHelper;//恢复原来，避免外来的链接被关闭。
            }
            return result;
        }

        internal bool Update()
        {
            bool hasFK = (jointPrimaryIndex != null && jointPrimaryIndex.Count > 1) || mdt.Columns.JointPrimary.Count > 1;
            if (!hasFK)
            {
                foreach (MCellStruct item in mdt.Columns)
                {
                    if ((item.IsForeignKey && string.IsNullOrEmpty(item.FKTableName)) || item.MaxSize > 8000 || DataType.GetGroup(item.SqlType) == 999)
                    {
                        hasFK = true;
                        break;
                    }
                }
            }
            if (hasFK)
            {
                return NormalUpdate();
            }
            else
            {
                return BulkCopyUpdate();//只有一个主键，没有外键关联，同时只有基础类型。
            }
        }

        internal bool NormalUpdate()
        {
            List<int> indexList = new List<int>();
            bool result = true;
            using (MAction action = new MAction(mdt.TableName, _Conn))
            {
                action.SetAopOff();
                DbBase sourceHelper = action.dalHelper;
                if (_dalHelper != null)
                {
                    action.dalHelper = _dalHelper;
                }
                else
                {
                    action.BeginTransation();
                }
                action.dalHelper.IsAllowRecordSql = false;//屏蔽SQL日志记录

                MDataRow row;
                for (int i = 0; i < mdt.Rows.Count; i++)
                {
                    row = mdt.Rows[i];
                    if (row.GetState(true) > 1)
                    {
                        action.ResetTable(row, false);
                        string where = SqlCreate.GetWhere(action.DalType, GetJoinPrimaryCell(row));
                        result = action.Update(where) || action.RecordsAffected == 0;//没有可更新的数据，也返回true
                        sourceTable.RecordsAffected = i;
                        if (!result)
                        {
                            string msg = "Error On : MDataTable.AcceptChanges.Update." + mdt.TableName + " : [" + row.PrimaryCell.Value + "] : " + action.DebugInfo;
                            sourceTable.DynamicData = msg;
                            Log.WriteLogToTxt(msg);
                            break;
                        }
                        else
                        {
                            indexList.Add(i);
                        }
                    }
                }
                action.dalHelper.IsAllowRecordSql = true;//恢复SQL日志记录
                if (_dalHelper == null)
                {
                    action.EndTransation();
                }
                else
                {
                    action.dalHelper = sourceHelper;//恢复原来，避免外来的链接被关闭。
                }
            }
            if (result)
            {
                foreach (int index in indexList)
                {
                    mdt.Rows[index].SetState(0);
                }
                indexList.Clear();
                indexList = null;
            }
            return result;
        }

        internal bool BulkCopyUpdate()
        {
            int count = 0, pageSize = 5000;
            MDataTable dt = null;
            using (MAction action = new MAction(mdt.TableName, _Conn))
            {
                if (action.DalVersion.StartsWith("08"))
                {
                    pageSize = 1000;
                }
                count = mdt.Rows.Count / pageSize;
                DbBase sourceHelper = action.dalHelper;
                if (_dalHelper != null)
                {
                    action.dalHelper = _dalHelper;
                }
                else
                {
                    action.BeginTransation();
                }

                bool result = false;
                MCellStruct keyColumn = jointPrimaryIndex != null ? mdt.Columns[jointPrimaryIndex[0]] : mdt.Columns.FirstPrimary;
                string columnName = keyColumn.ColumnName;
                for (int i = 0; i < count; i++)
                {
                    dt = mdt.Select(i + 1, pageSize, null);//分页读取
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        #region 核心逻辑
                        string whereIn = SqlCreate.GetWhereIn(keyColumn, dt.GetColumnItems<string>(columnName, BreakOp.NullOrEmpty, true), action.DalType);
                        MDataTable dtData = action.Select(whereIn);//获取远程数据。
                        dtData.Load(dt);//重新加载赋值。
                        result = action.Delete(whereIn);
                        if (result)
                        {
                            dtData.DynamicData = action;
                            result = dtData.AcceptChanges(AcceptOp.InsertWithID);
                        }
                        if (!result)
                        {
                            break;
                        }
                        #endregion
                    }
                }
                if (_dalHelper == null)
                {
                    action.BeginTransation();
                }
                else
                {
                    action.dalHelper = sourceHelper;//还原。
                }
            }
            return true;
        }
        internal bool Auto()
        {
            bool result = true;

            using (MAction action = new MAction(mdt.TableName, _Conn))
            {
                action.SetAopOff();
                DbBase sourceHelper = action.dalHelper;
                if (_dalHelper != null)
                {
                    action.dalHelper = _dalHelper;
                }
                else
                {
                    action.BeginTransation();
                }
                action.dalHelper.IsAllowRecordSql = false;//屏蔽SQL日志记录 2000数据库大量的In条件会超时。

                if ((jointPrimaryIndex != null && jointPrimaryIndex.Count == 1) || mdt.Columns.JointPrimary.Count == 1)
                //jointPrimaryIndex == null && mdt.Columns.JointPrimary.Count == 1 && mdt.Rows.Count <= 10000
                //&& (!action.DalVersion.StartsWith("08") || mdt.Rows.Count < 1001)) //只有一个主键-》组合成In远程查询返回数据-》
                {
                    #region 新逻辑

                    MCellStruct keyColumn = jointPrimaryIndex != null ? mdt.Columns[jointPrimaryIndex[0]] : mdt.Columns.FirstPrimary;
                    string columnName = keyColumn.ColumnName;
                    //计算分组处理
                    int pageSize = 5000;
                    if (action.DalVersion.StartsWith("08")) { pageSize = 1000; }
                    int count = mdt.Rows.Count / pageSize + 1;
                    for (int i = 0; i < count; i++)
                    {
                        MDataTable dt = mdt.Select(i + 1, pageSize, null);//分页读取
                        if (dt != null && dt.Rows.Count > 0)
                        {
                            string whereIn = SqlCreate.GetWhereIn(keyColumn, dt.GetColumnItems<string>(columnName, BreakOp.NullOrEmpty, true), action.DalType);
                            action.SetSelectColumns(columnName);
                            MDataTable keyTable = action.Select(whereIn);//拿到数据，准备分拆上市

                            MDataTable[] dt2 = dt.Split(SqlCreate.GetWhereIn(keyColumn, keyTable.GetColumnItems<string>(columnName, BreakOp.NullOrEmpty, true), DalType.None));//这里不需要格式化查询条件。
                            result = dt2[0].Rows.Count == 0;
                            if (!result)
                            {
                                MDataTable updateTable = dt2[0];
                                updateTable.SetState(2, BreakOp.Null);
                                updateTable.DynamicData = action;
                                result = updateTable.AcceptChanges(AcceptOp.Update, _Conn, columnName);
                                if (!result)
                                {
                                    sourceTable.DynamicData = updateTable.DynamicData;
                                }
                            }
                            if (result && dt2[1].Rows.Count > 0)
                            {
                                MDataTable insertTable = dt2[1];
                                insertTable.DynamicData = action;
                                bool keepID = !insertTable.Rows[0].PrimaryCell.IsNullOrEmpty;
                                result = insertTable.AcceptChanges((keepID ? AcceptOp.InsertWithID : AcceptOp.Insert), _Conn, columnName);
                                if (!result)
                                {
                                    sourceTable.DynamicData = insertTable.DynamicData;
                                }
                            }
                        }
                    }
                    
                    #endregion
                    
                    #region 旧逻辑，已不用 分拆处理 本地比较分拆两个表格【更新和插入】-》分开独立处理。
                    /*
                    string columnName = mdt.Columns.FirstPrimary.ColumnName;
                    string whereIn = SqlCreate.GetWhereIn(mdt.Columns.FirstPrimary, mdt.GetColumnItems<string>(columnName, BreakOp.NullOrEmpty, true), action.DalType);
                    action.SetSelectColumns(mdt.Columns.FirstPrimary.ColumnName);
                    dt = action.Select(whereIn);

                    MDataTable[] dt2 = mdt.Split(SqlCreate.GetWhereIn(mdt.Columns.FirstPrimary, dt.GetColumnItems<string>(columnName, BreakOp.NullOrEmpty, true), DalType.None));//这里不需要格式化查询条件。
                    result = dt2[0].Rows.Count == 0;
                    if (!result)
                    {
                        dt2[0].SetState(2, BreakOp.Null);
                        dt2[0].DynamicData = action;
                        MDataTableBatchAction m1 = new MDataTableBatchAction(dt2[0], _Conn);
                        m1.SetJoinPrimaryKeys(new string[] { columnName });
                        result = m1.Update();
                        if (!result)
                        {
                            sourceTable.DynamicData = dt2[0].DynamicData;
                        }
                    }
                    if (result && dt2[1].Rows.Count > 0)
                    {
                        dt2[1].DynamicData = action;
                        MDataTableBatchAction m2 = new MDataTableBatchAction(dt2[1], _Conn);
                        m2.SetJoinPrimaryKeys(new string[] { columnName });
                        result = m2.Insert(!dt2[1].Rows[0].PrimaryCell.IsNullOrEmpty);
                        if (!result)
                        {
                            sourceTable.DynamicData = dt2[1].DynamicData;
                        }
                    }
                     */
                    #endregion

                }
                else
                {
                    // action.BeginTransation();
                    foreach (MDataRow row in mdt.Rows)
                    {
                        #region 循环处理
                        action.ResetTable(row, false);
                        string where = SqlCreate.GetWhere(action.DalType, GetJoinPrimaryCell(row));
                        if (!action.Exists(where))//row.PrimaryCell.IsNullOrEmpty || 
                        {
                            action.AllowInsertID = !row.PrimaryCell.IsNullOrEmpty;
                            action.Data.SetState(1, BreakOp.Null);
                            result = action.Insert(InsertOp.None);
                        }
                        else
                        {
                            action.Data.SetState(2);
                            if (action.Data.GetState(true) == 2)
                            {
                                result = action.Update(where);
                            }
                        }
                        if (!result)
                        {
                            string msg = "Error On : MDataTable.AcceptChanges.Auto." + mdt.TableName + " : [" + row[0].Value + "] : " + action.DebugInfo;
                            sourceTable.DynamicData = msg;
                            Log.WriteLogToTxt(msg);
                            break;
                        }
                        #endregion
                    }

                }
                action.dalHelper.IsAllowRecordSql = true;//恢复SQL日志记录
                if (_dalHelper == null)
                {
                    action.EndTransation();
                }
                else
                {
                    action.dalHelper = sourceHelper;//还原
                }
            }

            return result;
        }

        /// <summary>
        /// 检测GUID，若空，补值。
        /// </summary>
        private bool CheckGUIDAndDateTime(DalType dal)
        {
            bool fillGUID = false;
            int groupID;
            for (int i = 0; i < mdt.Columns.Count; i++)
            {
                MCellStruct ms = mdt.Columns[i];
                groupID = DataType.GetGroup(ms.SqlType);
                if (groupID == 2)
                {
                    for (int j = 0; j < mdt.Rows.Count; j++)
                    {
                        if (dal == DalType.MsSql && mdt.Rows[j][i].strValue == DateTime.MinValue.ToString())
                        {
                            mdt.Rows[j][i].Value = SqlDateTime.MinValue;
                        }
                        else if (dal == DalType.Oracle && mdt.Rows[j][i].strValue == SqlDateTime.MinValue.ToString())
                        {
                            mdt.Rows[j][i].Value = SqlDateTime.MinValue;
                        }
                    }
                }
                else if (ms.IsPrimaryKey && (groupID == 4 || (groupID == 0 && ms.MaxSize >= 36)))
                {
                    string defaultValue = Convert.ToString(ms.DefaultValue);
                    bool isGuid = defaultValue == "" || defaultValue == "newid" || defaultValue == SqlValue.GUID;
                    if (isGuid && !fillGUID)
                    {
                        fillGUID = true;
                    }
                    for (int k = 0; k < mdt.Rows.Count; k++)
                    {
                        if (mdt.Rows[k][i].IsNullOrEmpty)
                        {
                            mdt.Rows[k][i].Value = isGuid ? Guid.NewGuid().ToString() : defaultValue;
                        }
                    }
                }
            }
            return fillGUID;
        }
    }
    //[Flags]
    //internal enum OracleBulkCopyOptions
    //{
    //    Default = 0,
    //    UseInternalTransaction = 1,
    //}
    /// <summary>
    /// MDataTable的AcceptChanges方法的选项
    /// </summary>

}
