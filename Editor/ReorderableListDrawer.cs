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

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return false;
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            if (m_ReorderableList == null)
                CreateReorderableList(property);

            return m_ReorderableList.GetHeight(label);
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            m_ReorderableList.DoGUI(position);
        }

        //----------------------------------------------------------------------

        private ReorderableListOfValues m_ReorderableList;

        private void CreateReorderableList(SerializedProperty property)
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
                m_ReorderableList =
                    new ReorderableListOfValues(
                        property,
                        listType,
                        elementType
                    );
                return;
            }

            var elementIsScriptableObject =
                typeof(ScriptableObject)
                .IsAssignableFrom(elementType);

            if (elementIsScriptableObject)
            {
                var reorderableListAttribute = (ReorderableListAttribute)attribute;

                var elementsAreSubassets =
                    elementIsScriptableObject &&
                    reorderableListAttribute.elementsAreSubassets;

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

                    m_ReorderableList =
                        new ReorderableListOfSubassets(
                            property,
                            listType,
                            elementType,
                            subassetTypes
                        );
                    return;
                }
                else
                {
                    m_ReorderableList =
                        new ReorderableListOfValues(
                            property,
                            listType,
                            elementType
                        );
                    return;
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
                    m_ReorderableList =
                        new ReorderableListOfStructures(
                            property,
                            listType,
                            elementType
                        );
                    return;
                }
            }

            m_ReorderableList =
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