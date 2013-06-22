﻿/*
 * XCoder v4.3.2011.0913
 * 作者：nnhy/NEWLIFE
 * 时间：2011-09-14 18:01:27
 * 版权：版权所有 (C) 新生命开发团队 2011
*/
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using XCode;
using XCode.Configuration;

#if DEBUG
namespace XCode.Test
{
    /// <summary>实体测试</summary>
    [ModelCheckMode(ModelCheckModes.CheckTableWhenFirstUse)]
    public class EntityTest : EntityTest<EntityTest> { }

    /// <summary>实体测试</summary>
    public partial class EntityTest<TEntity> : MyEntity<TEntity> where TEntity : EntityTest<TEntity>, new()
    {
        #region 扩展属性﻿
        #endregion

        #region 扩展查询﻿
        /// <summary>根据主键查询一个实体测试实体对象用于表单编辑</summary>
        /// <param name="guid">主键一</param>
        /// <param name="guid2">主键二</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKeyForEdit(Guid guid, String guid2)
        {
            TEntity entity = Find(new String[] { _.Guid, _.Guid2 }, new Object[] { guid, guid2 });
            if (entity == null)
            {
                entity = new TEntity();
            }
            return entity;
        }


        /// <summary>根据名称查找</summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByName(String name)
        {
            if (Meta.Count >= 1000)
                return Find(new String[] { _.Name }, new Object[] { name });
            else // 实体缓存
                return Meta.Cache.Entities.Find(_.Name, name);
            // 单对象缓存
            //return Meta.SingleCache[name];
        }

        /// <summary>根据启用、开始日期、结束时间查找</summary>
        /// <param name="isenable">启用</param>
        /// <param name="startdate">开始日期</param>
        /// <param name="endtime">结束时间</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllByIsEnableAndStartDateAndEndTime(Boolean isenable, DateTime startdate, DateTime endtime)
        {
            return FindAll(new String[] { _.IsEnable, _.StartDate, _.EndTime }, new Object[] { isenable, startdate, endtime });
        }

        /// <summary>根据自增编号查找</summary>
        /// <param name="id">自增编号</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByID(SByte id)
        {
            if (Meta.Count >= 1000)
                return Find(new String[] { _.ID }, new Object[] { id });
            else // 实体缓存
                return Meta.Cache.Entities.Find(_.ID, id);
            // 单对象缓存
            //return Meta.SingleCache[id];
        }

        /// <summary>根据主键一、主键二查找</summary>
        /// <param name="guid">主键一</param>
        /// <param name="guid2">主键二</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByGuidAndGuid2(Guid guid, String guid2)
        {
            return Find(new String[] { _.Guid, _.Guid2 }, new Object[] { guid, guid2 });
        }
        #endregion

        #region 对象操作﻿
        ///// <summary>
        ///// 已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert
        ///// </summary>
        ///// <returns></returns>
        //public override Int32 Insert()
        //{
        //    return base.Insert();
        //}

        ///// <summary>
        ///// 已重载。在事务保护范围内处理业务，位于Valid之后
        ///// </summary>
        ///// <returns></returns>
        //protected override Int32 OnInsert()
        //{
        //    return base.OnInsert();
        //}

        ///// <summary>
        ///// 验证数据，通过抛出异常的方式提示验证失败。
        ///// </summary>
        ///// <param name="isNew"></param>
        //public override void Valid(Boolean isNew)
        //{
        //    // 建议先调用基类方法，基类方法会对唯一索引的数据进行验证
        //    base.Valid(isNew);

        //    // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        //    if (String.IsNullOrEmpty(_.Name)) throw new ArgumentNullException(_.Name, _.Name.Description + "无效！");
        //    if (!isNew && ID < 1) throw new ArgumentOutOfRangeException(_.ID, _.ID.Description + "必须大于0！");

        //    // 在新插入数据或者修改了指定字段时进行唯一性验证，CheckExist内部抛出参数异常
        //    if (isNew || Dirtys[_.Name]) CheckExist(_.Name);
        //    if (isNew || Dirtys[_.Name] || Dirtys[_.DbType]) CheckExist(_.Name, _.DbType);
        //    if ((isNew || Dirtys[_.Name]) && Exist(_.Name)) throw new ArgumentException(_.Name, "值为" + Name + "的" + _.Name.Description + "已存在！");
        //}


        ///// <summary>
        ///// 首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法
        ///// </summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    base.InitData();

        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    // Meta.Count是快速取得表记录数
        //    if (Meta.Count > 0) return;

        //    // 需要注意的是，如果该方法调用了其它实体类的首次数据库操作，目标实体类的数据初始化将会在同一个线程完成
        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化{0}管理员数据……", typeof(TEntity).Name);

        //    TEntity user = new TEntity();
        //    user.Name = "admin";
        //    user.Password = DataHelper.Hash("admin");
        //    user.DisplayName = "管理员";
        //    user.RoleID = 1;
        //    user.IsEnable = true;
        //    user.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化{0}管理员数据！", typeof(TEntity).Name);
        //}
        #endregion

        #region 高级查询
        // 以下为自定义高级查询的例子

        ///// <summary>
        ///// 查询满足条件的记录集，分页、排序
        ///// </summary>
        ///// <param name="key">关键字</param>
        ///// <param name="orderClause">排序，不带Order By</param>
        ///// <param name="startRowIndex">开始行，0表示第一行</param>
        ///// <param name="maximumRows">最大返回行数，0表示所有行</param>
        ///// <returns>实体集</returns>
        //[DataObjectMethod(DataObjectMethodType.Select, true)]
        //public static EntityList<TEntity> Search(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        //{
        //    return FindAll(SearchWhere(key), orderClause, null, startRowIndex, maximumRows);
        //}

        ///// <summary>
        ///// 查询满足条件的记录总数，分页和排序无效，带参数是因为ObjectDataSource要求它跟Search统一
        ///// </summary>
        ///// <param name="key">关键字</param>
        ///// <param name="orderClause">排序，不带Order By</param>
        ///// <param name="startRowIndex">开始行，0表示第一行</param>
        ///// <param name="maximumRows">最大返回行数，0表示所有行</param>
        ///// <returns>记录数</returns>
        //public static Int32 SearchCount(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        //{
        //    return FindCount(SearchWhere(key), null, null, 0, 0);
        //}

        /// <summary>构造搜索条件</summary>
        /// <param name="key">关键字</param>
        /// <returns></returns>
        private static String SearchWhere(String key)
        {
            // WhereExpression重载&和|运算符，作为And和Or的替代
            var exp = SearchWhereByKeys(key);

            // 以下仅为演示，2、3行是同一个意思的不同写法，FieldItem重载了等于以外的运算符（第4行）
            //exp &= _.Name.Equal("testName")
            //    & !String.IsNullOrEmpty(key) & _.Name.Equal(key)
            //    .AndIf(!String.IsNullOrEmpty(key), _.Name.Equal(key))
            //    | _.ID > 0;

            return exp;
        }
        #endregion

        #region 扩展操作
        #endregion

        #region 业务
        #endregion
    }
}
#endif