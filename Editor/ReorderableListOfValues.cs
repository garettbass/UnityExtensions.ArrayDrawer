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
        public readonly Type listType;

        public readonly Type elementType;

        public string elementHeaderFormat;

        public bool showElementHeader
        {
            get { return !string.IsNullOrEmpty(elementHeaderFormat); }
        }

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
            elementHeightCallback = ElementHeightCallback;
            drawElementCallback = DrawElementCallback;
            drawElementBackgroundCallback = DrawElementBackgroundCallback;
#if UNITY_2018_1_OR_NEWER
            drawNoneElementCallback = DrawEmptyElementCallback;
#endif // UNITY_2018_1_OR_NEWER
        }

        //----------------------------------------------------------------------

        public float GetHeight(GUIContent label)
        {
            UpdateLabel(label);
            UpdateElementHeights();
            return GetHeight();
        }

        public virtual void DoGUI(Rect position)
        {
            base.DoList(position);
            DrawHeader(position);
            DrawFooterEdge(position);
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

        protected virtual void DrawElementBackground(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            if (isActive)
            {
                position.yMax += 1;
            }
            defaultBehaviours.DrawElementBackground(
                position,
                elementIndex,
                isActive,
                isFocused,
                draggable: true
            );
        }

        //----------------------------------------------------------------------

        protected virtual void PopulateElementContextMenu(
            GenericMenu menu,
            int elementIndex)
        {
            menu.AddItem(new GUIContent("Insert Above"), false, () =>
            {
                serializedProperty.InsertArrayElementAtIndex(elementIndex);
                serializedProperty.serializedObject.ApplyModifiedProperties();
                index = elementIndex;
            });
            menu.AddItem(new GUIContent("Insert Below"), false, () =>
            {
                serializedProperty.InsertArrayElementAtIndex(elementIndex + 1);
                serializedProperty.serializedObject.ApplyModifiedProperties();
                index = elementIndex + 1;
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                serializedProperty.DeleteArrayElementAtIndex(elementIndex);
                serializedProperty.serializedObject.ApplyModifiedProperties();
                index = -1;
            });
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
            position.xMin += 6;
            position.y += 1;
            EditorGUI.LabelField(position, m_Label);
        }

        private void DrawFooterEdge(Rect position)
        {
            position.xMin += 2;
            position.xMax -= 2;
            position.y += position.height - 18;
            position.y -= 1;
            DrawHorizontalLine(position);
        }

        //----------------------------------------------------------------------

        private GUIContent m_Label = new GUIContent();

        private void UpdateLabel(GUIContent label)
        {
            m_Label.image = label.image;
            m_Label.tooltip = label.tooltip;

            var labelText = label.text;
            var arraySize = serializedProperty.arraySize;

            if (string.IsNullOrEmpty(labelText))
            {
                m_Label.text = string.Format("({0})", arraySize);
            }
            else
            {
                m_Label.text = string.Format("{0} ({1})", labelText, arraySize);
            }
        }

        //----------------------------------------------------------------------

        private readonly List<float> m_ElementHeights = new List<float>();

        private void UpdateElementHeights()
        {
            var array = serializedProperty;
            var length = array.arraySize;
            m_ElementHeights.Clear();
            m_ElementHeights.Capacity = length;
            for (int elementIndex = 0; elementIndex < length; ++elementIndex)
            {
                var element = array.GetArrayElementAtIndex(elementIndex);
                var interiorHeight = GetElementHeight(element, elementIndex);
                var exteriorHeight = AddElementPadding(interiorHeight);
                m_ElementHeights.Add(exteriorHeight);
            }
        }

        //----------------------------------------------------------------------

        private void DrawHeaderCallback(Rect position)
        {
            // Header is drawn in DoList()
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

    }

}

#endif // UNITY_EDITOR