using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    [CustomPropertyDrawer(typeof(ReorderableListAttribute))]
    public class ReorderableListDrawer : ArrayDrawer
    {

        public new ReorderableListAttribute attribute
        {
            get { return (ReorderableListAttribute)base.attribute; }
        }

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return true;
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var reorderableListOfValues = GetReorderableList(property);

            Debug.Assert(
                reorderableListOfValues.serializedProperty.propertyPath ==
                property.propertyPath);

            return reorderableListOfValues.GetHeight(label);
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            var reorderableListOfValues = GetReorderableList(property);

            reorderableListOfValues.DoGUI(position);
        }

        //----------------------------------------------------------------------

        private class ReorderableListMap
        : Dictionary<string, ReorderableListOfValues>
        {

            public void Add(
                SerializedProperty property,
                ReorderableListOfValues reorderableListOfValues)
            {
                var propertyPath = property.propertyPath;
                base.Add(propertyPath, reorderableListOfValues);
            }

            public ReorderableListOfValues Find(SerializedProperty property)
            {
                var propertyPath = property.propertyPath;
                var reorderableListOfValues = default(ReorderableListOfValues);
                base.TryGetValue(
                    propertyPath,
                    out reorderableListOfValues
                );
                return reorderableListOfValues;
            }

        }

        private readonly ReorderableListMap
        m_ReorderableListMap = new ReorderableListMap();

        private ReorderableListOfValues
        m_MostRecentReorderableList;

        private ReorderableListOfValues
        GetReorderableList(SerializedProperty property)
        {
            if (m_MostRecentReorderableList != null)
            {
                var mostRecentPropertyPath =
                    m_MostRecentReorderableList
                    .serializedProperty
                    .propertyPath;
                if (mostRecentPropertyPath == property.propertyPath)
                    return m_MostRecentReorderableList;
            }

            m_MostRecentReorderableList =
                m_ReorderableListMap
                .Find(property);

            if (m_MostRecentReorderableList == null)
            {
                m_MostRecentReorderableList = CreateReorderableList(property);
                m_ReorderableListMap
                .Add(property, m_MostRecentReorderableList);
            }

            return m_MostRecentReorderableList;
        }

        private ReorderableListOfValues
        CreateReorderableList(SerializedProperty property)
        {
            var listType = fieldInfo.FieldType;

            var elementType = GetArrayOrListElementType(listType);

            var elementIsValue =
                elementType.IsEnum ||
                elementType.IsPrimitive ||
                elementType == typeof(string) ||
                elementType == typeof(Color) ||
                elementType == typeof(LayerMask) ||
                elementType == typeof(Vector2) ||
                elementType == typeof(Vector3) ||
                elementType == typeof(Vector4) ||
                elementType == typeof(Rect) ||
                elementType == typeof(AnimationCurve) ||
                elementType == typeof(Bounds) ||
                elementType == typeof(Gradient) ||
                elementType == typeof(Quaternion) ||
                elementType == typeof(Vector2Int) ||
                elementType == typeof(Vector3Int) ||
                elementType == typeof(RectInt) ||
                elementType == typeof(BoundsInt);

            if (elementIsValue)
            {
                return
                    new ReorderableListOfValues(
                        property,
                        listType,
                        elementType
                    );
            }

            var elementIsScriptableObject =
                typeof(ScriptableObject)
                .IsAssignableFrom(elementType);

            if (elementIsScriptableObject)
            {
                var elementsAreSubassets =
                    elementIsScriptableObject &&
                    attribute != null &&
                    attribute.elementsAreSubassets;

                if (elementsAreSubassets)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    var types = assemblies.SelectMany(a => a.GetTypes());

                    var subassetTypes =
                        types.Where(t =>
                            t.IsAbstract == false &&
                            t.IsGenericTypeDefinition == false &&
                            elementType.IsAssignableFrom(t)
                        )
                        .ToArray();

                    return
                        new ReorderableListOfSubassets(
                            property,
                            listType,
                            elementType,
                            subassetTypes
                        );
                }
                else
                {
                    return
                        new ReorderableListOfValues(
                            property,
                            listType,
                            elementType
                        );
                }
            }

            var elementPropertyDrawerType = GetDrawerTypeForType(elementType);
            if (elementPropertyDrawerType == null)
            {
                var elementIsStruct =
                    elementType.IsValueType &&
                    elementType.IsEnum == false &&
                    elementType.IsPrimitive == false;

                var elementIsClass =
                    elementType.IsClass;

                if (elementIsStruct || elementIsClass)
                {
                    return
                        new ReorderableListOfStructures(
                            property,
                            listType,
                            elementType
                        );
                }
            }

            return
                new ReorderableListOfValues(
                    property,
                    listType,
                    elementType
                );

        }

        //======================================================================

        private delegate Type GetArrayOrListElementTypeDelegate(Type listType);

        private static readonly GetArrayOrListElementTypeDelegate
        GetArrayOrListElementType =
            (GetArrayOrListElementTypeDelegate)
            Delegate.CreateDelegate(
                typeof(GetArrayOrListElementTypeDelegate),
                null,
                typeof(PropertyDrawer)
                .Assembly
                .GetType("UnityEditor.EditorExtensionMethods")
                .GetMethod(
                    "GetArrayOrListElementType",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
            );

        //======================================================================

        private delegate Type GetDrawerTypeForTypeDelegate(Type type);

        private static readonly GetDrawerTypeForTypeDelegate
        GetDrawerTypeForType =
            (GetDrawerTypeForTypeDelegate)
            Delegate.CreateDelegate(
                typeof(GetDrawerTypeForTypeDelegate),
                null,
                typeof(PropertyDrawer)
                .Assembly
                .GetType("UnityEditor.ScriptAttributeUtility")
                .GetMethod(
                    "GetDrawerTypeForType",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
            );

    }

}