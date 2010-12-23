﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.ComponentModel;

namespace XCode.Configuration
{
    /// <summary>
    /// 数据属性元数据以及特性
    /// </summary>
    public class FieldItem
    {
        private PropertyInfo _Property;
        /// <summary>属性元数据</summary>
        public PropertyInfo Property
        {
            get { return _Property; }
            internal set { _Property = value; }
        }

        private BindColumnAttribute _Column;
        /// <summary>绑定列特性</summary>
        public BindColumnAttribute Column
        {
            get { return _Column; }
            internal set { _Column = value; }
        }

        private DataObjectFieldAttribute _DataObjectField;
        /// <summary>数据字段特性</summary>
        public DataObjectFieldAttribute DataObjectField
        {
            get { return _DataObjectField; }
            internal set { _DataObjectField = value; }
        }

        private String _Name;
        /// <summary>
        /// 属性名
        /// </summary>
        public String Name
        {
            get
            {
                if (_Name == null)
                {
                    if (Property != null)
                        _Name = Property.Name;
                    else
                        _Name = String.Empty;
                }
                return _Name;
            }
        }

        private String _ColumnName;
        /// <summary>
        /// 绑定的字段名
        /// 默认使用BindColumn特性中指定的字段名，如果没有指定，则使用属性名。
        /// </summary>
        public String ColumnName
        {
            get
            {
                if (_ColumnName == null)
                {
                    if (Column != null && !String.IsNullOrEmpty(Column.Name))
                        _ColumnName = Column.Name;
                    else
                        _ColumnName = Property.Name;
                }
                return _ColumnName;
            }
        }

        /// <summary>
        /// 中文名
        /// </summary>
        public String DisplayName
        {
            get
            {
                if (Column == null || String.IsNullOrEmpty(Column.Description)) return "";
                return Column.Description;
            }
        }

        /// <summary>字段名（去除左右中括号）</summary>
        internal String ColumnNameEx
        {
            get { return ColumnName.Trim(new Char[] { '[', ']' }); }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FieldItem() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pi"></param>
        public FieldItem(PropertyInfo pi)
        {
            Property = pi;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pi"></param>
        /// <param name="bc"></param>
        public FieldItem(PropertyInfo pi, BindColumnAttribute bc)
        {
            Property = pi;
            Column = bc;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pi"></param>
        /// <param name="bc"></param>
        /// <param name="dof"></param>
        public FieldItem(PropertyInfo pi, BindColumnAttribute bc, DataObjectFieldAttribute dof)
        {
            Property = pi;
            Column = bc;
            DataObjectField = dof;
        }

        /// <summary>
        /// 已重载。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
