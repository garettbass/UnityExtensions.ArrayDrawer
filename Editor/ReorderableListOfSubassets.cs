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

        public bool hasSingleSubassetType
        {
            get { return m_SubassetTypes.Length == 1; }
        }

        public bool hasMultipleSubassetTypes
        {
            get { return m_SubassetTypes.Length > 1; }
        }

        private readonly bool m_UseFullSubassetTypeNames;

        public override bool showElementHeader
        {
            get
            {
                return base.showElementHeader ||
                       hasMultipleSubassetTypes;
            }
        }

        //----------------------------------------------------------------------

        public ReorderableListOfSubassets(
            ReorderableListAttribute attribute,
            SerializedProperty property,
            Type listType,
            Type elementType,
            Type[] subassetTypes)
        : base(attribute, property, listType, elementType)
        {
            m_SubassetTypes = subassetTypes;

            m_UseFullSubassetTypeNames = SubassetTypeNamesAreAmbiguous();

            onCanAddCallback = OnCanAddCallback;

            if (hasSingleSubassetType)
                onAddCallback = OnAddCallback;

            else if (hasMultipleSubassetTypes)
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

            var serializedObject = GetSerializedObjectFromCache(subasset);
            var properties = serializedObject.EnumerateChildProperties();
            return base.GetElementHeight(properties);
        }

        //----------------------------------------------------------------------

        protected override void DrawElement(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive)
        {
            var subasset = element.objectReferenceValue;
            if (subasset == null)
                return;

            var serializedObject = GetSerializedObjectFromCache(subasset);
            serializedObject.Update();
            var properties = serializedObject.EnumerateChildProperties();
            base.DrawElement(position, properties, elementIndex, isActive);
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
                    OnNextGUIFrame(() => InsertSubasset(elementType, elementIndex)));
                menu.AddItem(new GUIContent(insertBelow), false, () =>
                    OnNextGUIFrame(() => InsertSubasset(elementType, elementIndex + 1)));
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () =>
                OnNextGUIFrame(() => DeleteElement(elementIndex)));
        }

        //----------------------------------------------------------------------

        private void DrawElementHeader(
            Rect position,
            Object subasset,
            bool isActive)
        {
            var subassetType = subasset != null ? subasset.GetType() : typeof(Object);
            position.height = headerHeight;

            var titleContent = m_TitleContent;
            titleContent.text =
                ObjectNames
                .NicifyVariableName(subasset.name);
            titleContent.image =
                EditorGUIUtility
                .ObjectContent(subasset, subassetType)
                .image;

            var titleStyle = EditorStyles.boldLabel;

            var titleWidth = titleStyle.CalcSize(titleContent).x;

            var scriptRect = position;
            scriptRect.yMin -= 1;
            scriptRect.yMax -= 1;
            scriptRect.width = titleWidth + 16;

            using (ColorAlphaScope(0))
            {
                EditorGUI.BeginDisabledGroup(disabled: true);
                EditorGUI.ObjectField(
                    scriptRect,
                    subasset,
                    subassetType,
                    allowSceneObjects: false);
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

                var titleRect = position;
                titleRect.xMin -= 4;
                titleRect.yMin -= 2;
                titleRect.yMax += 1;
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
            serializedProperty.isExpanded = true;
            AddSubasset(m_SubassetTypes[0]);
        }

        private void OnAddDropdownCallback(Rect position, ReorderableList list)
        {
            serializedProperty.isExpanded = true;
            var menu = new GenericMenu();

            foreach (var mutableElementType in m_SubassetTypes)
            {
                var elementType = mutableElementType;
                var elementTypeName = 
                    m_UseFullSubassetTypeNames
                    ? elementType.FullName
                    : elementType.Name;

                var content = new GUIContent();
                content.text = ObjectNames.NicifyVariableName(elementTypeName);

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

            array.InsertArrayElementAtIndex(elementIndex);
            index = elementIndex;
            GUI.changed = true;

            var subasset = default(Object);

            if (typeof(ScriptableObject).IsAssignableFrom(subassetType))
            {
                subasset = ScriptableObject.CreateInstance(subassetType);
            }
            else if (typeof(Object).IsAssignableFrom(subassetType))
            {
                subasset = (Object)Activator.CreateInstance(subassetType);
            }

            if (subasset == null)
            {
                Debug.LogErrorFormat(
                    "Failed to create subasset of type {0}",
                    subassetType.FullName
                );
                return;
            }

            subasset.name = subassetType.Name;

            var serializedObject = array.serializedObject;
            serializedObject.targetObject.AddSubasset(subasset);

            var element = array.GetArrayElementAtIndex(elementIndex);
            var oldSubassets = element.FindReferencedSubassets();
            element.objectReferenceValue = subasset;
            if (oldSubassets.Any())
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                serializedObject.DestroyUnreferencedSubassets(oldSubassets);
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override void DeleteElement(int elementIndex)
        {
            if (elementIndex < 0)
                return;

            var array = serializedProperty;
            if (elementIndex < array.arraySize)
            {
                var serializedObject = array.serializedObject;
                var element = array.GetArrayElementAtIndex(elementIndex);
                var oldSubassets = element.FindReferencedSubassets();
                element.objectReferenceValue = null;
                array.DeleteArrayElementAtIndex(elementIndex);
                if (oldSubassets.Any())
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    serializedObject.DestroyUnreferencedSubassets(oldSubassets);
                }
                else
                {
                    serializedObject.ApplyModifiedProperties();
                }

                var length = array.arraySize;
                if (index > length - 1)
                    index = length - 1;
            }
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