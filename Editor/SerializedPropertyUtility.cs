using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal static class SerializedPropertyUtility
    {

        public static IEnumerable<SerializedProperty>
        EnumerateChildProperties(this SerializedObject serializedObject)
        {
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(enterChildren: true))
            {
                // yield return property; // skip "m_Script"
                while (iterator.NextVisible(enterChildren: false))
                {
                    yield return iterator;
                }
            }
        }

        public static IEnumerable<SerializedProperty>
        EnumerateChildProperties(this SerializedProperty property)
        {
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            if (iterator.NextVisible(enterChildren: true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end))
                        yield break;

                    yield return iterator;
                }
                while (iterator.NextVisible(enterChildren: false));
            }
        }

        //----------------------------------------------------------------------

        public static SerializedProperty
        FindParentProperty(this SerializedProperty property)
        {
            var serializedObject = property.serializedObject;
            var propertyPath = property.propertyPath;
            var propertyKeys = ParsePropertyPath(propertyPath).ToArray();
            var propertyKeyCount = propertyKeys.Length;
            if (propertyKeyCount == 1) {
                return null; // parent is serialized object
            }
            var lastPropertyKey = propertyKeys[propertyKeyCount - 1];
            if (lastPropertyKey is int) {
                // parent is an array, drop [Array,data,N] from path
                var parentKeys = propertyKeys.Take(propertyKeyCount - 3);
                var parentPath = JoinPropertyPath(parentKeys);
                return serializedObject.FindProperty(parentPath);
            } else {
                // parent is a structure, drop [name] from path
                Debug.Assert(lastPropertyKey is string);
                var parentKeys = propertyKeys.Take(propertyKeyCount - 1);
                var parentPath = JoinPropertyPath(parentKeys);
                return serializedObject.FindProperty(parentPath);
            }
        }

        //----------------------------------------------------------------------

        public static object FindObject(this object obj, IEnumerable<object> path)
        {
            foreach (var key in path)
            {
                if (key is string)
                {
                    var objType = obj.GetType();
                    var fieldName = (string)key;
                    var fieldInfo = objType.FindFieldInfo(fieldName);
                    if (fieldInfo == null)
                        throw FieldNotFoundException(objType, fieldName);
                    obj = fieldInfo.GetValue(obj);
                    continue;
                }
                if (key is int)
                {
                    var elementIndex = (int)key;
                    var array = (IList)obj;
                    obj = array[elementIndex];
                    continue;
                }
            }
            return obj;
        }

        public static object GetObject(this SerializedProperty property)
        {
            var obj = property.serializedObject.targetObject;
            var path = ParseValuePath(property);
            return FindObject(obj, path);
        }

        private static Exception FieldNotFoundException(Type type, string fieldName)
        {
            return new KeyNotFoundException( string.Format( "{0}.{1} not found", type, fieldName ) );
        }

        //----------------------------------------------------------------------

        public static bool
        IsArrayOrList(this SerializedProperty property) {
            return (
                property.propertyType == SerializedPropertyType.Generic &&
                property.isArray      == true
            );
        }

        public static bool
        IsStructure(this SerializedProperty property) {
            return (
                property.propertyType == SerializedPropertyType.Generic &&
                property.isArray      == false &&
                property.hasChildren  == true
            );
        }

        //----------------------------------------------------------------------

        private static FieldInfo FindFieldInfo(this Type type, string fieldName)
        {
            const BindingFlags bindingFlags =
                BindingFlags.FlattenHierarchy |
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            var fieldInfo = type.GetField(fieldName, bindingFlags);
            if (fieldInfo != null)
                return fieldInfo;

            var baseType = type.BaseType;
            if (baseType == null)
                return null;

            return FindFieldInfo(baseType, fieldName);
        }

        //----------------------------------------------------------------------

        private static IEnumerable<object> ParsePropertyPath(SerializedProperty property)
        {
            return ParsePropertyPath(property.propertyPath);
        }

        private static IEnumerable<object> ParsePropertyPath(string propertyPath)
        {
            return ParseValuePath(propertyPath);
        }

        public static string JoinPropertyPath(IEnumerable<object> keys)
        {
            return JoinValuePath(keys);
        }

        //----------------------------------------------------------------------

        private static string GetValuePath(SerializedProperty property)
        {
            return property.propertyPath.Replace(".Array.data[","[");
        }

        private static IEnumerable<object> ParseValuePath(SerializedProperty property)
        {
            return ParseValuePath(GetValuePath(property));
        }

        private static IEnumerable<object> ParseValuePath(string fieldPath)
        {
            var keys = fieldPath.Split('.');
            foreach (var key in keys)
            {
                if (key.IsElementIdentifier())
                {
                    var subkeys = key.Split('[', ']');
                    yield return subkeys[0];
                    foreach (var subkey in subkeys.Skip(1))
                    {
                        if (string.IsNullOrEmpty(subkey))
                        {
                            continue;
                        }
                        int index = int.Parse(subkey);
                        yield return index;
                    }
                    continue;
                }
                if (key.IsElementIndex())
                {
                    var subkeys = key.Split('[', ']');
                    foreach (var subkey in subkeys)
                    {
                        if (string.IsNullOrEmpty(subkey))
                        {
                            continue;
                        }
                        int index = int.Parse(subkey);
                        yield return index;
                    }
                    continue;
                }
                if (key.IsMemberIdentifier())
                {
                    yield return key;
                    continue;
                }
                throw new Exception(string.Format(
                    "invalid path: {0}", fieldPath
                ));
            }
        }

        public static string JoinValuePath(IEnumerable<object> keys)
        {
            var builder = new StringBuilder();
            foreach (var key in keys)
            {
                if (key is string)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('.');
                    }
                    builder.Append((string) key);
                    continue;
                }
                if (key is int)
                {
                    builder.Append('[');
                    builder.Append((int) key);
                    builder.Append(']');
                    continue;
                }
                throw new Exception(string.Format(
                    "invalid key: {0}", key
                ));
            }
            return builder.ToString();
        }

        //----------------------------------------------------------------------

        private static readonly Regex
        ElementIdentifier = new Regex(@"^[_a-zA-Z][_a-zA-Z0-9]*(\[[0-9]*\])+$");
        // e.g. "foo[0][1]"

        private static readonly Regex
        ElementIndex = new Regex(@"^(\[[0-9]*\])+$");

        private static readonly Regex
        MemberIdentifier = new Regex(@"^[_a-zA-Z][_a-zA-Z0-9]*$");
        // e.g. "foo"

        //----------------------------------------------------------------------

        private static bool IsElementIdentifier(this string s)
        {
            return ElementIdentifier.IsMatch(s);
        }

        private static bool IsElementIndex(this string s)
        {
            return ElementIndex.IsMatch(s);
        }

        private static bool IsMemberIdentifier(this string s)
        {
            return MemberIdentifier.IsMatch(s);
        }

        //======================================================================

        private static System.Exception
        UnsupportedValue(SerializedProperty property, object value) {
            var serializedObject = property.serializedObject;
            var targetObject = serializedObject.targetObject;
            var targetType = targetObject.GetType();
            var targetTypeName = targetType.Name;
            var propertyPath = property.propertyPath;
            return new System.Exception(string.Format(
                "unsupported value {0} for {1}.{2}",
                value, targetTypeName, propertyPath
            ));
        }

        private static System.Exception
        UnsupportedValue(
            SerializedProperty property,
            object value,
            string expected
        ) {
            var serializedObject = property.serializedObject;
            var targetObject = serializedObject.targetObject;
            var targetType = targetObject.GetType();
            var targetTypeName = targetType.Name;
            var propertyPath = property.propertyPath;
            if (value == null) {
                value = "null";
            } else {
                value = string.Format("'{0}'", value);
            }
            return new System.Exception(string.Format(
                "unsupported value {0} for {1}.{2}, expected {3}",
                value, targetTypeName, propertyPath, expected
            ));
        }

        private static System.Exception
        UnsupportedValueType(SerializedProperty property) {
            var serializedObject = property.serializedObject;
            var targetObject = serializedObject.targetObject;
            var targetType = targetObject.GetType();
            var targetTypeName = targetType.Name;
            var valueTypeName = property.propertyType.ToString();
            var propertyPath = property.propertyPath;
            return new System.Exception(string.Format(
                "unsupported value type {0} for {1}.{2}",
                valueTypeName, targetTypeName, propertyPath
            ));
        }

    }

}