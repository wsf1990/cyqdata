using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Configuration;
using System.ComponentModel;
using CYQ.Data.SQL;
using CYQ.Data.Cache;
using CYQ.Data.Table;

using CYQ.Data.Aop;
using CYQ.Data.Tool;
using CYQ.Data.UI;


namespace CYQ.Data
{
    /// <summary>
    /// ���ݲ�����[�ɲ�������/��ͼ]
    /// </summary>
    public partial class MAction : IDisposable
    {
        #region ȫ�ֱ���

        internal DbBase dalHelper;//���ݲ���

        private SqlCreate _sqlCreate;



        private InsertOp _option = InsertOp.Fill;

        private NoSqlAction _noSqlAction = null;
        private MDataRow _Data;//��ʾһ��
        /// <summary>
        /// Fill��֮�󷵻ص�������
        /// </summary>
        public MDataRow Data
        {
            get
            {
                return _Data;
            }
        }
        /// <summary>
        /// ԭʼ�������ı���/��ͼ����δ����[�����ݿ�ת������]��ʽ����
        /// </summary>
        private string _sourceTableName;
        private string _TableName; //����
        /// <summary>
        /// ��ǰ�����ı���(������������ͼ��䣬��Ϊ������ͼ���֣�
        /// </summary>
        public string TableName
        {
            get
            {
                return _TableName;
            }
            set
            {
                _TableName = value;
            }
        }
        /// <summary>
        /// ��ȡ��ǰ���ݿ������ַ���
        /// </summary>
        public string ConnectionString
        {
            get
            {
                if (dalHelper != null)
                {
                    return dalHelper.conn;
                }
                return string.Empty;
            }
        }
        private string _debugInfo = string.Empty;
        /// <summary>
        /// ������Ϣ[����Ҫ�鿴����ִ�е�SQL���,������AppDebug.OpenDebugInfo�������ļ�OpenDebugInfo��Ϊture]
        /// </summary>
        public string DebugInfo
        {
            get
            {
                if (dalHelper != null)
                {
                    return dalHelper.debugInfo.ToString();
                }
                return _debugInfo;
            }
            set
            {
                if (dalHelper != null)
                {
                    dalHelper.debugInfo.Length = 0;
                    dalHelper.debugInfo.Append(value);
                }
            }
        }
        /// <summary>
        /// ��ǰ���������ݿ�����[Access/Mssql/Oracle/SQLite/MySql/Txt/Xml��]
        /// </summary>
        public DalType DalType
        {
            get
            {
                return dalHelper.dalType;
            }
        }
        /// <summary>
        /// ��ǰ���������ݿ�İ汾��
        /// </summary>
        public string DalVersion
        {
            get
            {
                return dalHelper.Version;
            }
        }
        /// <summary>
        /// ִ��SQL����ʱ��Ӱ���������-2��Ϊ�쳣����
        /// </summary>
        public int RecordsAffected
        {
            get
            {
                return dalHelper.recordsAffected;
            }
        }
        /// <summary>
        /// ���ʱ����[��λ��]
        /// </summary>
        public int TimeOut
        {
            get
            {
                if (dalHelper.Com != null)
                {
                    return dalHelper.Com.CommandTimeout;
                }
                return -1;
            }
            set
            {
                if (dalHelper.Com != null)
                {
                    dalHelper.Com.CommandTimeout = value;
                }
            }
        }
        private bool _AllowInsertID = false;
        /// <summary>
        /// ����������Ϊ��Int������ʶ�ģ��Ƿ������ֶ�����ID
        /// </summary>
        public bool AllowInsertID
        {
            get
            {
                return _AllowInsertID;
            }
            set
            {
                _AllowInsertID = value;
            }
        }
        private bool _isInsertCommand; //�Ƿ�ִ�в�������(������Update����)

