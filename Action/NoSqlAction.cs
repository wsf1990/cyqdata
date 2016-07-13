using System;
using System.Collections.Generic;
using System.Text;
using CYQ.Data.Table;
using CYQ.Data.SQL;

using System.IO;
using CYQ.Data.Tool;


namespace CYQ.Data
{
    internal class NoSqlAction : IDisposable
    {
        /// <summary>
        /// ���þ�̬������ÿ���������º���ã�
        /// </summary>
        internal static void ResetStaticVar()
        {

            _tableList.Clear();
            _tableList = null;//��̬�ֵ���ΪNull���ܼ�ʱ�ͷ��ڴ档
            _tableList = new MDictionary<string, MDataTable>(3);//���³�ʼ����
            _needToSaveState.Clear();
            _lockNextIDObj.Clear();
            _lockWriteTxtObj.Clear();
            _maxID.Clear();
            //_InsertRows.Clear();
            //_InsertRows = null;//��Null
            //_InsertRows = new List<MDataRow>();
            _lastWriteTimeUtc = DateTime.Now;
        }
        private static MDictionary<string, MDataTable> _tableList = new MDictionary<string, MDataTable>(3);//�ڴ����ݿ�
        private static readonly object _lockTableListObj = new object();
        /// <summary>
        ///  �Ƿ���Ҫ���£�0δ���£�1������[�����������]��2����ɾ�������[���±���]
        /// </summary>
        private static MDictionary<string, int> _needToSaveState = new MDictionary<string, int>(StringComparer.OrdinalIgnoreCase);//��Ҫ����[ȫ�ֵĿ�����Ч������]
        private static MDictionary<string, object> _lockNextIDObj = new MDictionary<string, object>(StringComparer.OrdinalIgnoreCase);//������ID��
        private static MDictionary<string, object> _lockWriteTxtObj = new MDictionary<string, object>(StringComparer.OrdinalIgnoreCase);//�ļ�д����
        private static MDictionary<string, int> _maxID = new MDictionary<string, int>(StringComparer.OrdinalIgnoreCase);//��ǰ������ID
        private List<MDataRow> _insertRows = new List<MDataRow>();//�²���ļ��ϣ���������MDataTable������
        /// <summary>
        /// ����д��ʱ��
        /// </summary>
        private static DateTime _lastWriteTimeUtc = DateTime.UtcNow;
        private MDataTable _Table = null;
        private MDataTable Table
        {
            get
            {
                if (_Table != null)
                {
                    return _Table;
                }
                else if (_tableList.ContainsKey(_FileFullName))
                {
                    _Table = _tableList[_FileFullName];
                    return _Table;
                }
                else
                {
                    switch (_DalType)
                    {
                        case DalType.Txt:
                            _Table = MDataTable.CreateFrom(_FileFullName, _Row.Columns);
                            break;
                        case DalType.Xml:
                            _Table = MDataTable.CreateFromXml(_FileFullName, _Row.Columns);
                            break;
                    }
                    if (_Table == null || _Table.Columns.Count == 0)
                    {
                        Error.Throw("MDataTable can't load data from file : " + _FileFullName);
                    }
                    //���������п���json��ĳЩ������ΪNull
                    //                foreach (MCellStruct rowST in _Row.Columns)
                    //                {
                    //                    foreach (MCellStruct tableST in _Table.Columns)
                    //{

                    //}
                    //                    if (!_Table.Columns.Contains(cst.ColumnName))
                    //                    {
                    //                        _Table.Columns.Add(cst);
                    //                    }
                    //                    else if(cst.SqlType!=_Table.col
                    //                    {

                    //                    }
                    //                }
                    _lastWriteTimeUtc = new IOInfo(_FileFullName).LastWriteTimeUtc;
                    if (_Table.Rows.Count > 0)
                    {
                        ////�����⡣
                        //if (_Table.Columns[0].SqlType == System.Data.SqlDbType.Int)
                        //{
                        //    _Table.Rows.Sort("order by " + _Table.Columns[0].ColumnName + " asc");
                        //}
                        lock (_lockTableListObj)
                        {
                            if (!_tableList.ContainsKey(_FileFullName))
                            {
                                _tableList.Add(_FileFullName, _Table);
                            }
                        }
                    }
                    return _Table;
                }
            }
        }

