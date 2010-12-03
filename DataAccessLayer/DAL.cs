using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using XCode.Cache;
using XCode.Code;
using XCode.XLicense;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// 数据访问层。
    /// <remarks>
    /// 主要用于选择不同的数据库，不同的数据库的操作有所差别。
    /// 每一个数据库链接字符串，对应唯一的一个DAL实例。
    /// 数据库链接字符串可以写在配置文件中，然后在Create时指定名字；
    /// 也可以直接把链接字符串作为AddConnStr的参数传入。
    /// 每一个DAL实例，会为每一个线程初始化一个DataBase实例。
    /// 每一个数据库操作都必须指定表名以用于管理缓存，空表名或*将匹配所有缓存
    /// </remarks>
    /// </summary>
    public class DAL
    {
        #region 创建函数
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="connName">配置名</param>
        private DAL(String connName)
        {
            _ConnName = connName;

            if (!ConnStrs.ContainsKey(connName)) throw new Exception("请在使用数据库前设置[" + connName + "]连接字符串");

            ConnStr = ConnStrs[connName].ConnectionString;

            DatabaseSchema.Check(this);
        }

        private static Dictionary<String, DAL> _dals = new Dictionary<String, DAL>();
        /// <summary>
        /// 创建一个数据访问层对象。以null作为参数可获得当前默认对象
        /// </summary>
        /// <param name="connName">配置名，或链接字符串</param>
        /// <returns>对应于指定链接的全局唯一的数据访问层对象</returns>
        public static DAL Create(String connName)
        {
            //当connName为null时，_dals里面并没有包含null的项，所以需要提前处理
            if (String.IsNullOrEmpty(connName)) return new DAL(null);

            if (_dals.ContainsKey(connName)) return _dals[connName];
            DAL d;
            lock (_dals)
            {
                if (_dals.ContainsKey(connName)) return _dals[connName];

                ////检查数据库最大连接数授权。
                //if (License.DbConnectCount != _dals.Count + 1)
                //    License.DbConnectCount = _dals.Count + 1;

                d = new DAL(connName);
                // 不用connName，因为可能在创建过程中自动识别了ConnName
                _dals.Add(d.ConnName, d);
            }

            return d;
        }

        private static Object _connStrs_lock = new Object();
        private static Dictionary<String, ConnectionStringSettings> _connStrs;
        private static Dictionary<String, Type> _connTypes = new Dictionary<String, Type>();
        /// <summary>
        /// 链接字符串集合
        /// </summary>
        public static Dictionary<String, ConnectionStringSettings> ConnStrs
        {
            get
            {
                if (_connStrs != null) return _connStrs;
                lock (_connStrs_lock)
                {
                    if (_connStrs != null) return _connStrs;
                    Dictionary<String, ConnectionStringSettings> cs = new Dictionary<String, ConnectionStringSettings>();

                    //读取配置文件
                    ConnectionStringSettingsCollection css = ConfigurationManager.ConnectionStrings;
                    if (css != null && css.Count > 0)
                    {
                        foreach (ConnectionStringSettings set in css)
                        {
                            if (set.Name == "LocalSqlServer") continue;

                            Type type = GetTypeFromConn(set.ConnectionString, set.ProviderName);

                            cs.Add(set.Name, set);
                            _connTypes.Add(set.Name, type);

#if DEBUG
                            NewLife.Log.XTrace.WriteLine("新增数据库链接{0}：{1}", set.Name, set.ConnectionString);
#endif
                        }
                    }
                    _connStrs = cs;
                }
                return _connStrs;
            }
        }

        /// <summary>
        /// 添加连接字符串
        /// </summary>
        /// <param name="connName"></param>
        /// <param name="connStr"></param>
        /// <param name="type"></param>
        /// <param name="provider"></param>
        public static void AddConnStr(String connName, String connStr, Type type, String provider)
        {
            if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException("connName");

            if (ConnStrs.ContainsKey(connName)) return;
            lock (ConnStrs)
            {
                if (ConnStrs.ContainsKey(connName)) return;

                if (type == null) type = GetTypeFromConn(connStr, provider);

                ConnectionStringSettings set = new ConnectionStringSettings(connName, connStr, provider);
                ConnStrs.Add(connName, set);
                _connTypes.Add(connName, type);
#if DEBUG
                NewLife.Log.XTrace.WriteLine("新增数据库链接{0}：{1}", set.Name, set.ConnectionString);
#endif
            }
        }

        private static Type GetTypeFromConn(String connStr, String provider)
        {
            Type type = null;
            if (!String.IsNullOrEmpty(provider))
            {
                provider = provider.ToLower();
                if (provider.Contains("sqlclient"))
                    type = typeof(SqlServer);
                else if (provider.Contains("oracleclient"))
                    type = typeof(Oracle);
                else if (provider.Contains("microsoft.jet.oledb"))
                    type = typeof(Access);
                else if (provider.Contains("access"))
                    type = typeof(Access);
                else if (provider.Contains("mysql"))
                    type = typeof(MySql);
                else if (provider.Contains("sqlite"))
                    type = typeof(SQLite);
                else if (provider.Contains("sql2008"))
                    type = typeof(SqlServer2005);
                else if (provider.Contains("sql2005"))
                    type = typeof(SqlServer2005);
                else if (provider.Contains("sql2000"))
                    type = typeof(SqlServer);
                else if (provider.Contains("sql"))
                    type = typeof(SqlServer);
                else
                {
                    if (provider.Contains(",")) // 带有程序集名称，加载程序集
                        type = Assembly.Load(provider.Substring(0, provider.IndexOf(","))).GetType(provider.Substring(provider.IndexOf(",") + 1, provider.Length), true, false);
                    else // 没有程序集名称，则使用本程序集
                        type = Type.GetType(provider, true, true);
                }
            }
            else
            {
                // 分析类型
                String str = connStr.ToLower();
                if (str.Contains("mssql") || str.Contains("sqloledb"))
                    type = typeof(SqlServer);
                else if (str.Contains("oracle"))
                    type = typeof(Oracle);
                else if (str.Contains("microsoft.jet.oledb"))
                    type = typeof(Access);
                else if (str.Contains("sql"))
                    type = typeof(SqlServer);
                else
                    type = typeof(Access);
            }
            return type;
        }
        #endregion

        #region 静态属性
        [ThreadStatic]
        private static DAL _Default;
        /// <summary>
        /// 当前数据访问对象
        /// </summary>
        public static DAL Default
        {
            get
            {
                if (_Default == null && ConnStrs != null && ConnStrs.Count > 0)
                {
                    String name = null;
                    foreach (String item in ConnStrs.Keys)
                    {
                        if (!String.IsNullOrEmpty(item))
                        {
                            name = item;
                            break;
                        }
                    }
                    if (!String.IsNullOrEmpty(name)) _Default = Create(name);
                }

                return _Default;
            }
        }
        #endregion

        #region 属性
        private String _ConnName;
        /// <summary>
        /// 配置名。只读，若要设置，请重新声明一个DAL对象。
        /// </summary>
        public String ConnName
        {
            get { return _ConnName; }
        }

        private Type _DALType;
        /// <summary>
        /// 数据访问层基层类型。
        /// <remarks>改变数据访问层数据库实体会断开当前链接，建议在任何数据库操作之前改变</remarks>
        /// </summary>
        public Type DALType
        {
            get
            {
                if (_DALType == null && _connTypes.ContainsKey(ConnName)) _DALType = _connTypes[ConnName];
                return _DALType;
            }
            set	// 如果外部需要改变数据访问层数据库实体
            {
                IDatabase idb;
                //if (HttpContext.Current == null)
                idb = _DBs != null && _DBs.ContainsKey(ConnName) ? _DBs[ConnName] : null;
                //else
                //    idb = HttpContext.Current.Items[ConnName + "_DB"] as IDataBase;
                if (idb != null)
                {
                    idb.Dispose();
                    idb = null;
                }
                _DALType = value;
            }
        }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public DatabaseType DbType
        {
            get { return DB.DbType; }
        }

        private String _ConnStr;
        /// <summary>
        /// 默认连接字符串，第一个ConnectionString就是
        /// </summary>
        public String ConnStr
        {
            get { return _ConnStr; }
            private set { _ConnStr = value; }
        }

        /// <summary>
        /// ThreadStatic 指示静态字段的值对于每个线程都是唯一的。
        /// </summary>
        [ThreadStatic]
        private static IDictionary<String, IDatabase> _DBs;
        /// <summary>
        /// DAL对象。
        /// <remarks>
        /// 这里使用线程级缓存或请求级缓存，保证所有数据库操作线程安全。
        /// 使用外部数据库驱动会使得性能稍有下降。
        /// </remarks>
        /// </summary>
        public IDatabase DB
        {
            get
            {
                if (String.IsNullOrEmpty(ConnStr)) throw new Exception("请在使用数据库前设置[" + ConnName + "]连接字符串");

                //if (HttpContext.Current == null) // 非Web程序，使用线程级缓存
                return CreateForNotWeb();
                //else
                //    return CreateForWeb();
            }
        }

        private static Dictionary<String, Boolean> IsSql2005 = new Dictionary<String, Boolean>();

        private IDatabase CreateForNotWeb()
        {
            if (_DBs == null) _DBs = new Dictionary<String, IDatabase>();
            if (_DBs.ContainsKey(ConnName)) return _DBs[ConnName];
            lock (_DBs)
            {
                if (_DBs.ContainsKey(ConnName)) return _DBs[ConnName];

                //// 创建对象，先取得程序集，再创建实例，是为了防止在本程序集创建外部DAL类的实例而出错
                ////检查授权
                //if (!License.Check()) return null;

                IDatabase _DB;
                if (DALType == typeof(Access))
                    _DB = new Access();
                else if (DALType == typeof(SqlServer))
                    _DB = new SqlServer();
                else if (DALType == typeof(SqlServer2005))
                    _DB = new SqlServer2005();
                else if (DALType == typeof(Oracle))
                    _DB = new Oracle();
                else if (DALType == typeof(MySql))
                    _DB = new MySql();
                else if (DALType == typeof(SQLite))
                    _DB = new SQLite();
                else
                    _DB = DALType.Assembly.CreateInstance(DALType.FullName, false, BindingFlags.Default, null, new Object[] { ConnStr }, null, null) as IDatabase;

                _DB.ConnectionString = ConnStr;

                //检查是否SqlServer2005
                //_DB = CheckSql2005(_DB);

                if (!IsSql2005.ContainsKey(ConnName))
                {
                    lock (IsSql2005)
                    {
                        if (!IsSql2005.ContainsKey(ConnName))
                        {
                            IsSql2005.Add(ConnName, CheckSql2005(_DB));
                        }
                    }
                }

                if (DALType != typeof(SqlServer2005) && IsSql2005.ContainsKey(ConnName) && IsSql2005[ConnName])
                {
                    _DALType = typeof(SqlServer2005);
                    _DB.Dispose();
                    _DB = new SqlServer2005();
                    _DB.ConnectionString = ConnStr;
                }

                _DBs.Add(ConnName, _DB);

                if (Database.Debug) Database.WriteLog("创建DB（NotWeb）：{0}", _DB.ID);

                return _DB;
            }
        }

        //private IDataBase CreateForWeb()
        //{
        //    String key = ConnName + "_DB";
        //    IDataBase d;

        //    if (HttpContext.Current.Items[key] != null && HttpContext.Current.Items[key] is IDataBase)
        //        d = HttpContext.Current.Items[key] as IDataBase;
        //    else
        //    {
        //        //检查授权
        //        if (!License.Check()) return null;

        //        if (DALType == typeof(Access))
        //            d = new Access();
        //        else if (DALType == typeof(SqlServer))
        //            d = new SqlServer();
        //        else if (DALType == typeof(Oracle))
        //            d = new Oracle();
        //        else if (DALType == typeof(MySql))
        //            d = new MySql();
        //        else if (DALType == typeof(SQLite))
        //            d = new SQLite();
        //        else
        //            d = DALType.Assembly.CreateInstance(DALType.FullName, false, BindingFlags.Default, null, new Object[] { ConnStr }, null, null) as IDataBase;

        //        d.ConnectionString = ConnStr;

        //        if (DataBase.Debug) DataBase.WriteLog("创建DB（Web）：{0}", d.ID);

        //        HttpContext.Current.Items.Add(key, d);
        //    }
        //    //检查是否SqlServer2005
        //    //_DB = CheckSql2005(_DB);

        //    if (!IsSql2005.ContainsKey(ConnName))
        //    {
        //        lock (IsSql2005)
        //        {
        //            if (!IsSql2005.ContainsKey(ConnName))
        //            {
        //                IsSql2005.Add(ConnName, CheckSql2005(d));
        //            }
        //        }
        //    }

        //    if (IsSql2005.ContainsKey(ConnName) && IsSql2005[ConnName])
        //    {
        //        _DALType = typeof(SqlServer2005);
        //        d.Dispose();
        //        d = new SqlServer2005();
        //        d.ConnectionString = ConnStr;
        //    }

        //    return d;
        //}

        //private IDataBase CheckSql2005(IDataBase db)
        //{
        //    //检查是否SqlServer2005
        //    if (db.DbType != DatabaseType.SqlServer) return db;

        //    //取数据库版本
        //    DataSet ds = db.Query("Select @@Version");
        //    if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows != null && ds.Tables[0].Rows.Count > 0)
        //    {
        //        String ver = ds.Tables[0].Rows[0][0].ToString();
        //        if (!String.IsNullOrEmpty(ver) && ver.StartsWith("Microsoft SQL Server 2005"))
        //        {
        //            _DALType = typeof(SqlServer2005);
        //            db.Dispose();
        //            db = new SqlServer2005(ConnStr);
        //        }
        //    }
        //    return db;
        //}

        private Boolean CheckSql2005(IDatabase db)
        {
            //检查是否SqlServer2005
            if (db.DbType != DatabaseType.SqlServer) return false;

            //切换到master库
            Database d = db as Database;
            String dbname = d.DatabaseName;
            //如果指定了数据库名，并且不是master，则切换到master
            if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
            {
                d.DatabaseName = "master";
            }

            //取数据库版本
            Boolean b = false;
            //DataSet ds = db.Query("Select @@Version");
            //if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows != null && ds.Tables[0].Rows.Count > 0)
            //{
            //    String ver = ds.Tables[0].Rows[0][0].ToString();
            //    if (!String.IsNullOrEmpty(ver) && ver.StartsWith("Microsoft SQL Server 2005"))
            //    {
            //        b = true;
            //    }
            //}
            String ver = db.ServerVersion;
            b = !ver.StartsWith("08");

            if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
            {
                d.DatabaseName = dbname;
            }

            return b;
        }

        /// <summary>
        /// 是否存在DB实例。
        /// 如果直接使用DB属性判断是否存在，它将会创建一个实例。
        /// </summary>
        private Boolean ExistDB
        {
            get
            {
                //if (HttpContext.Current == null || HttpContext.Current.Items == null)
                //{
                if (_DBs != null && !_DBs.ContainsKey(ConnName)) return true;
                return false;
                //}
                //else
                //{
                //    String key = ConnName + "_DB";
                //    if (HttpContext.Current.Items[key] != null && HttpContext.Current.Items[key] is IDataBase) return true;
                //    return false;
                //}
            }
        }
        #endregion

        #region 使用缓存后的数据操作方法
        #region 属性
        private Boolean _EnableCache = true;
        /// <summary>
        /// 是否启用缓存。
        /// <remarks>设为false可清空缓存</remarks>
        /// </summary>
        public Boolean EnableCache
        {
            get { return _EnableCache; }
            set
            {
                _EnableCache = value;
                if (!_EnableCache) XCache.RemoveAll();
            }
        }

        /// <summary>
        /// 缓存个数
        /// </summary>
        public Int32 CacheCount
        {
            get
            {
                return XCache.Count;
            }
        }

        [ThreadStatic]
        private static Int32 _QueryTimes;
        /// <summary>
        /// 查询次数
        /// </summary>
        public static Int32 QueryTimes
        {
            //get { return DB != null ? DB.QueryTimes : 0; }
            get { return _QueryTimes; }
        }

        [ThreadStatic]
        private static Int32 _ExecuteTimes;
        /// <summary>
        /// 执行次数
        /// </summary>
        public static Int32 ExecuteTimes
        {
            //get { return DB != null ? DB.ExecuteTimes : 0; }
            get { return _ExecuteTimes; }
        }
        #endregion

        private static Dictionary<String, String> _PageSplitCache = new Dictionary<String, String>();
        /// <summary>
        /// 根据条件把普通查询SQL格式化为分页SQL。
        /// </summary>
        /// <remarks>
        /// 因为需要继承重写的原因，在数据类中并不方便缓存分页SQL。
        /// 所以在这里做缓存。
        /// </remarks>
        /// <param name="sql">SQL语句</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <returns>分页SQL</returns>
        public String PageSplit(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            String cacheKey = String.Format("{0}_{1}_{2}_{3}_", sql, startRowIndex, maximumRows, ConnName);
            if (!String.IsNullOrEmpty(keyColumn)) cacheKey += keyColumn;
            if (_PageSplitCache.ContainsKey(cacheKey)) return _PageSplitCache[cacheKey];
            lock (_PageSplitCache)
            {
                if (_PageSplitCache.ContainsKey(cacheKey)) return _PageSplitCache[cacheKey];
                String s = DB.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
                _PageSplitCache.Add(cacheKey, s);
                return s;
            }
        }

        /// <summary>
        /// 根据条件把普通查询SQL格式化为分页SQL。
        /// </summary>
        /// <remarks>
        /// 因为需要继承重写的原因，在数据类中并不方便缓存分页SQL。
        /// 所以在这里做缓存。
        /// </remarks>
        /// <param name="builder">查询生成器</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <returns>分页SQL</returns>
        public String PageSplit(SelectBuilder builder, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            String cacheKey = String.Format("{0}_{1}_{2}_{3}_", builder.ToString(), startRowIndex, maximumRows, ConnName);
            if (!String.IsNullOrEmpty(keyColumn)) cacheKey += keyColumn;
            if (_PageSplitCache.ContainsKey(cacheKey)) return _PageSplitCache[cacheKey];
            lock (_PageSplitCache)
            {
                if (_PageSplitCache.ContainsKey(cacheKey)) return _PageSplitCache[cacheKey];
                String s = DB.PageSplit(builder, startRowIndex, maximumRows, keyColumn);
                _PageSplitCache.Add(cacheKey, s);
                return s;
            }
        }

        /// <summary>
        /// 执行SQL查询，返回记录集
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableNames">所依赖的表的表名</param>
        /// <returns></returns>
        public DataSet Select(String sql, String[] tableNames)
        {
            String cacheKey = sql + "_" + ConnName;
            if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            DataSet ds = DB.Query(sql);
            if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            return ds;
        }

        /// <summary>
        /// 执行SQL查询，返回记录集
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableName">所依赖的表的表名</param>
        /// <returns></returns>
        public DataSet Select(String sql, String tableName)
        {
            return Select(sql, new String[] { tableName });
        }

        /// <summary>
        /// 执行SQL查询，返回分页记录集
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <param name="tableNames">所依赖的表的表名</param>
        /// <returns></returns>
        public DataSet Select(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String[] tableNames)
        {
            //String cacheKey = sql + "_" + startRowIndex + "_" + maximumRows + "_" + ConnName;
            //if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            //Interlocked.Increment(ref _QueryTimes);
            //DataSet ds = DB.Query(PageSplit(sql, startRowIndex, maximumRows, keyColumn));
            //if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            //return ds;

            return Select(PageSplit(sql, startRowIndex, maximumRows, keyColumn), tableNames);
        }

        /// <summary>
        /// 执行SQL查询，返回分页记录集
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <param name="tableName">所依赖的表的表名</param>
        /// <returns></returns>
        public DataSet Select(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String tableName)
        {
            return Select(sql, startRowIndex, maximumRows, keyColumn, new String[] { tableName });
        }

        /// <summary>
        /// 执行SQL查询，返回总记录数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableNames">所依赖的表的表名</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, String[] tableNames)
        {
            String cacheKey = sql + "_SelectCount" + "_" + ConnName;
            if (EnableCache && XCache.IntContain(cacheKey)) return XCache.IntItem(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            Int32 rs = DB.QueryCount(sql);
            if (EnableCache) XCache.Add(cacheKey, rs, tableNames);
            return rs;
        }

        /// <summary>
        /// 执行SQL查询，返回总记录数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableName">所依赖的表的表名</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, String tableName)
        {
            return SelectCount(sql, new String[] { tableName });
        }

        /// <summary>
        /// 执行SQL查询，返回总记录数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <param name="tableNames">所依赖的表的表名</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String[] tableNames)
        {
            return SelectCount(PageSplit(sql, startRowIndex, maximumRows, keyColumn), tableNames);
        }

        /// <summary>
        /// 执行SQL查询，返回总记录数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="startRowIndex">开始行，0开始</param>
        /// <param name="maximumRows">最大返回行数</param>
        /// <param name="keyColumn">唯一键。用于not in分页</param>
        /// <param name="tableName">所依赖的表的表名</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String tableName)
        {
            return SelectCount(sql, startRowIndex, maximumRows, keyColumn, new String[] { tableName });
        }

        /// <summary>
        /// 执行SQL语句，返回受影响的行数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableNames">受影响的表的表名</param>
        /// <returns></returns>
        public Int32 Execute(String sql, String[] tableNames)
        {
            // 移除所有和受影响表有关的缓存
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            return DB.Execute(sql);
        }

        /// <summary>
        /// 执行SQL语句，返回受影响的行数
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableName">受影响的表的表名</param>
        /// <returns></returns>
        public Int32 Execute(String sql, String tableName)
        {
            return Execute(sql, new String[] { tableName });
        }

        /// <summary>
        /// 执行插入语句并返回新增行的自动编号
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="tableNames">受影响的表的表名</param>
        /// <returns>新增行的自动编号</returns>
        public Int32 InsertAndGetIdentity(String sql, String[] tableNames)
        {
            // 移除所有和受影响表有关的缓存
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            return DB.InsertAndGetIdentity(sql);
        }

        /// <summary>
        /// 执行插入语句并返回新增行的自动编号
        /// </summary>
        /// <param name="sql">SQL语句</param>
        /// <param name="tableName">受影响的表的表名</param>
        /// <returns>新增行的自动编号</returns>
        public Int32 InsertAndGetIdentity(String sql, String tableName)
        {
            return InsertAndGetIdentity(sql, new String[] { tableName });
        }

        /// <summary>
        /// 执行CMD，返回记录集
        /// </summary>
        /// <param name="cmd">CMD</param>
        /// <param name="tableNames">所依赖的表的表名</param>
        /// <returns></returns>
        public DataSet Select(DbCommand cmd, String[] tableNames)
        {
            String cacheKey = cmd.CommandText + "_" + ConnName;
            if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            DataSet ds = DB.Query(cmd);
            if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            DB.AutoClose();
            return ds;
        }

        /// <summary>
        /// 执行CMD，返回受影响的行数
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tableNames"></param>
        /// <returns></returns>
        public Int32 Execute(DbCommand cmd, String[] tableNames)
        {
            // 移除所有和受影响表有关的缓存
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            Int32 ret = DB.Execute(cmd);
            DB.AutoClose();
            return ret;
        }

        ///// <summary>
        ///// 获取一个DbCommand。
        ///// 配置了连接，并关联了事务。
        ///// 连接已打开。
        ///// 使用完毕后，必须调用AutoClose方法，以使得在非事务及设置了自动关闭的情况下关闭连接。
        ///// 除非迫不得已，否则，请不要使用该方法，可以考虑用Select(cmd)和Execute(cmd)来代替。
        ///// 非法使用会使得资源失去控制。极度危险！
        ///// </summary>
        ///// <returns></returns>
        //private DbCommand PrepareCommand()
        //{
        //    return DB.PrepareCommand();
        //}

        private IList<XTable> _Tables;
        /// <summary>
        /// 取得所有表和视图的构架信息
        /// </summary>
        /// <remarks>如果不存在缓存，则获取后返回；否则使用线程池线程获取，而主线程返回缓存</remarks>
        /// <returns></returns>
        public IList<XTable> Tables
        {
            get
            {
                // 如果不存在缓存，则获取后返回；否则使用线程池线程获取，而主线程返回缓存
                if (_Tables == null)
                    _Tables = GetTables();
                else
                    ThreadPool.QueueUserWorkItem(delegate(Object state) { _Tables = GetTables(); });

                return _Tables;
            }
        }

        private IList<XTable> GetTables()
        {
            List<XTable> list = DB.GetTables();
            if (list != null && list.Count > 0) list.Sort(delegate(XTable item1, XTable item2) { return item1.Name.CompareTo(item2.Name); });
            return list;
        }
        #endregion

        #region 事务
        /// <summary>
        /// 开始事务。
        /// 事务一旦开始，请务必在操作完成后提交或者失败时回滚，否则可能会造成资源失去控制。极度危险！
        /// </summary>
        /// <returns></returns>
        public Int32 BeginTransaction()
        {
            return DB.BeginTransaction();
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <returns></returns>
        public Int32 Commit()
        {
            return DB.Commit();
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <returns></returns>
        public Int32 Rollback()
        {
            return DB.Rollback();
        }
        #endregion

        #region 导入导出
        /// <summary>
        /// 导出架构信息
        /// </summary>
        /// <returns></returns>
        public String Export()
        {
            IList<XTable> list = Tables;

            if (list == null || list.Count < 1) return null;

            XmlSerializer serializer = new XmlSerializer(typeof(XTable[]));
            using (StringWriter sw = new StringWriter())
            {
                serializer.Serialize(sw, list);
                return sw.ToString();
            }
        }

        /// <summary>
        /// 导入架构信息
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static XTable[] Import(String xml)
        {
            if (String.IsNullOrEmpty(xml)) return null;

            XmlSerializer serializer = new XmlSerializer(typeof(XTable[]));
            using (StringReader sr = new StringReader(xml))
            {
                return serializer.Deserialize(sr) as XTable[];
            }
        }
        #endregion

        #region 创建数据操作实体
        /// <summary>
        /// 创建实体操作接口
        /// </summary>
        /// <remarks>因为只用来做实体操作，所以只需要一个实例即可</remarks>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public IEntityOperate CreateOperate(String tableName)
        {
            Assembly asm = EntityAssembly.Create(this);
            Type type = asm.GetType(tableName);
            if (type == null)
            {
                Type[] ts = asm.GetTypes();
                foreach (Type item in ts)
                {
                    if (item.Name == tableName)
                    {
                        type = item;
                        break;
                    }
                }

                if (type == null) return null;
            }

            return EntityFactory.CreateOperate(type);
        }
        #endregion
    }
}