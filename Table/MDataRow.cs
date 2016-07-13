using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using System.ComponentModel;
using CYQ.Data.SQL;
using CYQ.Data.Tool;
using CYQ.Data.Extension;
using System.Reflection;
using System.Collections.Specialized;
using CYQ.Data.UI;


namespace CYQ.Data.Table
{
    /// <summary>
    /// һ�м�¼
    /// </summary>
    [Serializable]
    public partial class MDataRow : List<MDataCell>, IDataRecord
    {
        public MDataRow(MDataTable dt)
        {
            if (dt != null)
            {
                Table = dt;
                MCellStruct cellStruct;
                foreach (MCellStruct item in dt.Columns)
                {
                    cellStruct = item;
                    base.Add(new MDataCell(ref cellStruct));
                }
                base.Capacity = dt.Columns.Count;
            }
        }
        public MDataRow(MDataColumn mdc)
        {
            Table.Columns.AddRange(mdc);
            base.Capacity = mdc.Count;
        }
        public MDataRow()
            : base()
        {

        }
        /// <summary>
        /// ��ȡ��ͷ
        /// </summary>
        public MDataColumn Columns
        {
            get
            {
                return Table.Columns;
            }
        }

        public static implicit operator MDataRow(DataRow row)
        {
            if (row == null)
            {
                return null;
            }
            MDataRow mdr = new MDataRow();
            mdr.TableName = row.Table.TableName;
            DataColumnCollection columns = row.Table.Columns;
            if (columns != null && columns.Count > 0)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    MCellStruct cellStruct = new MCellStruct(columns[i].ColumnName, DataType.GetSqlType(columns[i].DataType), columns[i].ReadOnly, columns[i].AllowDBNull, columns[i].MaxLength);
                    cellStruct.DefaultValue = columns[i].DefaultValue;
                    mdr.Add(new MDataCell(ref cellStruct, row[i]));
                }
            }

