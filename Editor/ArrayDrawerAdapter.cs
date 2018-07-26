using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    internal class ArrayDrawerAdapter : PropertyDrawer
    {

        private static readonly FieldInfo
        s_Attribute =
            typeof(PropertyDrawer)
            .GetField(
                "m_Attribute",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private static readonly FieldInfo
        s_FieldInfo =
            typeof(PropertyDrawer)
            .GetField(
                "m_FieldInfo",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private readonly ArrayDrawer m_ArrayDrawer;

        internal ArrayDrawerAdapter(ArrayDrawer arrayDrawer)
        {
            s_Attribute.SetValue(this, arrayDrawer.attribute);
            m_ArrayDrawer = arrayDrawer;
        }

        //----------------------------------------------------------------------

        public sealed override bool CanCacheInspectorGUI(
            SerializedProperty property)
        {
            ResolveFieldInfo(property);
            return m_ArrayDrawer.CanCacheInspectorGUI(property);
        }

        public sealed override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            ResolveFieldInfo(property);
            return m_ArrayDrawer.GetPropertyHeight(property, label);
        }

        public sealed override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            ResolveFieldInfo(property);
            m_ArrayDrawer.OnGUI(position, property, label);
        }

        //----------------------------------------------------------------------

        private void ResolveFieldInfo(SerializedProperty property)
        {
            if (m_ArrayDrawer.fieldInfo == null)
            {
                var fieldInfo = GetFieldInfo(property);
                s_FieldInfo.SetValue(this, fieldInfo);
                m_ArrayDrawer.fieldInfo = fieldInfo;
            }
        }

        //======================================================================

        private delegate FieldInfo GetFieldInfoFromPropertyDelegate(
            SerializedProperty property,
            out Type propertyType);

        private static readonly GetFieldInfoFromPropertyDelegate
        s_GetFieldInfoFromProperty =
            (GetFieldInfoFromPropertyDelegate)
            Delegate.CreateDelegate(
                typeof(GetFieldInfoFromPropertyDelegate),
                null,
                typeof(PropertyDrawer)
                .Assembly
                .GetType("UnityEditor.ScriptAttributeUtility")
                .GetMethod(
                    "GetFieldInfoFromProperty",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
            );

        internal static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            Type propertyType;
            var fieldInfo =
                s_GetFieldInfoFromProperty(property, out propertyType);

            if (fieldInfo == null)
                Debug.LogFormat("GetFieldInfo({0}) == null", property.propertyPath);

            return fieldInfo;
        }

    }

}