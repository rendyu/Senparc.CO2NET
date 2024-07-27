﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Senparc.CO2NET.MagicObject
{
    public class MO<T>
    {
        private T OriginalObject { get; set; }
        private T Object { get; set; }
        private Dictionary<string, PropertyChangeResult<object>> _changes;
        private T _snapshot;

        public event EventHandler<string> PropertyChanged;

        public MO(T obj)
        {
            OriginalObject = Clone(obj);
            Object = obj;
            _changes = new Dictionary<string, PropertyChangeResult<object>>();
        }

        public MO<T> Set<TValue>(Expression<Func<T, TValue>> expression, TValue value)
        {
            MemberExpression memberExpression = null;
            if (expression.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else if (expression.Body is MemberExpression)
            {
                memberExpression = expression.Body as MemberExpression;
            }

            if (memberExpression != null && memberExpression.Member is PropertyInfo propertyInfo)
            {
                var originalValue = (TValue)propertyInfo.GetValue(OriginalObject);
                var newValue = value;
                var propertyName = propertyInfo.Name;

                if (!Equals(originalValue, newValue))
                {
                    propertyInfo.SetValue(Object, newValue);
                    var changeResult = new PropertyChangeResult<object>
                    {
                        OldValue = originalValue,
                        NewValue = newValue,
                        IsChanged = true
                    };
                    _changes[propertyName] = changeResult;
                    PropertyChanged?.Invoke(this, propertyName);
                }
            }
            else
            {
                throw new ArgumentException("表达式必须是一个属性访问表达式", nameof(expression));
            }

            return this;
        }


        public PropertyChangeResult<TValue> Get<TValue>(Expression<Func<T, TValue>> expression)
        {
            if (expression.Body is MemberExpression memberExpression && memberExpression.Member is PropertyInfo propertyInfo)
            {
                var hasSnapshot = _snapshot != null;

                var originalValue = (TValue)propertyInfo.GetValue(OriginalObject);

                TValue? snapshptValue = hasSnapshot ? (TValue?)propertyInfo.GetValue(_snapshot) : default;
                var newValue = (TValue)propertyInfo.GetValue(Object);

                return new PropertyChangeResult<TValue>
                {
                    OldValue = originalValue,
                    SnapshotValue = snapshptValue,
                    NewValue = newValue,
                    IsChanged = /*hasSnapshot ? Equals(snapshptValue, newValue) :*/ !Equals(originalValue, newValue),
                    HasShapshot = hasSnapshot
                };
            }
            else
            {
                throw new ArgumentException("表达式必须是一个属性访问表达式", nameof(expression));
            }
        }

        public Dictionary<string, PropertyChangeResult<object>> GetChanges()
        {
            return _changes;
        }

        public void Reset()
        {
            Object = Clone(OriginalObject);
            _changes.Clear();
        }

        public bool HasChanges()
        {
            return _changes.Count > 0;
        }

        public void SetProperties(Dictionary<Expression<Func<T, object>>, object> properties)
        {
            foreach (var property in properties)
            {
                if (property.Key.Body is UnaryExpression unaryExpression && unaryExpression.Operand is MemberExpression memberExpression)
                {
                    Set(Expression.Lambda<Func<T, object>>(Expression.Convert(memberExpression, typeof(object)), property.Key.Parameters), property.Value);
                }
                else if (property.Key.Body is MemberExpression memberExpression2)
                {
                    Set(Expression.Lambda<Func<T, object>>(Expression.Convert(memberExpression2, typeof(object)), property.Key.Parameters), property.Value);
                }
                else
                {
                    throw new ArgumentException("表达式必须是一个属性访问表达式", nameof(property.Key));
                }
            }
        }



        public void TakeSnapshot()
        {
            _snapshot = Clone(Object);
        }

        public void RestoreSnapshot()
        {
            if (_snapshot != null)
            {
                Object = Clone(_snapshot);
                _changes.Clear();
            }
        }

        private T Clone(T source)
        {
            var cloneMethod = source.GetType().GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)cloneMethod.Invoke(source, null);
        }

        public void RevertChanges()
        {
            var properties = OriginalObject.GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    var originalValue = property.GetValue(OriginalObject);
                    property.SetValue(Object, originalValue);
                }
            }
            _changes.Clear();
        }

    }
}