        private int maxID
        {
            get
            {
                return _maxID[_FileFullName];
            }
            set
            {
                _maxID[_FileFullName] = value;
            }
        }
        private object lockNextIDobj
        {
            get
            {
                return _lockNextIDObj[_FileFullName];
            }
        }
        private object lockWriteTxtobj
        {
            get
            {
                return _lockWriteTxtObj[_FileFullName];
            }
        }
        /// <summary>
        /// ��һ��������ID
        /// </summary>
        private int NextID
        {
            get
            {
                lock (lockNextIDobj)
                {
                    if (maxID > 0)
                    {
                        maxID++;
                    }
                    else if (DataType.GetGroup(Table.Columns.FirstPrimary.SqlType) == 1)//����ID����int��Ч
                    {
                        try
                        {
                            if (Table.Rows.Count > 0)
                            {
                                int lastIndex = _Table.Rows.Count - 1;
                                do
                                {
                                    if (lastIndex >= 0)
                                    {
                                        if (_Table.Rows[lastIndex][0].IsNull)
                                        {
                                            lastIndex--;
                                        }
                                        else
                                        {
                                            maxID = Convert.ToInt32(_Table.Rows[lastIndex][0].Value) + 1;
                                        }
                                    }
                                    else
                                    {
                                        maxID = 1;
                                    }
                                }
                                while (maxID == 0);

                            }
                            else
                            {
                                maxID = 1;
                            }
                        }
                        catch
                        {
                        }
                    }
                    else
                    {
                        Error.Throw("Increment id only allow use for int type");
                    }

                }
                return maxID;
            }
        }
        /// <summary>
        /// ����·���������ļ�����
        /// </summary>
        string _FileFullName = string.Empty;
        /// <summary>
        /// ������·�����ļ�����(����չ��)
        /// </summary>
        string _FileName = string.Empty;
        internal MDataRow _Row;//MAction�е�Row
        DalType _DalType = DalType.None;
        public NoSqlAction(ref MDataRow row, string fileName, string filePath, DalType dalType)
        {
            Reset(ref row, fileName, filePath, dalType);
        }
        /// <summary>
        /// �л���
        /// </summary>
        /// <param name="row">�����нṹ</param>
        /// <param name="fileName">�ļ�����</param>
        /// <param name="filePath">�ļ�·��</param>
        /// <param name="dalType">��������</param>
        public void Reset(ref MDataRow row, string fileName, string filePath, DalType dalType)
        {
            _insertRows.Clear();//�л����ʱ�����á�
            _Row = row;
            string exName = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(exName))
            {
                switch (dalType)
                {
                    case DalType.Txt:
                        _FileName = fileName + ".txt";
                        break;
                    case DalType.Xml:
                        _FileName = fileName + ".xml";
                        break;
                }
            }
            else
            {
                _FileName = fileName;
            }
            _FileFullName = filePath + _FileName;
            _DalType = dalType;
            if (!_needToSaveState.ContainsKey(_FileFullName))
            {
                _needToSaveState.Add(_FileFullName, 0);
            }
            if (!_lockNextIDObj.ContainsKey(_FileFullName))
            {
                _lockNextIDObj.Add(_FileFullName, new object());
            }
            if (!_lockWriteTxtObj.ContainsKey(_FileFullName))
            {
                _lockWriteTxtObj.Add(_FileFullName, new object());
            }
            if (!_maxID.ContainsKey(_FileFullName))
            {
                _maxID.Add(_FileFullName, 0);
            }
        }
        public bool Delete(object where)
        {
            int count = 0;
            return Delete(where, out count);
        }
        internal bool Delete(object where, out int count)
        {
            count = -1;
            if (!string.IsNullOrEmpty(Convert.ToString(where)))
            {
                List<MDataRow> rowList = Table.FindAll(where);
                if (rowList != null)
                {
                    count = rowList.Count;
                    if (count > 0)
                    {
                        for (int i = rowList.Count - 1; i >= 0; i--)
                        {
                            Table.Rows.Remove(rowList[i]);
                        }
                        _needToSaveState[_FileFullName] = 2;
                        return true;
                    }
                }
            }
            return false;
        }
        public bool Insert(bool isOpenTrans)
        {
            MDataCell cell = _Row.PrimaryCell;
            if (IsCanDoInsertCheck((cell.IsNullOrEmpty || cell.Struct.IsAutoIncrement || cell.Struct.IsPrimaryKey) ? 1 : 0))
            {
                //�ж��Ƿ���Ҫ����������ID
                if (!cell.Struct.IsCanNull && (cell.Struct.IsAutoIncrement || cell.Struct.IsPrimaryKey))
                {
                    int groupID = DataType.GetGroup(cell.Struct.SqlType);
                    string existWhere = cell.ColumnName + (groupID == 1 ? "={0}" : "='{0}'");
                    if (cell.IsNull || cell.cellValue.State == 0 || cell.strValue == "0" || Exists(string.Format(existWhere, cell.Value)))//��������ڣ�����ID�ظ�
                    {
                        switch (groupID)
                        {
                            case 1:
                                cell.Value = NextID;
                                break;
                            case 4:
                                cell.Value = Guid.NewGuid();
                                break;
                            default:
                                return (bool)Error.Throw("first column value can't be null");
                        }
                    }
                    if (groupID == 1 || groupID == 4)//�ټ���Ƿ��Ѵ���
                    {
                        if (!isOpenTrans && Exists(string.Format(existWhere, cell.Value)))
                        {
                            Error.Throw("first column value must be unique:(" + cell.ColumnName + ":" + cell.Value + ")");
                        }
                        else if (groupID == 1)
                        {
                            maxID = (int)cell.Value;
                        }
                    }
                }

                CheckFileChanged(true);
                _Row.SetState(0);//״̬���ã������ظ�ʹ�ò��룡
                MDataRow newRow = Table.NewRow(true);
                newRow.LoadFrom(_Row);
                _insertRows.Add(newRow);//�������ã�
                _needToSaveState[_FileFullName] = _needToSaveState[_FileFullName] > 1 ? 2 : 1;
                return true;
            }
            return false;
        }
        public bool Update(object where)
        {
            int count = 0;
            return Update(where, out count);
        }
        public bool Update(object where, out int count)
        {
            count = -1;
            CheckFileChanged(true);
            List<MDataRow> rowList = Table.FindAll(where);
            if (rowList != null)
            {
                count = rowList.Count;
                if (count > 0)
                {
                    for (int i = rowList.Count - 1; i >= 0; i--)
                    {
                        rowList[i].LoadFrom(_Row, RowOp.Update, false);
                        rowList[i].SetState(0);//״̬����
                    }
                    _Row.SetState(0);
                    _needToSaveState[_FileFullName] = 2;
                    return true;
                }
            }
            return false;
        }
        public bool Fill(object where)
        {
            CheckFileChanged(true);
            MDataRow row = Table.FindRow(where);
            if (row != null)
            {

                _Row.LoadFrom(row);
                _Row.SetState(0);//��ѯʱ�������ᶨλ״̬Ϊ1
                return true;
            }
            return false;
        }
        public int GetCount(object where)
        {
            CheckFileChanged(true);
            return Table.GetCount(where);
        }
        public bool Exists(object where)
        {
            CheckFileChanged(true);
            return Table.FindRow(where) != null;
        }
        public MDataTable Select(int pageIndex, int pageSize, object where, out int rowCount, params object[] selectColumns)
        {
            CheckFileChanged(true);
            MDataTable dt = Table.Select(pageIndex, pageSize, where, selectColumns);
            rowCount = dt.RecordsAffected;
            return dt;
        }

