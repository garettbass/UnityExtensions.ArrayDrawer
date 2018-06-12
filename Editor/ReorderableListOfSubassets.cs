#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal class ReorderableListOfSubassets : ReorderableListOfStructures
    {

        private readonly SerializedObjectCache m_SerializedObjectCache =
            new SerializedObjectCache();

        private readonly Type[] m_SubassetTypes;

        private readonly bool m_UseFullSubassetTypeNames;

        //----------------------------------------------------------------------

        public ReorderableListOfSubassets(
            SerializedProperty property,
            Type listType,
            Type elementType,
            Type[] subassetTypes)
        : base(property, listType, elementType)
        {
            m_SubassetTypes = subassetTypes;

            m_UseFullSubassetTypeNames = SubassetTypeNamesAreAmbiguous();

            onRemoveCallback = OnRemoveCallback;

            onCanAddCallback = OnCanAddCallback;

            if (m_SubassetTypes.Length == 1)
                onAddCallback = OnAddCallback;

            else if (m_SubassetTypes.Length > 1)
                onAddDropdownCallback = OnAddDropdownCallback;
        }

        //----------------------------------------------------------------------

        public override void DoGUI(Rect position)
        {
            base.DoGUI(position);
            EvictObsoleteSerializedObjectsFromCache();
        }

        //----------------------------------------------------------------------

        protected override float GetElementHeight(
            SerializedProperty element,
            int elementIndex)
        {
            var subasset = element.objectReferenceValue;
            if (subasset == null)
                return EditorGUIUtility.singleLineHeight;

            var height = m_SubassetTypes.Length > 1 ? headerHeight : 0f;

            var serializedObject = GetSerializedObjectFromCache(subasset);

            if (showElementHeader || m_SubassetTypes.Length > 1)
            {
                height += headerHeight;
            }

            height += GetSubassetHeight(serializedObject);

            return Mathf.Max(height, elementHeight);
        }

        private float GetSubassetHeight(
            SerializedObject serializedObject)
        {
            var height = 0f;

            var count = m_SubassetTypes.Length > 1 ? 1 : 0;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            foreach (var property in EnumerateChildProperties(serializedObject))
            {
                if (count++ > 0)
                    height += spacing;

                height += GetPropertyHeight(property);
            }

            return height;
        }

        //----------------------------------------------------------------------

        protected override void DrawElement(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            var subasset = element.objectReferenceValue;
            if (subasset == null)
                return;

            var serializedObject = GetSerializedObjectFromCache(subasset);

            if (showElementHeader || m_SubassetTypes.Length > 1)
            {
                DrawElementHeader(
                    position,
                    subasset,
                    serializedObject,
                    isActive
                );
                position.y += headerHeight;
            }

            position.xMin += 12;

            DrawSubasset(position, serializedObject);
        }

        private void DrawSubasset(
            Rect position,
            SerializedObject serializedObject)
        {
            serializedObject.Update();

            var count = m_SubassetTypes.Length > 1 ? 1 : 0;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            foreach (var property in EnumerateChildProperties(serializedObject))
            {
                if (count++ > 0)
                    position.y += spacing;

                position.height = GetPropertyHeight(property);
                PropertyField(position, property);
                position.y += position.height;
            }

            serializedObject.ApplyModifiedProperties();
        }

        //----------------------------------------------------------------------

        protected override void PopulateElementContextMenu(
            GenericMenu menu,
            int elementIndex)
        {
            foreach (var mutableElementType in m_SubassetTypes)
            {
                var elementType = mutableElementType;

                var elementTypeName =
                    m_UseFullSubassetTypeNames
                    ? elementType.FullName
                    : elementType.Name;

                var insertAbove = "Insert Above/" + elementTypeName;
                var insertBelow = "Insert Below/" + elementTypeName;

                menu.AddItem(new GUIContent(insertAbove), false, () =>
                {
                    InsertSubasset(elementType, elementIndex);
                    index = elementIndex;
                });
                menu.AddItem(new GUIContent(insertBelow), false, () =>
                {
                    InsertSubasset(elementType, elementIndex + 1);
                    index = elementIndex + 1;
                });
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                RemoveSubasset();
                index = -1;
            });
        }

        //----------------------------------------------------------------------

        private void DrawElementHeader(
            Rect position,
            Object subasset,
            SerializedObject serializedObject,
            bool isActive)
        {
            position.height = headerHeight;

            var titleContent = m_TitleContent;
            titleContent.text = ObjectNames.NicifyVariableName(subasset.name);

            var titleStyle =
                isActive
                ? EditorStyles.whiteBoldLabel
                : EditorStyles.boldLabel;

            var titleWidth =
                titleStyle
                .CalcSize(titleContent)
                .x;

            var scriptRect = position;
            scriptRect.yMin -= 1;
            scriptRect.yMax -= 1;
            scriptRect.width = titleWidth + 16;
            var scriptProperty = serializedObject.FindProperty("m_Script");

            using (ColorAlphaScope(0))
            {
                EditorGUI.BeginDisabledGroup(disabled: true);
                EditorGUI.PropertyField(
                    scriptRect,
                    scriptProperty,
                    GUIContent.none
                );
                EditorGUI.EndDisabledGroup();
            }

            if (IsRepaint())
            {
                var fillRect = position;
                fillRect.xMin -= draggable ? 18 : 4;
                fillRect.xMax += 4;
                fillRect.y -= 2;

                var fillStyle = HeaderBackgroundStyle;

                using (ColorAlphaScope(0.5f))
                {
                    fillStyle.Draw(fillRect, false, false, false, false);
                }

                var embossStyle = EditorStyles.whiteBoldLabel;
                var embossRect = position;
                embossRect.yMin -= 0;
                EditorGUI.BeginDisabledGroup(true);
                embossStyle.Draw(embossRect, titleContent, false, false, false, false);
                EditorGUI.EndDisabledGroup();

                var titleRect = position;
                titleRect.yMin -= 1;
                titleRect.width = titleWidth;
                titleStyle.Draw(titleRect, titleContent, false, false, false, false);
            }
        }

        //----------------------------------------------------------------------

        private bool OnCanAddCallback(ReorderableList list)
        {
            return m_SubassetTypes.Length > 0;
        }

        private void OnAddCallback(ReorderableList list)
        {
            AddSubasset(m_SubassetTypes[0]);
        }

        private void OnAddDropdownCallback(Rect position, ReorderableList list)
        {
            var menu = new GenericMenu();

            foreach (var mutableElementType in m_SubassetTypes)
            {
                var elementType = mutableElementType;

                var content = new GUIContent();

                content.text =
                    m_UseFullSubassetTypeNames
                    ? elementType.FullName
                    : elementType.Name;

                menu.AddItem(
                    content,
                    on: false,
                    func: () => AddSubasset(elementType)
                );
            }
            position.x -= 2;
            position.y += 1;
            menu.DropDown(position);
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            RemoveSubasset();
        }

        //----------------------------------------------------------------------

        private void AddSubasset(Type subassetType)
        {
            var array = serializedProperty;
            var elementIndex = array.arraySize;

            InsertSubasset(subassetType, elementIndex);
        }

        private void InsertSubasset(Type subassetType, int elementIndex)
        {
            var array = serializedProperty;
            var serializedObject = array.serializedObject;

            array.InsertArrayElementAtIndex(elementIndex);
            index = elementIndex;

            var subasset = ScriptableObject.CreateInstance(subassetType);
            subasset.name =
                m_UseFullSubassetTypeNames
                ? subassetType.FullName
                : subassetType.Name;

            var asset = serializedObject.targetObject;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            AssetDatabase.AddObjectToAsset(subasset, assetPath);

            var element = array.GetArrayElementAtIndex(elementIndex);
            element.objectReferenceInstanceIDValue = subasset.GetInstanceID();

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void RemoveSubasset()
        {
            var array = serializedProperty;
            var serializedObject = array.serializedObject;

            var element = array.GetArrayElementAtIndex(index);
            var subasset = element.objectReferenceValue;
            if (subasset != null)
            {
                element.objectReferenceValue = null;
                Object.DestroyImmediate(subasset, allowDestroyingAssets: true);
            }

            array.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            var length = array.arraySize;
            if (index > length - 1)
                index = length - 1;
        }

        //----------------------------------------------------------------------

        private bool SubassetTypeNamesAreAmbiguous()
        {
            var elementTypeNames = m_SubassetTypes.Select(t => t.Name);
            var elementTypeNamesAreAmbiguous =
                elementTypeNames.Count() >
                elementTypeNames.Distinct().Count();
            return elementTypeNamesAreAmbiguous;
        }

        //----------------------------------------------------------------------

        private static IEnumerable<SerializedProperty> EnumerateChildProperties(
            SerializedObject serializedObject)
        {
            var property = serializedObject.GetIterator();
            if (property.NextVisible(enterChildren: true))
            {
                // yield return property; // skip "m_Script"
                while (property.NextVisible(enterChildren: false))
                {
                    yield return property;
                }
            }
        }

        //----------------------------------------------------------------------

        class SerializedObjectCache : Dictionary<Object, SerializedObject> { }

        private SerializedObject GetSerializedObjectFromCache(Object @object)
        {
            var cache = m_SerializedObjectCache;
            var serializedObject = default(SerializedObject);
            if (!cache.TryGetValue(@object, out serializedObject))
            {
                serializedObject = new SerializedObject(@object);
                cache.Add(@object, serializedObject);
            }
            return serializedObject;
        }

        private void EvictObsoleteSerializedObjectsFromCache()
        {
            var cache = m_SerializedObjectCache;
            var destroyedObjects = cache.Keys.Where(key => key == null);
            if (destroyedObjects.Any())
            {
                foreach (var @object in destroyedObjects.ToArray())
                {
                    cache.Remove(@object);
                }
            }
        }

    }

}

#endif // UNITY_EDITOR