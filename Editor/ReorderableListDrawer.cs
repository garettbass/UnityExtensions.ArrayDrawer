using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityExtensions
{

    [CustomPropertyDrawer(typeof(ReorderableListAttribute))]
    public class ReorderableListDrawer : ArrayDrawer
    {

        public delegate void ElementDelegate(SerializedProperty array, int index);

        public static event ElementDelegate onElementSelected;

        public struct ElementSelectionScope : IDisposable
        {
            private readonly ElementDelegate m_callback;

            public ElementSelectionScope(ElementDelegate callback)
            {
                m_callback = callback;
                onElementSelected += m_callback;
            }

            public void Dispose()
            {
                onElementSelected -= m_callback;
            }
        }

        //----------------------------------------------------------------------

        public delegate void BackgroundColorDelegate(
            SerializedProperty array,
            int index,
            ref Color backgroundColor);

        public static event BackgroundColorDelegate onBackgroundColor;

        public struct BackgroundColorScope : IDisposable
        {
            private readonly BackgroundColorDelegate m_callback;

            public BackgroundColorScope(BackgroundColorDelegate callback)
            {
                m_callback = callback;
                onBackgroundColor += m_callback;
            }

            public void Dispose()
            {
                onBackgroundColor -= m_callback;
            }
        }

        //----------------------------------------------------------------------

        private static readonly ReorderableListAttribute
        s_DefaultAttribute = new ReorderableListAttribute();

        public new ReorderableListAttribute attribute
        {
            get
            {
                var attribute = (ReorderableListAttribute)base.attribute;
                return attribute ?? s_DefaultAttribute;
            }
        }

        public override bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return true;
        }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var reorderableListOfValues =
                GetReorderableList(
                    attribute,
                    fieldInfo,
                    property);

            Debug.Assert(
                reorderableListOfValues.serializedProperty.propertyPath ==
                property.propertyPath);

            try {
                return reorderableListOfValues.GetHeight(label);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                return 0f;
            }
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            var reorderableList =
                GetReorderableList(
                    attribute,
                    fieldInfo,
                    property);
            reorderableList.onBackgroundColor = onBackgroundColor;
            reorderableList.onSelectCallback += OnSelectCallback;
            reorderableList.DoGUI(position);
            reorderableList.onSelectCallback -= OnSelectCallback;
            reorderableList.onBackgroundColor = null;
        }

        //----------------------------------------------------------------------

        private void OnSelectCallback(ReorderableList list)
        {
            var array = list.serializedProperty;
            var index = list.index;
            if (onElementSelected != null)
                onElementSelected.Invoke(array, index);
        }

        //----------------------------------------------------------------------

        private class ReorderableListMap
        : Dictionary<string, ReorderableListOfValues>
        {
            public ReorderableListOfValues Find(string key)
            {
                var reorderableList = default(ReorderableListOfValues);
                base.TryGetValue(key, out reorderableList);
                return reorderableList;
            }
        }

        private readonly ReorderableListMap
        m_ReorderableListMap = new ReorderableListMap();

        private ReorderableListOfValues
        m_MostRecentReorderableList;

        private string
        m_MostRecentPropertyPath;

        private ReorderableListOfValues
        GetReorderableList(
            ReorderableListAttribute attribute,
            FieldInfo fieldInfo,
            SerializedProperty property)
        {
            var propertyPath = property.propertyPath;

            if (m_MostRecentReorderableList != null)
            {
                if (m_MostRecentPropertyPath == propertyPath)
                {
                    m_MostRecentReorderableList.serializedProperty = property;
                    return m_MostRecentReorderableList;
                }
            }

            m_MostRecentReorderableList =
                m_ReorderableListMap
                .Find(propertyPath);

            if (m_MostRecentReorderableList == null)
            {
                var reorderableList =
                    CreateReorderableList(
                        attribute,
                        fieldInfo,
                        property);

                m_ReorderableListMap.Add(propertyPath, reorderableList);

                m_MostRecentReorderableList = reorderableList;
            }
            else
            {
                m_MostRecentReorderableList.serializedProperty = property;
            }

            m_MostRecentPropertyPath = propertyPath;

            return m_MostRecentReorderableList;
        }

        private ReorderableListOfValues
        CreateReorderableList(
            ReorderableListAttribute attribute,
            FieldInfo fieldInfo,
            SerializedProperty property)
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
                        attribute,
                        property,
                        listType,
                        elementType
                    );
            }

            var elementIsUnityEngineObject =
                typeof(UnityEngine.Object)
                .IsAssignableFrom(elementType);

            if (elementIsUnityEngineObject)
            {
                var elementsAreSubassets =
                    elementIsUnityEngineObject &&
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
                            attribute,
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
                            attribute,
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
                            attribute,
                            property,
                            listType,
                            elementType
                        );
                }
            }

            return
                new ReorderableListOfValues(
                    attribute,
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