        #region ��������
        private bool IsCanDoInsertCheck(int start)
        {
            bool isCanDo = false;
            for (int i = start; i < _Row.Count; i++)
            {
                if (_Row[i].cellValue.State == 0 && !_Row[i].IsNull)
                {
                    _Row[i].Value = null;
                }
                if (!_Row[i].IsNullOrEmpty)
                {
                    isCanDo = true;
                }
                else if (Convert.ToString(_Row[i].Struct.DefaultValue).Length > 0)
                {
                    _Row[i].SetDefaultValueToValue();
                    if (!_Row[i].IsNullOrEmpty)
                    {
                        isCanDo = true;
                    }
                }
                else if (!_Row[i].Struct.IsCanNull)
                {
                    Error.Throw("Column [" + _Row[i].ColumnName + "] 's value can't be null or empty ! (tip:column property:iscannull=false)");
                }
            }
            return isCanDo;
        }
        #endregion

        #region IDisposable ��Ա

        public void Dispose()
        {
            int state = _needToSaveState[_FileFullName];
            if (state > 0)
            {
                bool isFirstAddRow = (Table.Rows.Count - _insertRows.Count) == 0;//������״����������ݡ�
                if (state > 1 || isFirstAddRow || _DalType == DalType.Xml || _insertRows.Count == 0)
                {
                    Save();
                }
                else//�ı����в���
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < _insertRows.Count; i++)
                    {
                        sb.Append(",\r\n" + _insertRows[i].ToJson());
                    }
                    _insertRows.Clear();//����
                    if (!Tool.IOHelper.Append(_FileFullName, sb.ToString()))
                    {
                        Save();//ʧ�ܣ������³���д�룡
                    }
                }
                _needToSaveState[_FileFullName] = 0;//����Ϊ0
                CheckFileChanged(false);//ͨ�������������޸�ʱ�䡣
            }
        }
        /// <summary>
        /// ����ļ��Ƿ��ѱ��޸Ĺ�
        /// </summary>
        /// <param name="isNeedToReloadTable"></param>
        private void CheckFileChanged(bool isNeedToReloadTable)
        {
            if (isNeedToReloadTable && IOHelper.IsLastFileWriteTimeChanged(_FileFullName, ref _lastWriteTimeUtc))//�Ѿ����޸Ĺ�
            {
                if (_tableList.ContainsKey(_FileFullName))
                {
                    try
                    {
                        _tableList[_FileFullName].Rows.Clear();
                        _tableList.Remove(_FileFullName);
                    }
                    catch// (Exception err)
                    {

                    }

                }
                _Table = null;//��Ҫ���¼������ݡ�
            }

        }
        private void Save()
        {
            try
            {
                string text = string.Empty;

                if (string.IsNullOrEmpty(text))
                {
                    text = _DalType == DalType.Txt ? Table.ToJson(false, true).Replace("},{", "},\r\n{").Trim('[', ']') : Table.ToXml();
                }
                int tryAgainCount = 3;
                bool isError = false;
                do
                {
                    lock (lockWriteTxtobj)
                    {
                        try
                        {

                            IOHelper.Write(_FileFullName, text);
                            tryAgainCount = 0;
                        }
                        catch
                        {
                            tryAgainCount--;
                            isError = true;
                        }
                    }
                    if (isError)
                    {
                        System.Threading.Thread.Sleep(20 * (4 - tryAgainCount));
                    }

                }
                while (tryAgainCount > 0);
            }
            catch (Exception err)
            {
                Log.WriteLogToTxt(err);
            }
        }
        #endregion
    }
}
