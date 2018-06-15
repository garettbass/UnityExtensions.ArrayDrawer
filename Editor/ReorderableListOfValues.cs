#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal class ReorderableListOfValues : ReorderableList
    {

        private const float kIndentPerLevel = 15;

        public readonly Type listType;

        public readonly Type elementType;

        public string elementHeaderFormat;

        public bool showElementHeader
        {
            get { return !string.IsNullOrEmpty(elementHeaderFormat); }
        }

        protected static readonly new Defaults
        defaultBehaviours = new Defaults();

        protected readonly GUIContent m_TitleContent = new GUIContent();

        //----------------------------------------------------------------------

        public ReorderableListOfValues(
            SerializedProperty property,
            Type listType,
            Type elementType)
        : base(
            serializedObject: property.serializedObject,
            elements: property.Copy(),
            draggable: true,
            displayHeader: true,
            displayAddButton: true,
            displayRemoveButton: true)
        {
            this.listType = listType;
            this.elementType = elementType;

            headerHeight -= 2;
            drawHeaderCallback = DrawHeaderCallback;
            drawFooterCallback = DrawFooterCallback;
            elementHeightCallback = ElementHeightCallback;
            drawElementCallback = DrawElementCallback;
            drawElementBackgroundCallback = DrawElementBackgroundCallback;

            onAddCallback = OnAddCallback;
            onCanRemoveCallback = OnCanRemoveCallback;

#if UNITY_2018_1_OR_NEWER
            drawNoneElementCallback = DrawEmptyElementCallback;
#endif // UNITY_2018_1_OR_NEWER
        }

        //----------------------------------------------------------------------

        public float GetHeight(GUIContent label)
        {
            UpdateLabel(label);
            UpdateElementHeights();
            var height = GetHeight();

            if (!displayAdd && !displayRemove)
            {
                height -= 14;
            }

            return height;
        }

        public virtual void DoGUI(Rect position)
        {
            if (!displayAdd && !displayRemove)
            {
                index = -1;
            }

            position.xMin += EditorGUI.indentLevel * kIndentPerLevel;

            using (IndentLevelScope(-EditorGUI.indentLevel))
            {
                var array = serializedProperty;
                if (array.isExpanded)
                {
                    DoList(position);
                }
                else
                {
                    index = -1;
                    DoCollapsedListBackground(position);
                }
                DrawHeader(position);
            }
        }

        //----------------------------------------------------------------------

        private void DoCollapsedListBackground(Rect position)
        {
            var headerRect = position;
            headerRect.height = headerHeight;

            var listRect = position;
            listRect.y += headerHeight;
            listRect.height = 7;

            var footerRect = position;
            footerRect.y += headerHeight + listRect.height;
            footerRect.height = footerHeight;

            if (showDefaultBackground && IsRepaint())
            {
                defaultBehaviours.DrawHeaderBackground(headerRect);
                defaultBehaviours
                .boxBackground
                .Draw(listRect, false, false, false, false);
            }
            DrawFooterCallback(footerRect);
        }

        //----------------------------------------------------------------------

        private void OnAddCallback(ReorderableList list)
        {
            serializedProperty.isExpanded = true;
            defaultBehaviours.DoAddButton(list);
        }

        private bool OnCanRemoveCallback(ReorderableList list)
        {
            return serializedProperty.isExpanded;
        }

        //----------------------------------------------------------------------

        protected virtual float GetElementHeight(
            SerializedProperty element,
            int elementIndex)
        {
            return GetPropertyHeight(element, GUIContent.none);
        }

        protected virtual void DrawElement(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            PropertyField(position, element, GUIContent.none);
        }

        //----------------------------------------------------------------------

        protected static readonly GUIStyle
        ElementBackgroundStyle = "CN EntryBackEven";

        protected virtual void DrawElementBackground(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            if (isActive)
            {
                var isProSkin = EditorGUIUtility.isProSkin;
                position.xMax += isProSkin ? 1 : 0;
                position.yMin -= isProSkin ? 0 : 1;
                position.yMax += isProSkin ? 2 : 1;
            }
            defaultBehaviours.DrawElementBackground(
                position,
                elementIndex,
                isActive,
                isFocused,
                draggable: true
            );

            if (IsRepaint() && element != null)
            {
                var fillStyle = ElementBackgroundStyle;
                var fillRect = position;
                fillRect.xMin += 2;
                fillRect.xMax -= 2;
                fillRect.yMin += 1;
                fillRect.yMax -= 1;
                using (ColorAlphaScope(isActive ? 0.5f : 1))
                {
                    fillStyle.Draw(fillRect, false, false, false, false);
                }
            }
        }

        //----------------------------------------------------------------------

        protected virtual void PopulateElementContextMenu(
            GenericMenu menu,
            int elementIndex)
        {
            var property = serializedProperty;
            var serializedObject = property.serializedObject;
            if (displayAdd)
            {
                menu.AddItem(new GUIContent("Insert Above"), false, () =>
                {
                    property.InsertArrayElementAtIndex(elementIndex);
                    serializedObject.ApplyModifiedProperties();
                    index = elementIndex;
                });
                menu.AddItem(new GUIContent("Insert Below"), false, () =>
                {
                    property.InsertArrayElementAtIndex(elementIndex + 1);
                    serializedObject.ApplyModifiedProperties();
                    index = elementIndex + 1;
                });

            }
            if (displayAdd && displayRemove)
            {
                menu.AddSeparator("");
            }
            if (displayRemove)
            {
                menu.AddItem(new GUIContent("Remove"), false, () =>
                {
                    property.DeleteArrayElementAtIndex(elementIndex);
                    serializedObject.ApplyModifiedProperties();
                    index = -1;
                });
            }
        }

        //----------------------------------------------------------------------

        protected float GetPropertyHeight(SerializedProperty property)
        {
            return
                EditorGUI.GetPropertyHeight(
                    property,
                    includeChildren: true
                );
        }

        protected float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            return
                EditorGUI.GetPropertyHeight(
                    property,
                    label,
                    includeChildren: true
                );
        }

        //----------------------------------------------------------------------

        protected void PropertyField(
            Rect position,
            SerializedProperty property)
        {
            EditorGUI.PropertyField(
                position,
                property,
                includeChildren: true
            );
        }

        protected void PropertyField(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.PropertyField(
                position,
                property,
                label,
                includeChildren: true
            );
        }

        //----------------------------------------------------------------------

        private static readonly GUIStyle
        EyeDropperHorizontalLine = "EyeDropperHorizontalLine";

        protected static void DrawHorizontalLine(Rect position)
        {
            if (IsRepaint())
            {
                var style = EyeDropperHorizontalLine;
                position.height = 1;
                var color = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.75f);
                style.Draw(position, false, false, false, false);
                GUI.color = color;
            }
        }

        protected static bool IsRepaint()
        {
            var @event = Event.current;
            return @event != null && @event.type == EventType.Repaint;
        }

        //----------------------------------------------------------------------

        private void DrawHeader(Rect position)
        {
            defaultBehaviours.DrawHeaderBackground(position);
            position.xMin += 16;
            position.y += 1;
            position.height = EditorGUIUtility.singleLineHeight;

            var property = serializedProperty;
            var wasExpanded = property.isExpanded;
            var isExpanded = EditorGUI.Foldout(position, wasExpanded, m_Label);
            if (isExpanded != wasExpanded)
            {
                property.isExpanded = isExpanded;
            }
        }

        //----------------------------------------------------------------------

        private GUIContent m_Label = new GUIContent();

        private void UpdateLabel(GUIContent label)
        {
            m_Label.image = label.image;
            m_Label.tooltip = label.tooltip;

            var text = label.text;
            var arraySize = serializedProperty.arraySize;

            if (string.IsNullOrEmpty(text))
            {
                text = string.Format("({0})", arraySize);
            }
            else
            {
                text = string.Format("{0} ({1})", text, arraySize);
            }

            m_Label.text = text;
        }

        //----------------------------------------------------------------------

        private readonly List<float> m_ElementHeights = new List<float>();

        private void UpdateElementHeights()
        {
            var array = serializedProperty;
            var length = array.arraySize;
            m_ElementHeights.Clear();
            m_ElementHeights.Capacity = length;
            var isExpanded = array.isExpanded;
            for (int elementIndex = 0; elementIndex < length; ++elementIndex)
            {
                var height = 0f;
                if (isExpanded)
                {
                    var element = array.GetArrayElementAtIndex(elementIndex);
                    height = GetElementHeight(element, elementIndex);
                    height = AddElementPadding(height);
                }
                m_ElementHeights.Add(height);
            }
        }

        //----------------------------------------------------------------------

        private void DrawHeaderCallback(Rect position)
        {
            // DoGUI draws the header content after the list is drawn
        }

        private void DrawFooterCallback(Rect position)
        {
            if (displayAdd || displayRemove)
                defaultBehaviours.DrawFooter(position, this);

            position.xMin += 2;
            position.xMax -= 2;
            position.y -= 6;
            DrawHorizontalLine(position);
        }

        private float ElementHeightCallback(int elementIndex)
        {
            return m_ElementHeights[elementIndex];
        }

        private void DrawElementCallback(
            Rect position,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            RemoveElementPadding(ref position);

            var array = serializedProperty;
            if (array.isExpanded == false)
                return;

            var element = array.GetArrayElementAtIndex(elementIndex);
            DrawElement(position, element, elementIndex, isActive, isFocused);
        }

        private void DrawElementBackgroundCallback(
            Rect position,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            var array = serializedProperty;
            if (array.isExpanded == false)
                return;

            var length = array.arraySize;
            var element = default(SerializedProperty);

            var activeIndex = base.index;
            if (activeIndex == elementIndex && isActive == false)
            {
                // HACK: ReorderableList invokes this callback with the
                // wrong elementIndex.
                var nonDragTargetIndices = m_NonDragTargetIndices;
                if (nonDragTargetIndices != null)
                {
                    elementIndex = nonDragTargetIndices[elementIndex];
                }
            }

            if (elementIndex >= 0 && elementIndex < length)
            {
                // HACK: ReorderableList invokes this callback with the
                // wrong height.
                position.height = ElementHeightCallback(elementIndex);
                element = array.GetArrayElementAtIndex(elementIndex);
            }

            DrawElementBackground(
                position,
                element,
                elementIndex,
                isActive,
                isFocused
            );

            if (element != null)
            {
                HandleElementEvents(position, elementIndex);
            }

            {
                var upperEdge = position;
                upperEdge.xMin += 2;
                upperEdge.xMax -= 2;
                upperEdge.y -= 1;
                DrawHorizontalLine(upperEdge);
            }

            {
                var lowerEdge = position;
                lowerEdge.xMin += 2;
                lowerEdge.xMax -= 2;
                lowerEdge.y += lowerEdge.height;
                lowerEdge.y -= 1;
                DrawHorizontalLine(lowerEdge);
            }
        }

        private void DrawEmptyElementCallback(Rect position)
        {
            position.y += 2;
            EditorGUI.BeginDisabledGroup(disabled: true);
            EditorGUI.LabelField(position, "List is Empty");
            EditorGUI.EndDisabledGroup();
        }

        //----------------------------------------------------------------------

        private void HandleElementEvents(Rect position, int elementIndex)
        {
            var @event = Event.current;
            if (@event == null)
                return;

            var handleRect = position;
            handleRect.width = 19;

            var isRightMouseInHandleRect =
                @event.button == 1 &&
                handleRect.Contains(@event.mousePosition);

            var isActiveElementIndex = index == elementIndex;

            switch (@event.type)
            {
                case EventType.MouseDown:
                    if (isRightMouseInHandleRect)
                    {
                        EndEditingActiveTextField();
                        index = elementIndex;
                        return;
                    }
                    break;

                case EventType.MouseUp:
                    if (isRightMouseInHandleRect && isActiveElementIndex)
                    {
                        DoElementContextMenu(handleRect, elementIndex);
                        return;
                    }
                    break;
            }
        }

        //----------------------------------------------------------------------

        private void DoElementContextMenu(Rect position, int elementIndex)
        {
            position.x += 1;
            position.height = elementHeight - 1;

            var menu = new GenericMenu();

            PopulateElementContextMenu(menu, elementIndex);

            if (menu.GetItemCount() > 0)
                menu.DropDown(position);
        }

        //----------------------------------------------------------------------

        private static readonly FieldInfo
        m_NonDragTargetIndicesField =
            typeof(ReorderableList)
            .GetField(
                "m_NonDragTargetIndices",
                BindingFlags.Instance |
                BindingFlags.NonPublic
            );

        private List<int> m_NonDragTargetIndices
        {
            get
            {
                return
                    (List<int>)
                    m_NonDragTargetIndicesField
                    .GetValue(this);
            }
        }

        //----------------------------------------------------------------------

        private const float borderHeight = 0;

        private static float AddElementPadding(float elementHeight)
        {
            var verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
            return
                borderHeight
                + verticalSpacing
                + elementHeight
                + verticalSpacing
                + 1;
        }

        private static void RemoveElementPadding(ref Rect position)
        {
            var verticalSpacing = EditorGUIUtility.standardVerticalSpacing;
            position.y += borderHeight;
            position.y += verticalSpacing;
            position.height -= verticalSpacing;
            position.height -= 1;
        }

        //======================================================================

        private delegate void EndEditingActiveTextFieldDelegate();

        private static readonly EndEditingActiveTextFieldDelegate
        EndEditingActiveTextField =
            (EndEditingActiveTextFieldDelegate)
            Delegate.CreateDelegate(
                typeof(EndEditingActiveTextFieldDelegate),
                null,
                typeof(EditorGUI)
                .GetMethod(
                    "EndEditingActiveTextField",
                    BindingFlags.NonPublic |
                    BindingFlags.Public |
                    BindingFlags.Static
                )
            );

        //======================================================================

        protected struct Deferred : IDisposable
        {
            private readonly Action _onDispose;

            public Deferred(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_onDispose != null)
                    _onDispose();
            }
        }

        protected Deferred ColorScope(Color newColor)
        {
            var oldColor = GUI.color;
            GUI.color = newColor;
            return new Deferred(() => GUI.color = oldColor);
        }

        protected Deferred ColorAlphaScope(float a)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            return new Deferred(() => GUI.color = oldColor);
        }

        protected IDisposable IndentLevelScope(int indent = 1)
        {
            EditorGUI.indentLevel += indent;
            return new Deferred(() => EditorGUI.indentLevel -= indent);
        }

    }

}

#endif // UNITY_EDITOR