            return mdr;
        }
        private string _Conn = string.Empty;
        /// <summary>
        /// �����������ݿ�����������[��MAction�Ӵ��мܹ����м���ʱ,������������,���ȳ�ΪĬ�ϵ����ݿ�����]
        /// </summary>
        public string Conn
        {
            get
            {
                if (_Table != null)
                {
                    return _Table.Conn;
                }
                else if (string.IsNullOrEmpty(_Conn))
                {
                    return AppConfig.DB.DefaultConn;
                }
                return _Conn;
            }
            set
            {
                if (_Table != null)
                {
                    _Table.Conn = value;
                }
                else
                {
                    _Conn = value;
                }
            }
        }
        private string _TableName;
        /// <summary>
        /// ԭʼ����[δ���������ݿ���ݴ���]
        /// </summary>
        public string TableName
        {
            get
            {
                if (_Table != null)
                {
                    return _Table.TableName;
                }
                return _TableName;
            }
            set
            {
                if (_Table != null)
                {
                    _Table.TableName = value;
                }
                else
                {
                    _TableName = value;
                }
            }
        }
        /// <summary>
        /// ����ö��������
        /// </summary>
        public MDataCell this[object field]
        {
            get
            {
                if (field is int || (field is Enum && AppConfig.IsEnumToInt))
                {
                    int index = (int)field;
                    if (base.Count > index)
                    {
                        return base[index];
                    }
                }
                else if (field is IField)
                {
                    IField iFiled = field as IField;
                    if (iFiled.ColID > -1)
                    {
                        return base[iFiled.ColID];
                    }
                    return this[iFiled.Name];
                }
                return this[field.ToString()];
            }
        }
        public MDataCell this[string key]
        {
            get
            {
                int index = Columns.GetIndex(key);//���¼�����Ƿ�һ�¡�
                if (index > -1)
                {
                    return base[index];
                }
                return null;
            }
        }
        private MDataTable _Table;
        /// <summary>
        /// ��ȡ����ӵ����ܹ��� MDataTable��
        /// </summary>
        public MDataTable Table
        {
            get
            {
                if (_Table == null)
                {
                    _Table = new MDataTable(_TableName);
                    if (this.Count > 0)
                    {
                        foreach (MDataCell cell in this)
                        {

                            _Table.Columns.Add(cell.Struct);
                        }
                    }

                    _Table.Rows.Add(this);
                }
                return _Table;
            }
            internal set
            {
                _Table = value;
            }
        }
        /// <summary>
        /// ͨ��һ����������ȡ�����ô��е�����ֵ��
        /// </summary>
        public object[] ItemArray
        {
            get
            {
                object[] values = new object[Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = this[i].Value;
                }
                return values;
            }
        }
        private string _RowError;
        /// <summary>
        /// ��ȡ�������е��Զ������˵����
        /// </summary>
        public string RowError
        {
            get { return _RowError; }
            set { _RowError = value; }
        }



        /// <summary>
        /// ��ȡ��һ���ؼ�������
        /// </summary>
        public MDataCell PrimaryCell
        {
            get
            {
                return JointPrimaryCell[0];
            }
        }
        private List<MDataCell> _JointPrimaryCell;
        /// <summary>
        /// ��ȡ���������б����ж��������
        /// </summary>
        public List<MDataCell> JointPrimaryCell
        {
            get
            {
                if (_JointPrimaryCell == null && Columns.Count > 0)
                {
                    _JointPrimaryCell = new List<MDataCell>(Columns.JointPrimary.Count);
                    foreach (MCellStruct st in Columns.JointPrimary)
                    {
                        _JointPrimaryCell.Add(this[st.ColumnName]);
                    }
                }
                return _JointPrimaryCell;
            }
        }

        /// <summary>
        /// �˷�����Emit������
        /// </summary>
        public object GetItemValue(int index)//����Public
        {
            MDataCell cell = this[index];
            if (cell == null || cell.Value == null || cell.Value == DBNull.Value)
            {
                return null;
            }
            return cell.Value;
        }
        /// <summary>
        /// ��������ֵ
        /// </summary>
        /// <returns></returns>
        public object[] GetItemValues()
        {
            object[] values = new object[Columns.Count];
            for (int i = 0; i < this.Count; i++)
            {
                values[i] = this[i].Value;
            }
            return values;
        }
        /// <summary>
        /// ȡֵ
        /// </summary>
        public T Get<T>(object key)
        {
            return Get<T>(key, default(T));
        }
        public T Get<T>(object key, T defaultValue)
        {
            MDataCell cell = this[key];
            if (cell == null || cell.IsNull)
            {
                return defaultValue;
            }
            return cell.Get<T>();
        }


        /// <summary>
        /// ���е�����ת�����У�ColumnName��Value���ı�
        /// </summary>
        public MDataTable ToTable()
        {
            MDataTable dt = new MDataTable(TableName);
            dt.Columns.Add("ColumnName");
            dt.Columns.Add("Value");
            for (int i = 0; i < Count; i++)
            {
                dt.NewRow(true).Set(0, this[i].ColumnName).Set(1, this[i].ToString());
            }
            return dt;
        }


        /// <summary>
        /// ���е������е�ֵȫ����ΪNull
        /// </summary>
        public new void Clear()
        {
            for (int i = 0; i < this.Count; i++)
            {
                this[i].cellValue.Value = null;
                this[i].cellValue.State = 0;
                this[i].cellValue.IsNull = true;
                this[i].strValue = null;
            }
        }

        /// <summary>
        /// ��ȡ�еĵ�ǰ״̬[0:δ���ģ�1:�Ѹ�ֵ,ֵ��ͬ[�ɲ���]��2:�Ѹ�ֵ,ֵ��ͬ[�ɸ���]]
        /// </summary>
        /// <returns></returns>
        public int GetState()
        {
            return GetState(false);
        }
        /// <summary>
        /// ��ȡ�еĵ�ǰ״̬[0:δ���ģ�1:�Ѹ�ֵ,ֵ��ͬ[�ɲ���]��2:�Ѹ�ֵ,ֵ��ͬ[�ɸ���]]
        /// </summary>
        public int GetState(bool ignorePrimaryKey)
        {
            int state = 0;
            for (int i = 0; i < this.Count; i++)
            {
                MDataCell cell = this[i];
                if (ignorePrimaryKey && cell.Struct.IsPrimaryKey)
                {
                    continue;
                }
                state = cell.cellValue.State > state ? cell.cellValue.State : state;
            }
            return state;
        }

        /// <summary>
        /// Ϊ������ֵ
        /// </summary>
        /// <param name="key">�ֶ���</param>
        /// <param name="value">ֵ</param>
        public MDataRow Set(object key, object value)
        {
            MDataCell cell = this[key];
            if (cell != null)
            {
                cell.Value = value;
            }
            return this;
        }
        /// <summary>
        /// ���е������е�״̬ȫ������
        /// </summary>
        /// <param name="state">״̬[0:δ���ģ�1:�Ѹ�ֵ,ֵ��ͬ[�ɲ���]��2:�Ѹ�ֵ,ֵ��ͬ[�ɸ���]]</param>
        public MDataRow SetState(int state)
        {
            SetState(state, BreakOp.None); return this;
        }
        /// <summary>
        /// ���е������е�״̬ȫ������
        /// </summary>
        /// <param name="state">״̬[0:δ���ģ�1:�Ѹ�ֵ,ֵ��ͬ[�ɲ���]��2:�Ѹ�ֵ,ֵ��ͬ[�ɸ���]]</param>
        /// <param name="op">״̬����ѡ��</param>
        public MDataRow SetState(int state, BreakOp op)
        {
            for (int i = 0; i < this.Count; i++)
            {
                switch (op)
                {
                    case BreakOp.Null:
                        if (this[i].IsNull)
                        {
                            continue;
                        }
                        break;
                    case BreakOp.Empty:
                        if (this[i].strValue == "")
                        {
                            continue;
                        }
                        break;
                    case BreakOp.NullOrEmpty:
                        if (this[i].IsNullOrEmpty)
                        {
                            continue;
                        }
                        break;
                }
                this[i].cellValue.State = state;
            }
            return this;
        }

        public void Add(string columnName, object value)
        {
            Add(columnName, SqlDbType.NVarChar, value);
        }
        public void Add(string columnName, SqlDbType sqlType, object value)
        {
            MCellStruct cs = new MCellStruct(columnName, sqlType, false, true, -1);
            Add(new MDataCell(ref cs, value));
        }
        public new void Add(MDataCell cell)
        {
            base.Add(cell);
            Columns.Add(cell.Struct);
        }
        public new void Insert(int index, MDataCell cell)
        {
            base.Insert(index, cell);
            Columns.Insert(index, cell.Struct);
            //MDataCell c = this[cell.ColumnName];
            //c = cell;
        }
        public new void Insert(int index, MDataRow row)
        {
            base.InsertRange(index, row);
            Columns.InsertRange(index, row.Columns);
        }
        public new void Remove(string columnName)
        {
            int index = Columns.GetIndex(columnName);
            if (index > -1)
            {
                RemoveAt(index);
            }
        }
        public new void Remove(MDataCell item)
        {
            if (Columns.Count == Count)
            {
                Columns.Remove(item.Struct);
            }
            else
            {
                base.Remove(item);
            }
        }

        public new void RemoveAt(int index)
        {
            if (Columns.Count == Count)
            {
                Columns.RemoveAt(index);
            }
            else
            {
                base.RemoveAt(index);
            }
        }

        public new void RemoveRange(int index, int count)
        {
            if (Columns.Count == Count)
            {
                Columns.RemoveRange(index, count);
            }
            else
            {
                base.RemoveRange(index, count);
            }

        }

        public new void RemoveAll(Predicate<MDataCell> match)
        {
            Error.Throw(AppConst.Global_NotImplemented);
        }

        #region ICloneable ��Ա
        /// <summary>
        /// ����һ��
        /// </summary>
        /// <returns></returns>
        public MDataRow Clone()
        {
            MDataRow row = new MDataRow();

            for (int i = 0; i < base.Count; i++)
            {
                MCellStruct mcb = base[i].Struct;
                MDataCell mdc = new MDataCell(ref mcb);
                mdc.strValue = base[i].strValue;
                mdc.cellValue.Value = base[i].cellValue.Value;
                mdc.cellValue.State = base[i].cellValue.State;
                mdc.cellValue.IsNull = base[i].cellValue.IsNull;
                row.Add(mdc);
            }
            row.TableName = TableName;
            row.Conn = Conn;
            return row;
        }

        #endregion

        #region IDataRecord ��Ա

        int IDataRecord.FieldCount
        {
            get
            {
                return base.Count;
            }
        }

        bool IDataRecord.GetBoolean(int i)
        {
            return (bool)this[i].Value;
        }

        byte IDataRecord.GetByte(int i)
        {
            return (byte)this[i].Value;
        }

        long IDataRecord.GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            return (byte)this[i].Value;
        }

        char IDataRecord.GetChar(int i)
        {
            return (char)this[i].Value;
        }

        long IDataRecord.GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            return (long)this[i].Value;
        }

        IDataReader IDataRecord.GetData(int i)
        {
            return null;
        }

        string IDataRecord.GetDataTypeName(int i)
        {
            return "";
            //return this[i]._CellValue.ValueType.Name;
        }

        DateTime IDataRecord.GetDateTime(int i)
        {
            return (DateTime)this[i].Value;
        }

        decimal IDataRecord.GetDecimal(int i)
        {
            return (decimal)this[i].Value;
        }

        double IDataRecord.GetDouble(int i)
        {
            return (double)this[i].Value;
        }

        Type IDataRecord.GetFieldType(int i)
        {
            return this[i].Struct.ValueType;
        }

        float IDataRecord.GetFloat(int i)
        {
            return (float)this[i].Value;
        }

        Guid IDataRecord.GetGuid(int i)
        {
            return (Guid)this[i].Value;
        }

        short IDataRecord.GetInt16(int i)
        {
            return (short)this[i].Value;
        }

        int IDataRecord.GetInt32(int i)
        {
            return (int)this[i].Value;
        }

        long IDataRecord.GetInt64(int i)
        {
            return (long)this[i].Value;
        }

        string IDataRecord.GetName(int i)
        {
            return (string)this[i].Value;
        }

        int IDataRecord.GetOrdinal(string name)
        {
            return (int)this[name].Value;
        }

        string IDataRecord.GetString(int i)
        {
            return (string)this[i].Value;
        }

        object IDataRecord.GetValue(int i)
        {
            return this[i].Value;
        }

        int IDataRecord.GetValues(object[] values)
        {
            return 0;
        }

        bool IDataRecord.IsDBNull(int i)
        {
            return this[i].Value == DBNull.Value;
        }

        object IDataRecord.this[string name]
        {

            get
            {
                return this[name].Value;
            }
        }

        object IDataRecord.this[int i]
        {
            get
            {
                return this[i].Value;
            }
        }

        #endregion

    }

    //��չ��������
    public partial class MDataRow
    {
        /// <summary>
        /// ��ʵ�塢Json��Xml��IEnumerable�ӿ�ʵ�ֵ��ࡢMDataRow
        /// </summary>
        /// <returns></returns>
        public static MDataRow CreateFrom(object anyObj)
        {
            return CreateFrom(anyObj, null);
        }
        /// <summary>
        /// ��ʵ�塢Json��Xml��IEnumerable�ӿ�ʵ�ֵ��ࡢMDataRow
        /// </summary>
        public static MDataRow CreateFrom(object anyObj, Type valueType)
        {
            MDataRow row = new MDataRow();
            if (anyObj is string)
            {
                row.LoadFrom(anyObj as string);
            }
            else if (anyObj is IEnumerable)
            {
                row.LoadFrom(anyObj as IEnumerable, valueType);
            }
            else if (anyObj is MDataRow)
            {
                row.LoadFrom(row);
            }
            else
            {
                row.LoadFrom(anyObj);
            }
            return row;
        }

        /// <summary>
        /// ����е�����Json
        /// </summary>
        public string ToJson()
        {
            return ToJson(RowOp.IgnoreNull, false);
        }
        public string ToJson(bool isConvertNameToLower)
        {
            return ToJson(RowOp.IgnoreNull, isConvertNameToLower);
        }
        /// <summary>
        /// ����е�����Json
        /// </summary>
        public string ToJson(RowOp op)
        {
            return ToJson(op, false);
        }
        /// <summary>
        /// ���Json
        /// </summary>
        /// <param name="op">��������</param>
        /// <returns></returns>
        public string ToJson(RowOp op, bool isConvertNameToLower)
        {
            JsonHelper helper = new JsonHelper();
            helper.IsConvertNameToLower = isConvertNameToLower;
            helper.RowOp = op;
            helper.Fill(this);
            return helper.ToString();
        }
        public bool WriteJson(string fileName)
        {
            return WriteJson(fileName, RowOp.IgnoreNull);
        }
        internal string ToXml(bool isConvertNameToLower)
        {
            string xml = string.Empty;
            foreach (MDataCell cell in this)
            {
                xml += cell.ToXml(isConvertNameToLower);
            }
            return xml;
        }
        /// <summary>
        /// ��json���浽ָ���ļ���
        /// </summary>
        public bool WriteJson(string fileName, RowOp op)
        {
            return IOHelper.Write(fileName, ToJson(op));
        }
        /// <summary>
        /// ת��ʵ��
        /// </summary>
        /// <typeparam name="T">ʵ������</typeparam>
        public T ToEntity<T>()
        {
            object obj = Activator.CreateInstance(typeof(T));
            SetToEntity(ref obj, this);
            return (T)obj;
        }


        private object GetValue(MDataRow row, Type type)
        {
            switch (StaticTool.GetSystemType(ref type))
            {
                case SysType.Base:
                    return StaticTool.ChangeType(row[0].Value, type);
                case SysType.Enum:
                    return Enum.Parse(type, row[0].ToString());
                default:
                    object o = Activator.CreateInstance(type);
                    SetToEntity(ref o, row);
                    return o;
            }
        }
        /// <summary>
        /// ��ֵ��������UI
        /// </summary>
        public void SetToAll(params object[] parentControls)
        {
            SetToAll(null, parentControls);
        }
        /// <summary>
        /// ��ֵ��������UI
        /// </summary>
        /// <param name="autoPrefix">�Զ�ǰ׺��������ö��ŷָ�</param>
        /// <param name="parentControls">ҳ��ؼ�</param>
        public void SetToAll(string autoPrefix, params object[] parentControls)
        {
            if (Count > 0)
            {
                MDataRow row = this;
                using (MActionUI mui = new MActionUI(ref row, null, null))
                {
                    if (!string.IsNullOrEmpty(autoPrefix))
                    {
                        string[] pres = autoPrefix.Split(',');
                        mui.SetAutoPrefix(pres[0], pres);
                    }
                    mui.SetAll(parentControls);
                }
            }
        }
        /// <summary>
        /// ��Web Post����ȡֵ��
        /// </summary>
        public void LoadFrom()
        {
            LoadFrom(true);
        }
        /// <summary>
        /// ��Web Post����ȡֵ �� ��Winform��WPF�ı��ؼ���ȡֵ��
        /// <param name="isWeb">TrueΪWebӦ�ã���֮ΪWinӦ��</param>
        /// <param name="prefixOrParentControl">Webʱ����ǰ׺��Winʱ���ø������ؼ�</param>
        /// </summary>
        public void LoadFrom(bool isWeb, params object[] prefixOrParentControl)
        {
            if (Count > 0)
            {
                MDataRow row = this;
                using (MActionUI mui = new MActionUI(ref row, null, null))
                {
                    if (prefixOrParentControl.Length > 0)
                    {
                        if (isWeb)
                        {
                            string[] items = prefixOrParentControl as string[];
                            mui.SetAutoPrefix(items[0], items);
                        }
                        else
                        {
                            mui.SetAutoParentControl(prefixOrParentControl[0], prefixOrParentControl);
                        }
                    }

                    mui.GetAll(false);
                }
            }
        }

        /// <summary>
        /// �ӱ���м���ֵ
        /// </summary>
        public void LoadFrom(MDataRow row)
        {
            LoadFrom(row, RowOp.None, Count == 0);
        }
        /// <summary>
        /// �ӱ���м���ֵ
        /// </summary>
        public void LoadFrom(MDataRow row, RowOp rowOp, bool isAllowAppendColumn)
        {
            LoadFrom(row, rowOp, isAllowAppendColumn, true);
        }
        /// <summary>
        /// �ӱ���м���ֵ
        /// </summary>
        /// <param name="row">Ҫ�������ݵ���</param>
        /// <param name="rowOp">��ѡ��[�������м��ص�����]</param>
        /// <param name="isAllowAppendColumn">���row�������У��Ƿ����[Ĭ��ֵΪ��true]</param>
        /// <param name="isWithValueState">�Ƿ�ͬʱ����ֵ��״̬[Ĭ��ֵΪ��true]</param>
        public void LoadFrom(MDataRow row, RowOp rowOp, bool isAllowAppendColumn, bool isWithValueState)
        {
            if (row != null)
            {
                if (isAllowAppendColumn)
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (!Columns.Contains(row[i].ColumnName))
                        {
                            Columns.Add(row[i].Struct);
                        }
                    }
                }
                MDataCell rowCell;
                foreach (MDataCell cell in this)
                {
                    rowCell = row[cell.ColumnName];
                    if (rowCell == null)
                    {
                        continue;
                    }
                    if (rowOp == RowOp.None || (!rowCell.IsNull && rowCell.cellValue.State >= (int)rowOp))
                    {
                        cell.Value = rowCell.cellValue.Value;//�����ڸ�ֵ����Ϊ��ͬ�ļܹ����������������� int[access] int64[sqlite]����ת��ʱ�����
                        //cell._CellValue.IsNull = rowCell._CellValue.IsNull;//
                        if (isWithValueState)
                        {
                            cell.cellValue.State = rowCell.cellValue.State;
                        }
                    }
                }

            }
        }
        /// <summary>
        /// �����������ֵ
        /// </summary>
        /// <param name="values"></param>
        public void LoadFrom(object[] values)
        {
            if (values != null && values.Length <= Count)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    this[i].Value = values[i];
                }
            }
        }
        /// <summary>
        /// ��json�����ֵ
        /// </summary>
        public void LoadFrom(string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                Dictionary<string, string> dic = JsonHelper.Split(json);
                if (dic != null && dic.Count > 0)
                {
                    LoadFrom(dic);
                }
            }
            else
            {
                LoadFrom(true);
            }
        }
        /// <summary>
        /// �ӷ����ֵ伯�������
        /// </summary>
        public void LoadFrom(IEnumerable dic)
        {
            LoadFrom(dic, null);
        }
        internal void LoadFrom(IEnumerable dic, Type valueType)
        {
            if (dic != null)
            {
                bool isNameValue = dic is NameValueCollection;
                bool isAddColumn = Columns.Count == 0;
                SqlDbType sdt = SqlDbType.NVarChar;
                if (isAddColumn)
                {
                    if (valueType != null)
                    {
                        sdt = DataType.GetSqlType(valueType);
                    }
                    else if (!isNameValue)
                    {
                        Type type = dic.GetType();
                        if (type.IsGenericType)
                        {
                            sdt = DataType.GetSqlType(type.GetGenericArguments()[1]);
                        }
                        else
                        {
                            sdt = SqlDbType.Variant;
                        }
                    }
                }
                string key = null; object value = null;
                Type t = null;
                foreach (object o in dic)
                {
                    if (isNameValue)
                    {
                        key = Convert.ToString(o);
                        value = ((NameValueCollection)dic)[key];
                    }
                    else
                    {
                        t = o.GetType();
                        value = t.GetProperty("Value").GetValue(o, null);
                        if (value != null)
                        {
                            key = Convert.ToString(t.GetProperty("Key").GetValue(o, null));
                        }
                    }
                    if (value != null)
                    {
                        if (isAddColumn)
                        {
                            SqlDbType sdType = sdt;
                            if (sdt == SqlDbType.Variant)
                            {
                                sdType = DataType.GetSqlType(value.GetType());
                            }
                            Add(key, sdType, value);
                        }
                        else
                        {
                            Set(key, value);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// ��ʵ��ת�������С�
        /// </summary>
        /// <param name="entity">ʵ�����</param>
        public void LoadFrom(object entity)
        {
            LoadFrom(entity, BreakOp.None);
        }
        /// <summary>
        /// ��ʵ��ת�������С�
        /// </summary>
        /// <param name="entity">ʵ�����</param>
        public void LoadFrom(object entity, BreakOp op)
        {
            if (entity == null)
            {
                return;
            }
            try
            {
                Type t = entity.GetType();
                if (Columns.Count == 0)
                {
                    MDataColumn mcs = TableSchema.GetColumns(t);
                    MCellStruct ms = null;
                    for (int i = 0; i < mcs.Count; i++)
                    {
                        ms = mcs[i];
                        MDataCell cell = new MDataCell(ref ms);
                        Add(cell);
                    }
                }

                if (string.IsNullOrEmpty(TableName))
                {
                    TableName = t.Name;
                }
                PropertyInfo[] pis = StaticTool.GetPropertyInfo(t);
                if (pis != null)
                {
                    foreach (PropertyInfo pi in pis)
                    {
                        int index = Columns.GetIndex(pi.Name);
                        if (index > -1)
                        {
                            object propValue = pi.GetValue(entity, null);
                            switch (op)
                            {
                                case BreakOp.Null:
                                    if (propValue == null)
                                    {
                                        continue;
                                    }
                                    break;
                                case BreakOp.Empty:
                                    if (Convert.ToString(propValue) == "")
                                    {
                                        continue;
                                    }
                                    break;
                                case BreakOp.NullOrEmpty:
                                    if (propValue == null || Convert.ToString(propValue) == "")
                                    {
                                        continue;
                                    }
                                    break;
                            }
                            Set(index, propValue);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Log.WriteLogToTxt(err);
            }
        }

        /// <summary>
        /// �����е�����ֵ����ʵ�����
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        public void SetToEntity(object obj)
        {
            SetToEntity(ref obj, this);
        }
        /// <summary>
        /// ��ָ���е����ݸ���ʵ�����
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="row"></param>
        internal void SetToEntity(ref object obj, MDataRow row)
        {
            if (obj == null || row == null || row.Count == 0)
            {
                return;
            }
            Type objType = obj.GetType();
            string objName = objType.FullName, cellName = string.Empty;
            try
            {
                #region �������
                PropertyInfo[] pis = StaticTool.GetPropertyInfo(objType);
                foreach (PropertyInfo p in pis)
                {
                    cellName = p.Name;
                    MDataCell cell = row[cellName];
                    if (cell == null || cell.IsNull)
                    {
                        continue;
                    }
                    Type propType = p.PropertyType;
                    object objValue = null;
                    switch (StaticTool.GetSystemType(ref propType))
                    {
                        case SysType.Enum:
                            p.SetValue(obj, Enum.Parse(propType, cell.ToString()), null);
                            break;
                        case SysType.Base:
                            object value = StaticTool.ChangeType(cell.Value, p.PropertyType);
                            p.SetValue(obj, value, null);
                            break;
                        case SysType.Array:
                            if (cell.Value.GetType() == propType)
                            {
                                objValue = cell.Value;
                            }
                            else
                            {
                                Type arrayType = Type.GetType(propType.FullName.Replace("[]", ""));
                                MDataTable dtArray = MDataTable.CreateFrom(cell.ToString(), TableSchema.GetColumns(arrayType));
                                objValue = Activator.CreateInstance(propType, dtArray.Rows.Count);//����ʵ��
                                Type objArrayListType = objValue.GetType();
                                MDataRow item;
                                for (int i = 0; i < dtArray.Rows.Count; i++)
                                {
                                    item = dtArray.Rows[i];
                                    object o = GetValue(item, arrayType);
                                    MethodInfo method = objArrayListType.GetMethod("Set");
                                    if (method != null)
                                    {
                                        method.Invoke(objValue, new object[] { i, o });
                                    }
                                }
                            }
                            p.SetValue(obj, objValue, null);
                            break;
                        case SysType.Collection:
                        case SysType.Generic:
                            if (cell.Value.GetType() == propType)
                            {
                                objValue = cell.Value;
                            }
                            else
                            {
                                Type[] argTypes = null;
                                int len = StaticTool.GetArgumentLength(ref propType, out argTypes);

                                objValue = Activator.CreateInstance(propType);//����ʵ��
                                Type objListType = objValue.GetType();
                                if (len == 1) // Table
                                {

                                    MDataTable dt = MDataTable.CreateFrom(cell.ToString());//, SchemaCreate.GetColumns(argTypes[0])
                                    foreach (MDataRow rowItem in dt.Rows)
                                    {
                                        object o = GetValue(rowItem, argTypes[0]);
                                        MethodInfo method = objListType.GetMethod("Add");
                                        if (method == null)
                                        {
                                            method = objListType.GetMethod("Push");
                                        }
                                        if (method != null)
                                        {
                                            method.Invoke(objValue, new object[] { o });
                                        }
                                    }
                                    dt = null;
                                }
                                else if (len == 2) // row
                                {
                                    MDataRow mRow = MDataRow.CreateFrom(cell.Value, argTypes[1]);
                                    foreach (MDataCell mCell in mRow)
                                    {
                                        object mObj = GetValue(mCell.ToRow(), argTypes[1]);
                                        objValue.GetType().GetMethod("Add").Invoke(objValue, new object[] { mCell.ColumnName, mObj });
                                    }
                                    mRow = null;
                                }
                            }
                            p.SetValue(obj, objValue, null);
                            break;
                        case SysType.Custom://�����ݹ�
                            MDataRow mr = new MDataRow(TableSchema.GetColumns(propType));
                            mr.LoadFrom(cell.ToString());
                            objValue = Activator.CreateInstance(propType);
                            SetToEntity(ref objValue, mr);
                            mr = null;
                            p.SetValue(obj, objValue, null);
                            break;

                    }
                }
                #endregion
            }
            catch (Exception err)
            {
                string msg = "[AttachInfo]:" + string.Format("ObjName:{0} PropertyName:{1}", objName, cellName) + "\r\n";
                msg += Log.GetExceptionMessage(err);
                Log.WriteLogToTxt(msg);
            }
        }

    }
    public partial class MDataRow : System.ComponentModel.ICustomTypeDescriptor
    {
        #region ICustomTypeDescriptor ��Ա

        System.ComponentModel.AttributeCollection ICustomTypeDescriptor.GetAttributes()
        {
            return null;
        }

        string ICustomTypeDescriptor.GetClassName()
        {
            return "MDataRow";
        }

        string ICustomTypeDescriptor.GetComponentName()
        {
            return string.Empty;
        }

        TypeConverter ICustomTypeDescriptor.GetConverter()
        {
            return null;
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
        {
            return null;
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
        {
            return null;
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        {
            return null;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
        {
            return null;
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        {
            return null;
        }
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
        {
            return Create();

        }
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return Create();
        }

        object ICustomTypeDescriptor.GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd)
        {
            return this;
        }
        [NonSerialized]
        PropertyDescriptorCollection properties;
        PropertyDescriptorCollection Create()
        {
            if (properties != null)
            {
                return properties;
            }
            properties = new PropertyDescriptorCollection(null);

            foreach (MDataCell mdc in this)
            {
                properties.Add(new MDataProperty(mdc, null));
            }
            return properties;
        }
        #endregion
    }
}
