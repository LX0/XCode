﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using XCode.Configuration;
using XCode.DataAccessLayer;
using NewLife.Reflection;
using NewLife.Collections;

namespace XCode
{
    interface IDataRowEntityAccessor
    {
        /// <summary>
        /// 加载数据表
        /// </summary>
        /// <param name="dt">数据表</param>
        /// <returns>实体数组</returns>
        IEntityList LoadData(DataTable dt);

        /// <summary>
        /// 从一个数据行对象加载数据。不加载关联对象。
        /// </summary>
        /// <param name="dr">数据行</param>
        /// <param name="entity">实体对象</param>
        void LoadData(DataRow dr, IEntity entity);

        /// <summary>
        /// 从一个数据行对象加载数据。不加载关联对象。
        /// </summary>
        /// <param name="dr">数据读写器</param>
        /// <param name="entity">实体对象</param>
        void LoadData(IDataReader dr, IEntity entity);

        /// <summary>
        /// 把数据复制到数据行对象中。
        /// </summary>
        /// <param name="entity">实体对象</param>
        /// <param name="dr">数据行</param>
        DataRow ToData(IEntity entity, ref DataRow dr);
    }

    class DataRowEntityAccessor : IDataRowEntityAccessor
    {
        #region 属性
        private Type _EntityType;
        /// <summary>实体类</summary>
        public Type EntityType
        {
            get { return _EntityType; }
            set { _EntityType = value; }
        }

        private IEntityOperate _Factory;
        /// <summary>实体操作者</summary>
        public IEntityOperate Factory
        {
            get { return _Factory ?? (_Factory = EntityFactory.CreateOperate(EntityType)); }
            set { _Factory = value; }
        }

        public DataRowEntityAccessor(Type type) { EntityType = type; }
        #endregion

        #region 存取
        /// <summary>
        /// 加载数据表
        /// </summary>
        /// <param name="dt">数据表</param>
        /// <returns>实体数组</returns>
        public IEntityList LoadData(DataTable dt)
        {
            if (dt == null || dt.Rows.Count < 1) return null;

            // 准备好实体列表
            //EntityList<TEntity> list = new EntityList<TEntity>(dt.Rows.Count);
            IEntityList list = TypeX.CreateInstance(typeof(EntityList<>).MakeGenericType(EntityType), dt.Rows.Count) as IEntityList;

            List<String> ps = new List<String>();
            foreach (DataColumn item in dt.Columns)
            {
                ps.Add(item.ColumnName);
            }

            // 遍历每一行数据，填充成为实体
            foreach (DataRow dr in dt.Rows)
            {
                //TEntity obj = new TEntity();
                // 由实体操作者创建实体对象，因为实体操作者可能更换
                IEntity obj = Factory.Create();
                LoadData(dr, obj, ps);
                list.Add(obj);
            }
            return list;
        }

        /// <summary>
        /// 从一个数据行对象加载数据。不加载关联对象。
        /// </summary>
        /// <param name="dr">数据行</param>
        /// <param name="entity">实体对象</param>
        public void LoadData(DataRow dr, IEntity entity)
        {
            if (dr == null) return;

            List<String> ps = new List<String>();
            foreach (DataColumn item in dr.Table.Columns)
            {
                ps.Add(item.ColumnName);
            }
            LoadData(dr, entity, ps);
        }

        /// <summary>
        /// 从一个数据行对象加载数据。不加载关联对象。
        /// </summary>
        /// <param name="dr">数据读写器</param>
        /// <param name="entity">实体对象</param>
        public void LoadData(IDataReader dr, IEntity entity)
        {
            if (dr == null) return;

            // IDataReader的GetSchemaTable方法太浪费资源了
            for (int i = 0; i < dr.FieldCount; i++)
            {
                SetValue(entity, dr.GetName(i), dr.GetValue(i));
            }
        }

        /// <summary>
        /// 把数据复制到数据行对象中。
        /// </summary>
        /// <param name="entity">实体对象</param>
        /// <param name="dr">数据行</param>
        public DataRow ToData(IEntity entity, ref DataRow dr)
        {
            if (dr == null) return null;

            List<String> ps = new List<String>();
            foreach (FieldItem fi in Factory.AllFields)
            {
                // 检查dr中是否有该属性的列。考虑到Select可能是不完整的，此时，只需要局部填充
                if (dr.Table.Columns.Contains(fi.ColumnName))
                {
                    dr[fi.ColumnName] = entity[fi.Name];
                }

                ps.Add(fi.ColumnName);
            }

            // 扩展属性也写入
            if (entity.Extends != null && entity.Extends.Count > 0)
            {
                foreach (String item in entity.Extends.Keys)
                {
                    try
                    {
                        if (!ps.Contains(item) && dr.Table.Columns.Contains(item))
                            dr[item] = entity.Extends[item];
                    }
                    catch { }
                }
            }
            return dr;
        }
        #endregion

        #region 方法
        static String[] TrueString = new String[] { "true", "y", "yes", "1" };
        static String[] FalseString = new String[] { "false", "n", "no", "0" };

        private void LoadData(DataRow dr, IEntity entity, List<String> ps)
        {
            if (dr == null) return;

            foreach (String item in ps)
            {
                SetValue(entity, item, dr[item]);
            }
        }

        private void SetValue(IEntity entity, String name, Object value)
        {
            // 注意：name并不一定是实体类的成员
            Object oldValue = entity[name];

            Type type = null;
            if (oldValue != null) type = oldValue.GetType();
            if (type == null) GetFieldTypeByName(name);

            // 不处理相同数据的赋值
            if (Object.Equals(value, oldValue)) return;

            if (type == typeof(String))
            {
                // 不处理空字符串对空字符串的赋值
                if (value != null && String.IsNullOrEmpty(value.ToString()))
                {
                    if (oldValue == null || String.IsNullOrEmpty(oldValue.ToString())) return;
                }
            }
            else if (type == typeof(Boolean))
            {
                // 处理字符串转为布尔型
                if (value != null && value.GetType() == typeof(String))
                {
                    String vs = value.ToString();
                    if (String.IsNullOrEmpty(vs))
                        value = false;
                    else
                    {
                        if (Array.IndexOf(TrueString, vs.ToLower()) >= 0)
                            value = true;
                        else if (Array.IndexOf(FalseString, vs.ToLower()) >= 0)
                            value = false;

                        if (DAL.Debug) DAL.WriteLog("无法把字符串{0}转为布尔型！", vs);
                    }
                }
            }

            //不影响脏数据的状态
            Boolean? b = null;
            if (entity.Dirtys.ContainsKey(name)) b = entity.Dirtys[name];

            entity[name] = value == DBNull.Value ? null : value;

            if (b != null)
                entity.Dirtys[name] = b.Value;
            else
                entity.Dirtys.Remove(name);
        }

        DictionaryCache<String, Type> nameTypes = new DictionaryCache<string, Type>();
        Type GetFieldTypeByName(String name)
        {
            return nameTypes.GetItem(name, delegate(String key)
            {
                foreach (FieldItem item in Factory.AllFields)
                {
                    if (item.ColumnName == key) return item.Type;
                }
                return null;
            });
        }
        #endregion
    }
}