        private bool _setIdentityResult = true;
        /// <summary>
        /// MSSQL�����ʶIDʱ������ѡ��[������ʱ��Ч]
        /// </summary>
        internal void SetIdentityInsertOn()
        {
            _setIdentityResult = true;
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.MsSql:
                    case DalType.Sybase:
                        if (_Data.Columns.FirstPrimary.IsAutoIncrement)//������
                        {
                            try
                            {
                                string lastTable = Convert.ToString(CacheManage.LocalInstance.Get("MAction_IdentityInsertForSql"));
                                if (!string.IsNullOrEmpty(lastTable))
                                {
                                    lastTable = "set identity_insert " + SqlFormat.Keyword(lastTable, dalHelper.dalType) + " off";
                                    dalHelper.ExeNonQuery(lastTable, false);
                                }
                                _setIdentityResult = dalHelper.ExeNonQuery("set identity_insert " + SqlFormat.Keyword(_TableName, dalHelper.dalType) + " on", false) > -2;
                                if (_setIdentityResult)
                                {
                                    CacheManage.LocalInstance.Set("MAction_IdentityInsertForSql", _TableName, 30);
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                }

            }
            _AllowInsertID = true;
        }
        /// <summary>
        /// MSSQL�����ʶID��رմ�ѡ��[������ʱ��Ч]
        /// </summary>;
        internal void SetIdentityInsertOff()
        {
            if (_setIdentityResult && dalHelper != null && dalHelper.isOpenTrans)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.MsSql:
                    case DalType.Sybase:
                        if (_Data.Columns.FirstPrimary.IsAutoIncrement)//������
                        {
                            try
                            {
                                if (dalHelper.ExeNonQuery("set identity_insert " + SqlFormat.Keyword(_TableName, DalType.MsSql) + " off", false) > -2)
                                {
                                    _setIdentityResult = false;
                                    CacheManage.LocalInstance.Remove("MAction_IdentityInsertForSql");
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                }

            }
            _AllowInsertID = false;
        }
        #endregion

        #region ���캯��

        /// <summary>
        /// ���캯��
        /// </summary>
        /// <param name="tableNamesEnum">����/��ͼ����</param>
        /// <example><code>
        ///     MAction action=new MAction(TableNames.Users);
        /// ��  MAction action=new MAction("Users");
        /// ��  MAction action=new MAction("(select m.*,u.UserName from Users u left join Message m on u.ID=m.UserID) v");
        /// ��  MAction action=new MAction(ViewNames.Users);//����ͼ
        /// ������ݿⷽʽ��
        /// MAction action=new MAction(U_DataBaseNameEnum.Users);
        /// ˵�����Զ���ȡ���ݿ�����[U_��EnumΪǰ��׺],ȡ�������ݿ�������ΪDataBaseNameConn
        /// U_Ϊ�� V_Ϊ��ͼ P_Ϊ�洢����
        /// </code></example>
        public MAction(object tableNamesEnum)
        {
            Init(tableNamesEnum, AppConfig.DB.DefaultConn);
        }

        /// <summary>
        /// ���캯��2
        /// </summary>
        /// <param name="tableNamesEnum">����/��ͼ����</param>
        /// <param name="conn">web.config�µ�connectionStrings��name����������,�������������ַ���</param>
        /// <example><code>
        ///     MAction action=new MAction(TableNames.Users,"Conn");
        /// ��  MAction action=new MAction(TableNames.Users,"server=.;database=CYQ;uid=sa;pwd=123456");
        /// </code></example>
        public MAction(object tableNamesEnum, string conn)
        {
            Init(tableNamesEnum, conn);
        }
        #endregion

        #region ��ʼ��

        private void Init(object tableObj, string conn)
        {
            tableObj = SqlCreate.SqlToViewSql(tableObj);
            string dbName = StaticTool.GetDbName(ref tableObj);
            if (conn == AppConfig.DB.DefaultConn && !string.IsNullOrEmpty(dbName))
            {
                conn = dbName + "Conn";
            }
            MDataRow newRow;
            InitConn(tableObj, conn, out newRow);//���Դ�MDataRow��ȡ�µ�Conn����
            InitSqlHelper(newRow, dbName);
            InitRowSchema(newRow, true);
            InitGlobalObject(true);
            //Aop.IAop myAop = Aop.InterAop.Instance.GetFromConfig();//��ͼ�������ļ������Զ���Aop
            //if (myAop != null)
            //{
            //    SetAop(myAop);
            //}
        }
        private void InitConn(object tableObj, string conn, out MDataRow newRow)
        {
            if (tableObj is MDataRow)
            {
                newRow = tableObj as MDataRow;
            }
            else
            {
                newRow = new MDataRow();
                newRow.TableName = SqlFormat.NotKeyword(tableObj.ToString());
            }
            if (!string.IsNullOrEmpty(conn))
            {
                newRow.Conn = conn;
            }
            _sourceTableName = newRow.TableName;
        }
        private void InitSqlHelper(MDataRow newRow, string newDbName)
        {
            if (dalHelper == null)// || newCreate
            {
                dalHelper = DalCreate.CreateDal(newRow.Conn);
                //���������¼���
                if (dalHelper.IsOnExceptionEventNull)
                {
                    dalHelper.OnExceptionEvent += new DbBase.OnException(_DataSqlHelper_OnExceptionEvent);
                }
            }
            else
            {
                dalHelper.ClearParameters();//oracle 11g(ĳ�û��ĵ����ϻ�����⣬�л������������δ�壩
            }
            if (!string.IsNullOrEmpty(newDbName))//��Ҫ�л����ݿ⡣
            {
                if (string.Compare(dalHelper.DataBase, newDbName, StringComparison.OrdinalIgnoreCase) != 0)//���ݿ����Ʋ���ͬ��
                {
                    if (newRow.TableName.Contains(" "))//��ͼ��䣬��ֱ���л����ݿ����ӡ�
                    {
                        dalHelper.ChangeDatabase(newDbName);
                    }
                    else
                    {
                        bool isWithDbName = newRow.TableName.Contains(".");//�Ƿ�DBName.TableName
                        string fullTableName = isWithDbName ? newRow.TableName : newDbName + "." + newRow.TableName;
                        string sourceDbName = dalHelper.DataBase;
                        DbResetResult result = dalHelper.ChangeDatabaseWithCheck(fullTableName);
                        switch (result)
                        {
                            case DbResetResult.Yes://���ݿ��л��� (����Ҫǰ׺��
                            case DbResetResult.No_SaveDbName:
                            case DbResetResult.No_DBNoExists:
                                if (isWithDbName) //����ǰ׺�ģ�ȡ��ǰ׺
                                {
                                    _sourceTableName = newRow.TableName = SqlFormat.NotKeyword(fullTableName);
                                }
                                break;
                            case DbResetResult.No_Transationing:
                                if (!isWithDbName)//�����ͬ�����ݿ⣬��Ҫ�������ݿ�ǰ׺
                                {
                                    _sourceTableName = newRow.TableName = fullTableName;
                                }
                                break;
                        }
                    }
                }
            }
        }
        void _DataSqlHelper_OnExceptionEvent(string msg)
        {
            _aop.OnError(msg);
        }
        private static DateTime lastGCTime = DateTime.Now;
        private void InitRowSchema(MDataRow row, bool resetState)
        {
            _Data = row;
            _TableName = SqlCompatible.Format(_sourceTableName, dalHelper.dalType);
            _TableName = DBTool.GetMapTableName(dalHelper.conn, _TableName);//�������ݿ�ӳ����ݡ�
            if (_Data.Count == 0)
            {
                if (!TableSchema.FillTableSchema(ref _Data, ref dalHelper, _TableName, _sourceTableName))
                {
                    if (!dalHelper.TestConn())
                    {
                        Error.Throw(dalHelper.dalType + "." + dalHelper.DataBase + ":open database failed! check the connectionstring is be ok!\r\nerror:" + dalHelper.debugInfo.ToString());
                    }
                    Error.Throw(dalHelper.dalType + "." + dalHelper.DataBase + ":check the tablename  \"" + _TableName + "\" is exist?\r\nerror:" + dalHelper.debugInfo.ToString());
                }
            }
            else if (resetState)
            {
                _Data.SetState(0);
            }
            _Data.Conn = row.Conn;//FillTableSchema��ı�_Row�Ķ���
        }


        /// <summary>
        /// ���л�,��A��ʱ�������Ҫ����B,����Ҫ����newһ��MAaction,��ֱ�ӻ��ñ������л�
        /// </summary>
        /// <param name="tableObj">Ҫ�л��ı�/��ͼ��</param>
        /// <example><code>
        ///     using(MAction action = new MAction(TableNames.Users))
        ///     {
        ///         if (action.Fill("UserName='·������'"))
        ///         {
        ///             int id = action.Get&lt;int&gt;(Users.ID);
        ///             if (action.ResetTable(TableNames.Message))
        ///             {
        ///                  //other logic...
        ///             }
        ///         }
        ///     }
        /// </code></example>
        public void ResetTable(object tableObj)
        {
            ResetTable(tableObj, true, null);
        }
        /// <summary>
        /// ���л�
        /// </summary>
        /// <param name="tableObj">Ҫ�л��ı�/��ͼ��</param>
        /// <param name="resetState">�Ƿ�����ԭ�е�����״̬��Ĭ��true)</param>
        public void ResetTable(object tableObj, bool resetState)
        {
            ResetTable(tableObj, resetState, null);
        }
        /// <summary>
        /// ���л�
        /// </summary>
        /// <param name="tableObj">Ҫ�л��ı�/��ͼ��</param>
        /// <param name="newDbName">Ҫ�л������ݿ�����</param>
        public void ResetTable(object tableObj, string newDbName)
        {
            ResetTable(tableObj, true, newDbName);
        }
        private void ResetTable(object tableObj, bool resetState, string newDbName)
        {
            tableObj = SqlCreate.SqlToViewSql(tableObj);
            newDbName = newDbName ?? StaticTool.GetDbName(ref tableObj);
            MDataRow newRow;
            InitConn(tableObj, string.Empty, out newRow);

            //newRow.Conn = newDbName;//����ָ�����ӣ������л����ݿ�
            InitSqlHelper(newRow, newDbName);
            InitRowSchema(newRow, resetState);
            InitGlobalObject(false);
        }

        private void InitGlobalObject(bool allowCreate)
        {
            if (_Data != null)
            {
                if (_sqlCreate == null)
                {
                    _sqlCreate = new SqlCreate(this);
                }
                if (_UI != null)
                {
                    _UI._Data = _Data;
                }
                else if (allowCreate)
                {
                    _UI = new MActionUI(ref _Data, dalHelper, _sqlCreate);
                }
                if (_noSqlAction != null)
                {
                    _noSqlAction.Reset(ref _Data, _TableName, dalHelper.Con.DataSource, dalHelper.dalType);
                }
                else if (allowCreate)
                {
                    switch (dalHelper.dalType)
                    {
                        case DalType.Txt:
                        case DalType.Xml:
                            _noSqlAction = new NoSqlAction(ref _Data, _TableName, dalHelper.Con.DataSource, dalHelper.dalType);
                            break;
                    }
                }
            }
        }

        #endregion

        #region ���ݿ��������

        private bool InsertOrUpdate(string sqlCommandText)
        {
            bool returnResult = false;
            if (_sqlCreate.isCanDo)
            {
                if (_isInsertCommand) //����
                {
                    _isInsertCommand = false;
                    object ID;
                    switch (dalHelper.dalType)
                    {
                        case DalType.MsSql:
                        case DalType.Sybase:
                            ID = dalHelper.ExeScalar(sqlCommandText, false);
                            if (ID == null && AllowInsertID && dalHelper.recordsAffected > -2)
                            {
                                ID = _Data.PrimaryCell.Value;
                            }
                            break;
                        default:
                            ID = dalHelper.ExeNonQuery(sqlCommandText, false);
                            if (ID != null && Convert.ToInt32(ID) > 0 && _option != InsertOp.None)
                            {
                                if (DataType.GetGroup(_Data.PrimaryCell.Struct.SqlType) == 1)
                                {
                                    ClearParameters();
                                    ID = dalHelper.ExeScalar(_sqlCreate.GetMaxID(), false);
                                }
                                else
                                {
                                    ID = null;
                                    returnResult = true;
                                }

                            }
                            break;
                    }
                    if ((ID != null && Convert.ToString(ID) != "-2") || (dalHelper.recordsAffected > -2 && _option == InsertOp.None))
                    {
                        if (_option != InsertOp.None)
                        {
                            _Data.PrimaryCell.Value = ID;
                        }
                        returnResult = (_option == InsertOp.Fill) ? Fill(ID) : true;
                    }
                }
                else //����
                {
                    returnResult = dalHelper.ExeNonQuery(sqlCommandText, false) > 0;
                }
            }
            else if (!_isInsertCommand && _Data.GetState() == 1) // ���²�����
            {
                //���������Ϣ
                return true;
            }
            return returnResult;
        }

        #region ����

        /// <summary>
        ///  ��������
        /// </summary>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     action.Set(Users.UserName,"·������");
        ///     action.Insert();
        /// }
        /// </code></example>
        public bool Insert()
        {
            return Insert(false, _option);
        }
        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="option">����ѡ��</param>
        public bool Insert(InsertOp option)
        {
            return Insert(false, option);
        }
        /// <summary>
        ///  ��������
        /// </summary>
        /// <param name="autoSetValue">�Զ��ӿ��ƻ�ȡֵ</param>
        public bool Insert(bool autoSetValue)
        {
            return Insert(autoSetValue, _option);
        }
        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="autoSetValue">�Ƿ��Զ���ȡֵ[�Զ��ӿؼ���ȡֵ,��Ҫ�ȵ���SetAutoPrefix�������ÿؼ�ǰ׺]</param>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     action.SetAutoPrefix("txt","ddl");
        ///     action.Insert(true);
        /// }
        /// </code></example>
        public bool Insert(bool autoSetValue, InsertOp option)
        {
            CheckDisposed();
            if (autoSetValue)
            {
                _UI.GetAll(true && !AllowInsertID);//�������IDʱ��Ҳ��Ҫ��ȡ������
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.AutoSetValue = autoSetValue;
                _aop.Para.InsertOp = option;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Insert);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Insert(dalHelper.isOpenTrans);
                        break;
                    default:
                        ClearParameters();
                        string sql = _sqlCreate.GetInsertSql();
                        _isInsertCommand = true;
                        _option = option;
                        _aop.Para.IsSuccess = InsertOrUpdate(sql);
                        break;
                }
            }
            else if (option != InsertOp.None)
            {
                _Data = _aop.Para.Row;
                InitGlobalObject(false);
            }
            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Insert);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }
        #endregion

