using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using NewLife;
using NewLife.Log;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// ���ݿ�Ự���ࡣ
    /// </summary>
    internal abstract partial class DbSession : DisposeBase, IDbSession
    {
        #region ���캯��
        public DbSession(IDatabase db) { _Db = db; }

        /// <summary>
        /// ������Դʱ���ع�δ�ύ���񣬲��ر����ݿ�����
        /// </summary>
        /// <param name="disposing"></param>
        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

            try
            {
                // ע�⣬û��Commit�����ݣ������ｫ�ᱻ�ع�
                //if (Trans != null) Rollback();
                // ��Ƕ�������У�Rollbackֻ�ܼ���Ƕ�ײ�������_Trans.Rollback�����������ϻع�
                if (_Trans != null && Opened) _Trans.Rollback();
                if (_Conn != null) Close();
            }
            catch (Exception ex)
            {
                WriteLog("ִ��" + DbType.ToString() + "��Disposeʱ������" + ex.ToString());
            }
        }
        #endregion

        #region ����
        private static Int32 gid = 0;
        private Int32? _ID;
        /// <summary>
        /// ��ʶ
        /// </summary>
        public Int32 ID
        {
            get
            {
                if (_ID == null) _ID = ++gid;
                return _ID.Value;
            }
        }

        /// <summary>
        /// �������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other
        /// </summary>
        public DatabaseType DbType { get { return Db.DbType; } }

        /// <summary>����</summary>
        public DbProviderFactory Factory { get { return Db.Factory; } }

        ///// <summary>���ݿ�Ԫ����</summary>
        //public abstract IDatabaseMeta Meta { get; }
        private IDatabase _Db;
        /// <summary>���ݿ�</summary>
        public IDatabase Db
        {
            get { return _Db; }
            //set { _Meta = value; }
        }

        private String _ConnectionString;
        /// <summary>�����ַ���</summary>
        public virtual String ConnectionString
        {
            get { return _ConnectionString; }
            set
            {
                _ConnectionString = value;
                if (!String.IsNullOrEmpty(_ConnectionString))
                {
                    DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
                    builder.ConnectionString = _ConnectionString;
                    if (builder.ContainsKey("owner"))
                    {
                        if (builder["owner"] != null) Owner = builder["owner"].ToString();
                        builder.Remove("owner");
                    }
                    _ConnectionString = builder.ToString();
                }
            }
        }

        private DbConnection _Conn;
        /// <summary>
        /// �������Ӷ���
        /// </summary>
        public virtual DbConnection Conn
        {
            get
            {
                if (_Conn == null)
                {
                    _Conn = Factory.CreateConnection();
                    _Conn.ConnectionString = ConnectionString;
                }
                return _Conn;
            }
            set { _Conn = value; }
        }

        private String _Owner;
        /// <summary>ӵ����</summary>
        public String Owner
        {
            get { return _Owner; }
            set { _Owner = value; }
        }

        private Int32 _QueryTimes;
        /// <summary>
        /// ��ѯ����
        /// </summary>
        public Int32 QueryTimes
        {
            get { return _QueryTimes; }
            set { _QueryTimes = value; }
        }

        private Int32 _ExecuteTimes;
        /// <summary>
        /// ִ�д���
        /// </summary>
        public Int32 ExecuteTimes
        {
            get { return _ExecuteTimes; }
            set { _ExecuteTimes = value; }
        }

        /// <summary>
        /// ���ݿ�������汾
        /// </summary>
        public String ServerVersion
        {
            get
            {
                if (!Opened) Open();
                String ver = Conn.ServerVersion;
                AutoClose();
                return ver;
            }
        }
        #endregion

        #region ��/�ر�
        private Boolean _IsAutoClose = true;
        /// <summary>
        /// �Ƿ��Զ��رա�
        /// ��������󣬸�������Ч��
        /// ���ύ��ع�����ʱ�����IsAutoCloseΪtrue������Զ��ر�
        /// </summary>
        public Boolean IsAutoClose
        {
            get { return _IsAutoClose; }
            set { _IsAutoClose = value; }
        }

        /// <summary>
        /// �����Ƿ��Ѿ���
        /// </summary>
        public Boolean Opened
        {
            get { return _Conn != null && _Conn.State != ConnectionState.Closed; }
        }

        /// <summary>
        /// ��
        /// </summary>
        public virtual void Open()
        {
            if (Conn != null && Conn.State == ConnectionState.Closed)
            {
                //try { 
                Conn.Open();
                //}
                //catch (Exception ex)
                //{
                //    WriteLog("ִ��" + this.GetType().FullName + "��Openʱ������" + ex.ToString());
                //    throw;
                //}
            }
        }

        /// <summary>
        /// �ر�
        /// </summary>
        public virtual void Close()
        {
            if (_Conn != null && Conn.State != ConnectionState.Closed)
            {
                try { Conn.Close(); }
                catch (Exception ex)
                {
                    WriteLog("ִ��" + DbType.ToString() + "��Closeʱ������" + ex.ToString());
                }
            }
        }

        /// <summary>
        /// �Զ��رա�
        /// ��������󣬲��ر����ӡ�
        /// ���ύ��ع�����ʱ�����IsAutoCloseΪtrue������Զ��ر�
        /// </summary>
        public void AutoClose()
        {
            if (IsAutoClose && Trans == null && Opened) Close();
        }

        /// <summary>���ݿ���</summary>
        public String DatabaseName
        {
            get
            {
                return Conn == null ? null : Conn.Database;
            }
            set
            {
                if (Opened)
                {
                    //����Ѵ򿪣�������������л�
                    Conn.ChangeDatabase(value);
                }
                else
                {
                    //���û�д򿪣���ı������ַ���
                    DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
                    builder.ConnectionString = ConnectionString;
                    builder["Database"] = value;
                    ConnectionString = builder.ToString();
                    Conn.ConnectionString = ConnectionString;
                }
            }
        }

        /// <summary>
        /// ���쳣����ʱ�������ر����ݿ����ӣ����߷������ӵ����ӳء�
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        protected virtual XDbException OnException(Exception ex)
        {
            if (Trans == null && Opened) Close(); // ǿ�ƹر����ݿ�
            //return new XException("�ڲ����ݿ�ʵ��" + this.GetType().FullName + "�쳣��ִ��" + Environment.StackTrace + "����������", ex);
            //String err = "�ڲ����ݿ�ʵ��" + DbType.ToString() + "�쳣��ִ�з���������" + Environment.NewLine + ex.Message;
            if (ex != null)
                return new XDbException(this, ex);
            else
                return new XDbException(this);
        }

        /// <summary>
        /// ���쳣����ʱ�������ر����ݿ����ӣ����߷������ӵ����ӳء�
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        protected virtual XSqlException OnException(Exception ex, String sql)
        {
            if (Trans == null && Opened) Close(); // ǿ�ƹر����ݿ�
            //return new XException("�ڲ����ݿ�ʵ��" + this.GetType().FullName + "�쳣��ִ��" + Environment.StackTrace + "����������", ex);
            //String err = "�ڲ����ݿ�ʵ��" + DbType.ToString() + "�쳣��ִ�з���������" + Environment.NewLine;
            //if (!String.IsNullOrEmpty(sql)) err += "SQL��䣺" + sql + Environment.NewLine;
            //err += ex.Message;
            if (ex != null)
                return new XSqlException(sql, this, ex);
            else
                return new XSqlException(sql, this);
        }
        #endregion

        #region ����
        private DbTransaction _Trans;
        /// <summary>
        /// ���ݿ�����
        /// </summary>
        protected DbTransaction Trans
        {
            get { return _Trans; }
            set { _Trans = value; }
        }

        /// <summary>
        /// ���������
        /// ���ҽ��������������1ʱ�����ύ��ع���
        /// </summary>
        private Int32 TransactionCount = 0;

        /// <summary>
        /// ��ʼ����
        /// </summary>
        /// <returns></returns>
        public Int32 BeginTransaction()
        {
            if (Debug) WriteLog("��ʼ����{0}", ID);

            TransactionCount++;
            if (TransactionCount > 1) return TransactionCount;

            try
            {
                if (!Opened) Open();
                Trans = Conn.BeginTransaction();
                TransactionCount = 1;
                return TransactionCount;
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }
        }

        /// <summary>
        /// �ύ����
        /// </summary>
        public Int32 Commit()
        {
            if (Debug) WriteLog("�ύ����{0}", ID);

            TransactionCount--;
            if (TransactionCount > 0) return TransactionCount;

            if (Trans == null) throw new InvalidOperationException("��ǰ��δ��ʼ��������BeginTransaction������ʼ������ID=" + ID);
            try
            {
                Trans.Commit();
                Trans = null;
                if (IsAutoClose) Close();
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }

            return TransactionCount;
        }

        /// <summary>
        /// �ع�����
        /// </summary>
        public Int32 Rollback()
        {
            if (Debug) WriteLog("�ع�����{0}", ID);

            TransactionCount--;
            if (TransactionCount > 0) return TransactionCount;

            if (Trans == null) throw new InvalidOperationException("��ǰ��δ��ʼ��������BeginTransaction������ʼ������ID=" + ID);
            try
            {
                Trans.Rollback();
                Trans = null;
                if (IsAutoClose) Close();
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }

            return TransactionCount;
        }
        #endregion

        #region �������� ��ѯ/ִ��
        /// <summary>
        /// ִ��SQL��ѯ�����ؼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public virtual DataSet Query(String sql)
        {
            QueryTimes++;
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                using (DbDataAdapter da = Factory.CreateDataAdapter())
                {
                    da.SelectCommand = cmd;
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    return ds;
                }
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ظ����������ȼܹ���Ϣ�ļ�¼���������Բ�����ͨ��ѯ
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public virtual DataSet QueryWithKey(String sql)
        {
            QueryTimes++;
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                using (DbDataAdapter da = Factory.CreateDataAdapter())
                {
                    da.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                    da.SelectCommand = cmd;
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    return ds;
                }
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ؼ�¼��
        /// </summary>
        /// <param name="builder">��ѯ������</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��¼��</returns>
        public virtual DataSet Query(SelectBuilder builder, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            return Query(Db.PageSplit(builder, startRowIndex, maximumRows, keyColumn));
        }

        /// <summary>
        /// ִ��DbCommand�����ؼ�¼��
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <returns></returns>
        public virtual DataSet Query(DbCommand cmd)
        {
            QueryTimes++;
            using (DbDataAdapter da = Factory.CreateDataAdapter())
            {
                try
                {
                    if (!Opened) Open();
                    cmd.Connection = Conn;
                    if (Trans != null) cmd.Transaction = Trans;
                    da.SelectCommand = cmd;
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    return ds;
                }
                catch (DbException ex)
                {
                    throw OnException(ex, cmd.CommandText);
                }
                finally
                {
                    AutoClose();
                }
            }
        }

        private static Regex reg_QueryCount = new Regex(@"^\s*select\s+\*\s+from\s+([\w\W]+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public virtual Int32 QueryCount(String sql)
        {
            if (sql.Contains(" "))
            {
                String orderBy = CheckOrderClause(ref sql);
                //sql = String.Format("Select Count(*) From {0}", CheckSimpleSQL(sql));
                //Match m = reg_QueryCount.Match(sql);
                MatchCollection ms = reg_QueryCount.Matches(sql);
                if (ms != null && ms.Count > 0)
                {
                    sql = String.Format("Select Count(*) From {0}", ms[0].Groups[1].Value);
                }
                else
                {
                    sql = String.Format("Select Count(*) From {0}", CheckSimpleSQL(sql));
                }
            }
            else
                sql = String.Format("Select Count(*) From {0}", FormatKeyWord(sql));

            QueryTimes++;
            DbCommand cmd = PrepareCommand();
            cmd.CommandText = sql;
            if (Debug) WriteLog(cmd.CommandText);
            try
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="builder">��ѯ������</param>
        /// <returns>�ܼ�¼��</returns>
        public virtual Int32 QueryCount(SelectBuilder builder)
        {
            QueryTimes++;
            DbCommand cmd = PrepareCommand();
            cmd.CommandText = builder.SelectCount().ToString();
            if (Debug) WriteLog(cmd.CommandText);
            try
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ���ٲ�ѯ������¼��������ƫ��
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public virtual Int32 QueryCountFast(String tableName)
        {
            return QueryCount(tableName);
        }

        /// <summary>
        /// ִ��SQL��䣬������Ӱ�������
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public virtual Int32 Execute(String sql)
        {
            ExecuteTimes++;
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                Int32 rs = cmd.ExecuteNonQuery();
                //AutoClose();
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ִ��DbCommand��������Ӱ�������
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <returns></returns>
        public virtual Int32 Execute(DbCommand cmd)
        {
            ExecuteTimes++;
            try
            {
                if (!Opened) Open();
                cmd.Connection = Conn;
                if (Trans != null) cmd.Transaction = Trans;
                Int32 rs = cmd.ExecuteNonQuery();
                //AutoClose();
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns>�����е��Զ����</returns>
        public virtual Int32 InsertAndGetIdentity(String sql)
        {
            ExecuteTimes++;
            //SQLServerд��
            sql = "SET NOCOUNT ON;" + sql + ";Select SCOPE_IDENTITY()";
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                Int32 rs = Int32.Parse(cmd.ExecuteScalar().ToString());
                //AutoClose();
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
            finally
            {
                AutoClose();
            }
        }

        /// <summary>
        /// ��ȡһ��DbCommand��
        /// ���������ӣ�������������
        /// �����Ѵ򿪡�
        /// ʹ����Ϻ󣬱������AutoClose��������ʹ���ڷ������������Զ��رյ�����¹ر�����
        /// </summary>
        /// <returns></returns>
        public virtual DbCommand PrepareCommand()
        {
            DbCommand cmd = Factory.CreateCommand();
            if (!Opened) Open();
            cmd.Connection = Conn;
            if (Trans != null) cmd.Transaction = Trans;
            return cmd;
        }
        #endregion

        #region ��������
        protected String FormatKeyWord(String keyWord)
        {
            return Db.FormatKeyWord(keyWord);
        }

        /// <summary>
        /// ����SQL��䣬����Select * From table
        /// </summary>
        /// <param name="sql">�����SQL���</param>
        /// <returns>����Ǽ�SQL����򷵻ر��������򷵻��Ӳ�ѯ(sql) XCode_Temp_a</returns>
        protected static String CheckSimpleSQL(String sql)
        {
            if (String.IsNullOrEmpty(sql)) return sql;

            Regex reg = new Regex(@"^\s*select\s+\*\s+from\s+([\w\[\]\""\""\']+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection ms = reg.Matches(sql);
            if (ms == null || ms.Count < 1 || ms[0].Groups.Count < 2 ||
                String.IsNullOrEmpty(ms[0].Groups[1].Value)) return String.Format("({0}) XCode_Temp_a", sql);
            return ms[0].Groups[1].Value;
        }

        /// <summary>
        /// ����Ƿ���Order�Ӿ��β������ǣ��ָ�sqlΪǰ��������
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private static String CheckOrderClause(ref String sql)
        {
            if (!sql.ToLower().Contains("order")) return null;

            // ʹ����������ϸ��жϡ��������Order By���������ұ�û��������)��������order by���Ҳ����Ӳ�ѯ�ģ�����Ҫ���⴦��
            MatchCollection ms = Regex.Matches(sql, @"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            if (ms == null || ms.Count < 1 || ms[0].Index < 1) return null;
            String orderBy = sql.Substring(ms[0].Index).Trim();
            sql = sql.Substring(0, ms[0].Index).Trim();

            return orderBy;
        }
        #endregion

        #region Sql��־���
        private static Boolean? _Debug;
        /// <summary>
        /// �Ƿ����
        /// </summary>
        public static Boolean Debug
        {
            get
            {
                if (_Debug != null) return _Debug.Value;

                String str = ConfigurationManager.AppSettings["XCode.Debug"];
                if (String.IsNullOrEmpty(str)) str = ConfigurationManager.AppSettings["OrmDebug"];
                if (String.IsNullOrEmpty(str))
                    _Debug = false;
                else if (str == "1" || str.Equals(Boolean.TrueString, StringComparison.OrdinalIgnoreCase))
                    _Debug = true;
                else if (str == "0" || str.Equals(Boolean.FalseString, StringComparison.OrdinalIgnoreCase))
                    _Debug = false;
                else
                    _Debug = Convert.ToBoolean(str);
                return _Debug.Value;
            }
            set { _Debug = value; }
        }

        /// <summary>
        /// �����־
        /// </summary>
        /// <param name="msg"></param>
        public static void WriteLog(String msg)
        {
            XTrace.WriteLine(msg);
        }

        /// <summary>
        /// �����־
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void WriteLog(String format, params Object[] args)
        {
            XTrace.WriteLine(format, args);
        }
        #endregion
    }
}