        #region ����
        /// <summary>
        ///  ��������[����where����ʱ���Զ����Դ�UI��ȡ]
        /// </summary>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     action.Set(Users.UserName,"·������");
        ///     action.Set(Users.ID,1);
        ///     action.Update();
        /// }
        /// </code></example>
        public bool Update()
        {
            return Update(null, true);
        }
        /// <summary>
        ///  ��������
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:[id=88 and name='·������']</param>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     action.Set(Users.UserName,"·������");
        ///     action.Update("id=1");
        /// }
        /// </code></example>
        public bool Update(object where)
        {
            return Update(where, false);
        }
        /// <summary>
        ///  ��������
        /// </summary>
        /// <param name="autoSetValue">�Ƿ��Զ���ȡֵ[�Զ��ӿؼ���ȡֵ,��Ҫ�ȵ���SetAutoPrefix��SetAutoParentControl�������ÿؼ�ǰ׺]</param>
        public bool Update(bool autoSetValue)
        {
            return Update(null, autoSetValue);
        }

        /// <summary>
        ///  ��������
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:[id=88 and name='·������']</param>
        /// <param name="autoSetValue">�Ƿ��Զ���ȡֵ[�Զ��ӿؼ���ȡֵ,��Ҫ�ȵ���SetAutoPrefix��SetAutoParentControl�������ÿؼ�ǰ׺]</param>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     action.SetAutoPrefix("txt","ddl");
        ///     action.Update("name='·������'",true);
        /// }
        /// </code></example>
        public bool Update(object where, bool autoSetValue)
        {
            CheckDisposed();
            if (autoSetValue)
            {
                _UI.GetAll(false);
            }
            if (where == null || Convert.ToString(where) == "")
            {
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.AutoSetValue = autoSetValue;
                _aop.Para.UpdateExpression = _sqlCreate.updateExpression;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Update);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Update(_sqlCreate.FormatWhere(where));
                        break;
                    default:
                        ClearParameters();
                        string sql = _sqlCreate.GetUpdateSql(where);
                        _aop.Para.IsSuccess = InsertOrUpdate(sql);
                        break;
                }
            }
            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Update);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }
        #endregion

        /// <summary>
        ///  ɾ������[����where����ʱ���Զ����Դ�UI��ȡ]
        /// </summary>
        public bool Delete()
        {
            return Delete(null);
        }
        /// <summary>
        ///  ɾ������
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:[id=88 and name='·������']</param>
        public bool Delete(object where)
        {
            CheckDisposed();
            if (where == null || Convert.ToString(where) == "")
            {
                _UI.PrimayAutoGetValue();
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Delete);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                string deleteField = AppConfig.DB.DeleteField;
                bool isToUpdate = !string.IsNullOrEmpty(deleteField) && _Data.Columns.Contains(deleteField);
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        string sqlWhere = _sqlCreate.FormatWhere(where);
                        if (isToUpdate)
                        {
                            _Data.Set(deleteField, true);
                            _aop.Para.IsSuccess = _noSqlAction.Update(sqlWhere);
                        }
                        else
                        {
                            _aop.Para.IsSuccess = _noSqlAction.Delete(sqlWhere);
                        }
                        break;
                    default:
                        ClearParameters();
                        string sql = isToUpdate ? _sqlCreate.GetDeleteToUpdateSql(where) : _sqlCreate.GetDeleteSql(where);
                        _aop.Para.IsSuccess = dalHelper.ExeNonQuery(sql, false) > 0;
                        break;
                }
            }
            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Delete);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.IsSuccess;
        }

        /// <summary>
        /// ѡ����������
        /// </summary>
        public MDataTable Select()
        {
            int count;
            return Select(0, 0, null, out count);
        }
        /// <summary>
        /// ����������ѯ��������
        /// </summary>
        /// <param name="where">where������:id>1</param>
        public MDataTable Select(object where)
        {
            int count;
            return Select(0, 0, where, out count);
        }
        public MDataTable Select(int topN, object where)
        {
            int count;
            return Select(0, topN, where, out count);
        }
        public MDataTable Select(int pageIndex, int pageSize)
        {
            int count;
            return Select(pageIndex, pageSize, null, out count);
        }
        public MDataTable Select(int pageIndex, int pageSize, object where)
        {
            int count;
            return Select(pageIndex, pageSize, where, out count);
        }
        /// <summary>
        /// ���ֲ����ܵ�ѡ��[��������ѯ,ѡ������ʱֻ���PageIndex/PageSize����Ϊ0]
        /// </summary>
        /// <param name="pageIndex">�ڼ�ҳ</param>
        /// <param name="pageSize">ÿҳ����[Ϊ0ʱĬ��ѡ������]</param>
        /// <param name="where"> ��ѯ����[�ɸ��� order by ���]</param>
        /// <param name="rowCount">���صļ�¼����</param>
        /// <returns>����ֵMDataTable</returns>
        public MDataTable Select(int pageIndex, int pageSize, object where, out int rowCount)
        {
            CheckDisposed();
            rowCount = 0;
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.PageIndex = pageIndex;
                _aop.Para.PageSize = pageSize;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                _aop.Para.SelectColumns = _sqlCreate.selectColumns;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Select);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                string primaryKey = SqlFormat.Keyword(_Data.Columns.FirstPrimary.ColumnName, dalHelper.dalType);//����������
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.Table = _noSqlAction.Select(pageIndex, pageSize, _sqlCreate.FormatWhere(where), out rowCount, _sqlCreate.selectColumns);
                        break;
                    default:
                        _aop.Para.Table = new MDataTable(_TableName.Contains("(") ? "SysDefaultCustomTable" : _TableName);
                        _aop.Para.Table.LoadRow(_Data);
                        ClearParameters();//------------------------�������
                        DbDataReader sdReader = null;
                        string whereSql = string.Empty;//�Ѹ�ʽ������ԭ��whereSql���
                        if (_sqlCreate != null)
                        {
                            whereSql = _sqlCreate.FormatWhere(where);
                        }
                        else
                        {
                            whereSql = SqlFormat.Compatible(where, dalHelper.dalType, dalHelper.Com.Parameters.Count == 0);
                        }
                        bool byPager = pageIndex > 0 && pageSize > 0;//��ҳ��ѯ(��һҳҲҪ��ҳ��ѯ����ΪҪ����������
                        if (byPager && AppConfig.DB.PagerBySelectBase && dalHelper.dalType == DalType.MsSql && !dalHelper.Version.StartsWith("08"))// || dalHelper.dalType == DalType.Oracle
                        {
                            #region �洢����ִ��
                            if (dalHelper.Com.Parameters.Count > 0)
                            {
                                dalHelper.debugInfo.Append(AppConst.HR + "error : select method deny call SetPara() method to add custom parameters!");
                            }
                            dalHelper.AddParameters("@PageIndex", pageIndex, DbType.Int32, -1, ParameterDirection.Input);
                            dalHelper.AddParameters("@PageSize", pageSize, DbType.Int32, -1, ParameterDirection.Input);
                            dalHelper.AddParameters("@TableName", _sqlCreate.GetSelectTableName(ref whereSql), DbType.String, -1, ParameterDirection.Input);

                            whereSql = _sqlCreate.AddOrderByWithCheck(whereSql, primaryKey);

                            dalHelper.AddParameters("@Where", whereSql, DbType.String, -1, ParameterDirection.Input);
                            sdReader = dalHelper.ExeDataReader("SelectBase", true);
                            #endregion
                        }
                        else
                        {
                            #region SQL����ҳִ��
                            if (byPager)
                            {
                                rowCount = Convert.ToInt32(dalHelper.ExeScalar(_sqlCreate.GetCountSql(whereSql), false));//��ҳ��ѯ�ȼ�������
                            }
                            if (!byPager || (rowCount > 0 && (pageIndex - 1) * pageSize < rowCount))
                            {
                                string sql = SqlCreateForPager.GetSql(dalHelper.dalType, dalHelper.Version, pageIndex, pageSize, whereSql, SqlFormat.Keyword(_TableName, dalHelper.dalType), rowCount, _sqlCreate.GetColumnsSql(), primaryKey, _Data.PrimaryCell.Struct.IsAutoIncrement);
                                sdReader = dalHelper.ExeDataReader(sql, false);
                            }
                            #endregion
                        }
                        if (sdReader != null)
                        {
                            _aop.Para.Table.ReadFromDbDataReader(sdReader);//�ڲ��йرա�
                            if (!byPager)
                            {
                                rowCount = _aop.Para.Table.Rows.Count;
                            }
                            else if (dalHelper.dalType == DalType.MsSql && AppConfig.DB.PagerBySelectBase)
                            {
                                rowCount = dalHelper.ReturnValue;
                            }
                            _aop.Para.Table.RecordsAffected = rowCount;
                        }
                        else
                        {
                            _aop.Para.Table.Rows.Clear();//Ԥ��֮ǰ�Ĳ������������һ�������С�
                        }
                        ClearParameters();//------------------------�������
                        break;
                }
            }
            else if (_aop.Para.Table.RecordsAffected > 0)
            {
                rowCount = _aop.Para.Table.RecordsAffected;//���ؼ�¼����
            }
            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.Select);
            }
            _aop.Para.Table.TableName = TableName;//Aop��Json�������ʱ�ᶪʧ������
            _aop.Para.Table.Conn = _Data.Conn;
            if (_sqlCreate != null)
            {
                _sqlCreate.selectColumns = null;
            }
            return _aop.Para.Table;
        }

        /// <summary>
        /// �������������ѡ��[����where����ʱ���Զ����Դ�UI��ȡ]
        /// </summary>
        public bool Fill()
        {
            return Fill(null);
        }

        /// <summary>
        /// �������[������ѡ��]���������з�Nullֵ��״̬����Ϊ1��
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:"id=88 or name='·������'"</param>
        /// <example><code>
        /// using(MAction action=new MAction(TableNames.Users))
        /// {
        ///     if(action.Fill("name='·������'")) //����action.Fill(888) ���� action.Fill(id=888)
        ///     {
        ///         action.SetTo(labUserName);
        ///     }
        /// }
        /// </code></example>
        public bool Fill(object where)
        {
            CheckDisposed();
            if (where == null || Convert.ToString(where) == "")
            {
                _UI.PrimayAutoGetValue();
                where = _sqlCreate.GetPrimaryWhere();
            }
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                _aop.Para.SelectColumns = _sqlCreate.selectColumns;
                //_aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.Fill);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.IsSuccess = _noSqlAction.Fill(_sqlCreate.FormatWhere(where));
                        break;
                    default:
                        ClearParameters();
                        MDataTable mTable = dalHelper.ExeDataReader(_sqlCreate.GetTopOneSql(where), false);
                        // dalHelper.ResetConn();//����Slave
                        if (mTable != null && mTable.Rows.Count > 0)
                        {
                            _Data.LoadFrom(mTable.Rows[0], RowOp.None, true);//setselectcolumn("aa as bb")ʱ
                            _aop.Para.IsSuccess = true;
                        }
                        else
                        {
                            _aop.Para.IsSuccess = false;
                        }
                        break;
                }
            }
            else if (_aop.Para.IsSuccess)
            {
                _Data.LoadFrom(_aop.Para.Row, RowOp.None, true);
            }

            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.Para.Row = _Data;
                _aop.End(Aop.AopEnum.Fill);
            }
            if (_aop.Para.IsSuccess)
            {
                if (_sqlCreate.selectColumns != null)
                {
                    string name;
                    string[] items;
                    foreach (object columnName in _sqlCreate.selectColumns)
                    {
                        items = columnName.ToString().Split(' ');
                        name = items[items.Length - 1];
                        MDataCell cell = _Data[name];
                        if (cell != null)
                        {
                            cell.State = 1;
                        }
                    }
                    items = null;
                }
                else
                {
                    _Data.SetState(1, BreakOp.Null);//��ѯʱ����λ״̬Ϊ1
                }
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            if (_sqlCreate != null)
            {
                _sqlCreate.selectColumns = null;
            }
            return _aop.Para.IsSuccess;
        }
        /// <summary>
        /// ���ؼ�¼����
        /// </summary>
        public int GetCount()
        {
            return GetCount(null);
        }
        /// <summary>
        /// ���ؼ�¼����
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:[id=88 and name='·������']</param>
        public int GetCount(object where)
        {
            CheckDisposed();
            AopResult aopResult = AopResult.Default;
            if (_aop.IsCustomAop)
            {
                _aop.Para.MAction = this;
                _aop.Para.TableName = _sourceTableName;
                _aop.Para.Row = _Data;
                _aop.Para.Where = where;
                // _aop.Para.AopPara = aopPara;
                _aop.Para.IsTransaction = dalHelper.isOpenTrans;
                aopResult = _aop.Begin(Aop.AopEnum.GetCount);
            }
            if (aopResult == AopResult.Default || aopResult == AopResult.Continue)
            {
                switch (dalHelper.dalType)
                {
                    case DalType.Txt:
                    case DalType.Xml:
                        _aop.Para.RowCount = _noSqlAction.GetCount(_sqlCreate.FormatWhere(where));
                        break;
                    default:
                        ClearParameters();//���ϵͳ����
                        string countSql = _sqlCreate.GetCountSql(where);
                        object result = dalHelper.ExeScalar(countSql, false);

                        _aop.Para.IsSuccess = result != null;
                        if (_aop.Para.IsSuccess)
                        {
                            _aop.Para.RowCount = Convert.ToInt32(result);
                        }
                        else
                        {
                            _aop.Para.RowCount = -1;
                        }

                        //ClearSysPara(); //����ڲ��Զ������[FormatWhere���Զ������]
                        break;
                }
            }
            if (_aop.IsCustomAop && (aopResult == AopResult.Break || aopResult == AopResult.Continue))
            {
                _aop.End(Aop.AopEnum.GetCount);
            }
            if (dalHelper.recordsAffected == -2)
            {
                OnError();
            }
            return _aop.Para.RowCount;
        }
        /// <summary>
        /// �Ƿ����ָ������������
        /// </summary>
        /// <param name="where">where����,��ֱ�Ӵ�id��ֵ��:[88],������where������:[id=88 and name='·������']</param>
        public bool Exists(object where)
        {
            CheckDisposed();
            switch (dalHelper.dalType)
            {
                case DalType.Txt:
                case DalType.Xml:
                    return _noSqlAction.Exists(_sqlCreate.FormatWhere(where));
                default:
                    return GetCount(where) > 0;
            }
        }
        #endregion

        #region ��������



        /// <summary>
        /// ȡֵ
        /// </summary>
        public T Get<T>(object key)
        {
            return _Data.Get<T>(key);
        }
        /// <summary>
        /// ȡֵ
        /// </summary>
        /// <param name="key">�ֶ���</param>
        /// <param name="defaultValue">ֵΪNullʱ��Ĭ���滻ֵ</param>
        public T Get<T>(object key, T defaultValue)
        {
            return _Data.Get<T>(key, defaultValue);
        }
        /// <summary>
        /// ����ֵ,����:[action.Set(TableName.ID,10);]
        /// </summary>
        /// <param name="key">�ֶ�����,����ö����:[TableName.ID]</param>
        /// <param name="value">Ҫ���ø��ֶε�ֵ</param>
        /// <example><code>
        /// setʾ����action.Set(Users.UserName,"·������");
        /// getʾ����int id=action.Get&lt;int&gt;(Users.ID);
        /// </code></example>
        public MAction Set(object key, object value)
        {
            MDataCell cell = _Data[key];
            if (cell != null)
            {
                cell.Value = value;
            }
            else
            {
                dalHelper.debugInfo.Append(AppConst.HR + "Alarm : can't find the ColumnName:" + key);
            }
            return this;
        }
        /// <summary>
        /// ����(Update)�������Զ�����ʽ���á�
        /// </summary>
        /// <param name="updateExpression">����a�ֶ�ֵ�Լ�1��"a=a+1"</param>
        public MAction SetExpression(string updateExpression)
        {
            _sqlCreate.updateExpression = updateExpression;
            return this;
        }
        List<AopCustomDbPara> customParaNames = new List<AopCustomDbPara>();
        /// <summary>
        /// ����������[��Where����Ϊ������(�磺name=@name)���ʱʹ��]
        /// </summary>
        /// <param name="paraName">��������</param>
        /// <param name="value">����ֵ</param>
        /// <param name="dbType">��������</param>
        public MAction SetPara(object paraName, object value, DbType dbType)
        {
            if (dalHelper.AddParameters(Convert.ToString(paraName), value, dbType, -1, ParameterDirection.Input))
            {
                AopCustomDbPara para = new AopCustomDbPara();
                para.ParaName = Convert.ToString(paraName).Replace(":", "").Replace("@", "");
                para.Value = value;
                para.ParaDbType = dbType;
                customParaNames.Add(para);
                if (_aop.IsCustomAop)
                {
                    _aop.Para.CustomDbPara = customParaNames;
                }
            }
            return this;
        }
        /// <summary>
        /// ����������[������������б�]
        /// </summary>
        /// <param name="customParas">Aop����ʹ�ã��������������</param>
        public MAction SetPara(List<AopCustomDbPara> customParas)
        {
            if (customParas != null && customParas.Count > 0)
            {
                foreach (AopCustomDbPara para in customParas)
                {
                    SetPara(para.ParaName, para.Value, para.ParaDbType);
                }
            }
            return this;
        }
        /// <summary>
        /// ���(SetPara���õ�)�Զ������
        /// </summary>
        public void ClearPara()
        {
            if (customParaNames.Count > 0)
            {
                if (dalHelper != null && dalHelper.Com.Parameters.Count > 0)
                {
                    string paraName = string.Empty;
                    foreach (AopCustomDbPara item in customParaNames)
                    {
                        for (int i = dalHelper.Com.Parameters.Count - 1; i > -1; i--)
                        {
                            if (string.Compare(dalHelper.Com.Parameters[i].ParameterName.TrimStart(dalHelper.Pre), item.ParaName.ToString()) == 0)
                            {
                                dalHelper.Com.Parameters.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
                customParaNames.Clear();
            }
        }
        /// <summary>
        /// ����ڲ�ϵͳ����Ĳ���
        /// </summary>
        /// <param name="withSysPara"></param>
        //private void ClearSysPara()
        //{
        //    if (customParaNames.Count > 0)
        //    {
        //        string paraName = string.Empty;
        //        for (int i = customParaNames.Count - 1; i >= 0; i--)
        //        {
        //            paraName = _DalHelper.Pre + customParaNames[i].ParaName;
        //            if (customParaNames[i].IsSysPara && _DalHelper.Com.Parameters.Contains(paraName))
        //            {
        //                _DalHelper.Com.Parameters.Remove(_DalHelper.Com.Parameters[paraName]);
        //            }
        //        }
        //    }
        //}
        /// <summary>
        /// ���ϵͳ����[�����Զ������]
        /// </summary>
        private void ClearParameters()
        {
            if (dalHelper != null)
            {
                if (customParaNames.Count > 0)//�����Զ������
                {
                    if (dalHelper.Com.Parameters.Count > 0)
                    {
                        bool isBreak = false;
                        for (int i = dalHelper.Com.Parameters.Count - 1; i > -1; i--)
                        {
                            for (int j = 0; j < customParaNames.Count; j++)
                            {
                                if (string.Compare(dalHelper.Com.Parameters[i].ParameterName.TrimStart(dalHelper.Pre), customParaNames[j].ParaName.ToString()) == 0)
                                {
                                    isBreak = true;
                                }
                            }
                            if (!isBreak)
                            {
                                dalHelper.Com.Parameters.RemoveAt(i);
                                isBreak = false;
                            }
                        }
                    }
                }
                else
                {
                    dalHelper.ClearParameters();
                }
            }
        }

        /// <summary>
        /// �����������ڵ���ʹ��ʱ��ѯָ������[���ú��ʹ��Fill��Select����]
        /// ��ʾ����ҳ��ѯʱ�������������б���ָ��ѡ��
        /// </summary>
        /// <param name="columnNames">�����ö������[����Fill��Select��,�������������]</param>
        public MAction SetSelectColumns(params object[] columnNames)
        {
            _sqlCreate.selectColumns = columnNames;
            return this;
        }

        /// <summary>
        /// ����Ԫ���������where������
        /// </summary>
        /// <param name="isAnd">trueΪand���ӣ���֮Ϊor����</param>
        /// <param name="cells">��Ԫ��</param>
        /// <returns></returns>
        public string GetWhere(bool isAnd, params MDataCell[] cells)
        {
            return SqlCreate.GetWhere(DalType, isAnd, cells);
        }

        /// <summary>
        /// ����Ԫ���������and���ӵ�where������
        /// </summary>
        /// <param name="cells">��Ԫ��</param>
        /// <returns></returns>
        public string GetWhere(params MDataCell[] cells)
        {
            return SqlCreate.GetWhere(DalType, cells);
        }



        #endregion

        #region �������
        /// <summary>
        /// �������񼶱�
        /// </summary>
        /// <param name="level"></param>
        public MAction SetTransLevel(IsolationLevel level)
        {
            dalHelper.tranLevel = level;
            return this;
        }

        /// <summary>
        /// ��ʼ����
        /// </summary>
        public void BeginTransation()
        {
            dalHelper.isOpenTrans = true;
        }
        /// <summary>
        /// �ύ��������[Ĭ�ϵ���Close/Disponseʱ���Զ�����]
        /// �����Ҫ��ǰ��������,�ɵ��ô˷���
        /// </summary>
        public bool EndTransation()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                return dalHelper.EndTransaction();
            }
            return false;
        }
        /// <summary>
        /// ����ع�
        /// </summary>
        public bool RollBack()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                return dalHelper.RollBack();
            }
            return false;
        }
        #endregion

        #region IDisposable ��Ա

        /// <summary>
        /// �ͷ���Դ
        /// </summary>
        public void Dispose()
        {
            hasDisposed = true;
            if (dalHelper != null)
            {
                if (!dalHelper.IsOnExceptionEventNull)
                {
                    dalHelper.OnExceptionEvent -= new DbBase.OnException(_DataSqlHelper_OnExceptionEvent);
                }
                _debugInfo = dalHelper.debugInfo.ToString();
                dalHelper.Dispose();
                dalHelper = null;
                if (_sqlCreate != null)
                {
                    _sqlCreate = null;
                }
            }
            if (_noSqlAction != null)
            {
                _noSqlAction.Dispose();
            }
            if (_aop != null)
            {
                _aop = null;
            }
        }
        internal void OnError()
        {
            if (dalHelper != null && dalHelper.isOpenTrans)
            {
                Dispose();
            }
        }
        bool hasDisposed = false;
        private void CheckDisposed()
        {
            if (hasDisposed)
            {
                Error.Throw("The current object 'MAction' has been disposed");
            }
        }
        #endregion


    }
    //AOP ����
    public partial class MAction
    {
        #region Aop����
        private InterAop _aop = new InterAop();
        //private IAop _aop = Aop.InterAop.Instance;//�����
        //private AopInfo _aopInfo = new AopInfo();
        /// <summary>
        /// ��ʱ����Aop�������л���Ļ�ԭ��
        /// </summary>
        // Aop.IAop _aopBak = null;
        /// <summary>
        /// ȡ��Aop����Aop����ģ��ʹ��MActionʱ�������
        /// </summary>
        public MAction SetAopOff()
        {
            _aop.IsCustomAop = false;
            //if (_aop.IsCustomAop)
            //{
            //    _aopBak = _aop;//���úñ��ݡ�
            //    _aop = Aop.InterAop.Instance;
            //    _aop.IsCustomAop = false;
            //}
            return this;
        }
        /// <summary>
        /// �ָ�Ĭ�����õ�Aop��
        /// </summary>
        public MAction SetAopOn()
        {
            _aop.IsCustomAop = true;
            //if (!_aop.IsCustomAop)
            //{
            //    SetAop(_aopBak);
            //}
            return this;
        }
        /// <summary>
        /// ��������ע���µ�Aop��һ������²���Ҫ�õ���
        /// </summary>
        /// <param name="aop"></param>
        //private MAction SetAop(Aop.IAop aop)
        //{
        //    _aop = aop;
        //    _aop.IsCustomAop = true;
        //    return this;
        //}
        /// <summary>
        /// ��Ҫ���ݶ���Ĳ�����Aopʹ��ʱ�����á�
        /// </summary>
        /// <param name="para"></param>
        public MAction SetAopPara(object para)
        {
            _aop.Para.AopPara = para;
            return this;
        }

        #endregion
    }
    //UI ����
    public partial class MAction
    {
        private MActionUI _UI;
        /// <summary>
        /// UI����
        /// </summary>
        public MActionUI UI
        {
            get
            {
                return _UI;
            }
        }
    }

}
