#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

using Object = UnityEngine.Object;
using ParallelListLayout = UnityExtensions.ReorderableListAttribute.ParallelListLayout;
using BackgroundColorDelegate = UnityExtensions.ReorderableListDrawer.BackgroundColorDelegate;
using System.Collections;

namespace UnityExtensions
{

    internal class ReorderableListOfValues : ReorderableList
    {

        private DateTime m_lastRendered = DateTime.MaxValue;
        public DateTime lastRendered
        {
            get { return m_lastRendered; }
        }

        private const float kIndentPerLevel = 15;

        public readonly Type listType;

        public readonly Type elementType;

        public string elementHeaderFormat;

        public bool hasElementHeaderFormat
        {
            get
            {
                return elementHeaderFormat != null;
            }
        }

        public string singularListHeaderFormat;

        public string pluralListHeaderFormat;

        public virtual bool showElementHeader
        {
            get { return false; }
        }

        public readonly bool showFooterButtons;

        public Color backgroundColor;

        internal BackgroundColorDelegate onBackgroundColor;

        public readonly SerializedProperty[] serializedProperties;

        public readonly ParallelListLayout parallelListLayout;

        protected static readonly new Defaults
        defaultBehaviours = new Defaults();

        protected readonly GUIContent m_TitleContent = new GUIContent();

        //----------------------------------------------------------------------

        public ReorderableListOfValues(
            ReorderableListAttribute attribute,
            SerializedProperty primaryProperty,
            Type listType,
            Type elementType)
        : base(
            serializedObject: primaryProperty.serializedObject,
            elements: primaryProperty.Copy(),
            draggable: !attribute.disableDragging,
            displayHeader: true,
            displayAddButton: !attribute.disableAdding,
            displayRemoveButton: !attribute.disableRemoving)
        {
            this.listType = listType;
            this.elementType = elementType;
            this.elementHeaderFormat = attribute.elementHeaderFormat;
            this.showFooterButtons =
                (displayAdd || displayRemove)
                && !attribute.hideFooterButtons;
            this.singularListHeaderFormat =
                attribute.singularListHeaderFormat
                ?? "{0} ({1})";
            this.pluralListHeaderFormat =
                attribute.pluralListHeaderFormat
                ?? "{0} ({1})";
            this.backgroundColor =
                new Color(
                    attribute.r,
                    attribute.g,
                    attribute.b);
            this.serializedProperties =
                AcquireSerializedProperties(
                    this.serializedProperty,
                    attribute.parallelListNames);
            this.parallelListLayout = attribute.parallelListLayout;

            headerHeight -= 2;
            drawHeaderCallback = DrawHeaderCallback;
            drawFooterCallback = DrawFooterCallback;
            elementHeightCallback = ElementHeightCallback;
            drawElementCallback = DrawElementCallback;
            drawElementBackgroundCallback = DrawElementBackgroundCallback;

            onAddCallback = OnAddCallback;
            onCanRemoveCallback = OnCanRemoveCallback;
            onRemoveCallback = OnRemoveCallback;

            onSelectCallback = OnSelectCallback;
            onReorderCallback = OnReorderCallback;

#if UNITY_2018_1_OR_NEWER
            drawNoneElementCallback = DrawEmptyElementCallback;
#endif // UNITY_2018_1_OR_NEWER
        }

        //----------------------------------------------------------------------

        private int m_dragIndex = 0;

        private void OnSelectCallback(ReorderableList list)
        {
            m_dragIndex = list.index;
        }

        private void OnReorderCallback(ReorderableList list)
        {
            var dragIndex = m_dragIndex;
            if (dragIndex < 0)
                return;

            var dropIndex = list.index;
            if (dropIndex < 0)
                return;

            try
            {
                for (int i = 1; i < serializedProperties.Length; ++i)
                {
                    var array = serializedProperties[i];
                    array.MoveArrayElement(dragIndex, dropIndex);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        //----------------------------------------------------------------------

        private static SerializedProperty[] AcquireSerializedProperties(
            SerializedProperty primaryProperty,
            string[] parallelListNames)
        {
            if (parallelListNames == null || parallelListNames.Length == 0)
                return new[] { primaryProperty };

            var serializedObject = primaryProperty.serializedObject;

            var serializedProperties =
                new List<SerializedProperty>(
                    1 + parallelListNames.Length);
            serializedProperties.Add(primaryProperty);

            var primaryArraySize = primaryProperty.arraySize;

            var primaryPropertyPath = primaryProperty.propertyPath;
            var lastDotIndex = primaryPropertyPath.LastIndexOf('.');
            var parallelPropertyPrefix =
                primaryPropertyPath.Substring(0, lastDotIndex + 1);
            foreach (var parallelListName in parallelListNames)
            {
                var parallelPropertyPath =
                    parallelPropertyPrefix +
                    parallelListName;
                var parallelProperty =
                    serializedObject
                    .FindProperty(parallelPropertyPath);
                if (parallelProperty != null &&
                    parallelProperty.isArray)
                {
                    ResizeArray(parallelProperty, primaryArraySize);
                    serializedProperties.Add(parallelProperty);
                }
            }
            return serializedProperties.ToArray();
        }

        private static void ResizeArray(SerializedProperty property, int arraySize)
        {
            while (property.arraySize < arraySize)
            {
                property.InsertArrayElementAtIndex(property.arraySize);
            }
            while (property.arraySize > arraySize)
            {
                property.DeleteArrayElementAtIndex(property.arraySize - 1);
            }
        }

        //----------------------------------------------------------------------

        public float GetHeight(GUIContent label)
        {
            m_lastRendered = DateTime.Now;
            UpdateLabel(label);
            UpdateElementHeights();
            var height = GetHeight();

            if (!showFooterButtons)
            {
                height -= 14; // no add/remove buttons in footer
            }

            if (!serializedProperty.isExpanded)
            {
                var elementCount = m_ElementHeights.Count;
                if (elementCount == 0)
                    height -= 21; // no empty element
            }

            return height;
        }

        public virtual void DoGUI(Rect position)
        {
            if ( m_onNextGUIFrame != null ) m_onNextGUIFrame.Invoke();
            m_onNextGUIFrame = null;

            if (!displayAdd && !displayRemove && !draggable)
            {
                index = -1;
            }

            position.xMin += EditorGUI.indentLevel * kIndentPerLevel;

            using (IndentLevelScope(-EditorGUI.indentLevel))
            {
                if (serializedProperty.isExpanded)
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
            InsertElement(serializedProperty.arraySize);
        }

        private bool OnCanRemoveCallback(ReorderableList list)
        {
            return serializedProperty.isExpanded;
        }

        private void OnRemoveCallback(ReorderableList list)
        {
            DeleteElement(index);
        }

        //----------------------------------------------------------------------

        [Serializable]
        private class ClipboardContent
        {
            public ClipboardElement[] elements;

            public ClipboardContent(int elementCount)
            {
                elements = new ClipboardElement[elementCount];
            }

            public static ClipboardContent Deserialize(string s)
            {
                try
                {
                    return JsonUtility.FromJson<ClipboardContent>(s);
                }
                catch
                {
                    return null;
                }
            }

            public string Serialize()
            {
                return JsonUtility.ToJson(this);
            }
        }

        [Serializable]
        private struct ClipboardElement
        {
            public string type;
            public string json;
        }

        private void CopyElement(int elementIndex)
        {
            if (elementIndex < 0)
                return;

            var arrayIndex = 0;
            var arrayCount = serializedProperties.Length;
            var clipboardContent = new ClipboardContent(arrayCount);
            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;
            foreach (var array in serializedProperties)
            {
                var arrayObj = (IList)array.GetObject();
                var elementObj = arrayObj[elementIndex];
                var elementType = elementObj.GetType();
                var elementJson = JsonUtility.ToJson(elementObj);
                var clipboardElement = new ClipboardElement();
                clipboardElement.type = elementType.FullName;
                clipboardElement.json = elementJson;
                clipboardContent.elements[arrayIndex] = clipboardElement;
                arrayIndex += 1;
            }
            EditorGUIUtility.systemCopyBuffer = clipboardContent.Serialize();
        }

        private void CutElement(int elementIndex)
        {
            if (elementIndex < 0)
                return;

            CopyElement(elementIndex);
            DeleteElement(elementIndex);
        }

        private bool CanPaste(ClipboardContent clipboardContent)
        {
            if (clipboardContent == null)
                return false;

            var arrayIndex = 0;
            var arrayCount = serializedProperties.Length;
            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;
            foreach (var array in serializedProperties)
            {
                var arrayObj = (IList)array.GetObject();
                var arrayType = arrayObj.GetType();
                var elementType =
                    (arrayType.IsArray)
                    ? arrayType.GetElementType()
                    : arrayType.GetGenericArguments()[0];
                
                var clipboardElement = clipboardContent.elements[arrayIndex++];
                if (clipboardElement.type != elementType.FullName)
                    return false;
            }
            return true;
        }

        private void PasteElement(int elementIndex, ClipboardContent clipboardContent)
        {
            if (elementIndex < 0)
                return;

            var clipboardElements = clipboardContent.elements;
            if (clipboardElements.Length == 0)
                return;

            var arrayIndex = 0;
            var arrayCount = serializedProperties.Length;
            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;
            var targetObject = serializedObject.targetObject;
            Undo.RecordObject(targetObject, string.Format( "Paste {0}", clipboardElements[ 0 ].type ) );
            foreach (var array in serializedProperties)
            {
                if (elementIndex >= array.arraySize)
                    array.arraySize = elementIndex + 1;

                var clipboardElement = clipboardContent.elements[arrayIndex++];
                var arrayObj = (IList)array.GetObject();
                var elementObj = arrayObj[elementIndex];
                var elementJson = clipboardElement.json;
                JsonUtility.FromJsonOverwrite(elementJson, elementObj);
            }
            serializedObject.Update();
            GUI.changed = true;
        }

        //----------------------------------------------------------------------

        protected virtual void InsertElement(int elementIndex)
        {
            if (elementIndex < 0)
                return;

            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;
            foreach (var array in serializedProperties)
            {
                array.InsertArrayElementAtIndex(elementIndex);
            }
            serializedObject.ApplyModifiedProperties();
            index = elementIndex;
            GUI.changed = true;
        }

        protected virtual void DeleteElement(int elementIndex)
        {
            if (elementIndex < 0)
                return;

            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;
            if (elementIndex < serializedProperty.arraySize)
            {
                foreach (var array in serializedProperties)
                {
                    var element = array.GetArrayElementAtIndex(elementIndex);
                    var oldSubassets = element.FindReferencedSubassets();
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
                }

                var length = serializedProperty.arraySize;
                if (index > length - 1)
                    index = length - 1;
            }
            GUI.changed = true;
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
            bool isActive)
        {
            PropertyField(position, element, GUIContent.none);
        }

        //----------------------------------------------------------------------

        protected static readonly GUIStyle
        ElementBackgroundStyle = "CN EntryBackEven";

        private void DrawElementBackground(
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

                var backgroundColor =
                    (this.backgroundColor == Color.black)
                    ? GUI.backgroundColor
                    : this.backgroundColor;

                if ( onBackgroundColor != null )
                    onBackgroundColor.Invoke( serializedProperty, elementIndex, ref this.backgroundColor );

                using (BackgroundColorScope(backgroundColor))
                using (ColorAlphaScope(isActive ? 0.5f : 1))
                {
                    fillStyle.Draw(fillRect, false, false, false, false);
                }
            }
        }

        //----------------------------------------------------------------------

        private Action m_onNextGUIFrame;

        protected void OnNextGUIFrame(Action action)
        {
            m_onNextGUIFrame += action;
        }

        //----------------------------------------------------------------------

        public static readonly GUIContent CutLabel = new GUIContent("Cut");
        public static readonly GUIContent CopyLabel = new GUIContent("Copy");
        public static readonly GUIContent PasteLabel = new GUIContent("Paste");

        protected virtual void PopulateElementContextMenu(
            GenericMenu menu,
            int elementIndex)
        {
            var serializedProperty = this.serializedProperty;
            var serializedObject = serializedProperty.serializedObject;

            menu.AddItem(CutLabel,false,() =>
                OnNextGUIFrame(() => CutElement(elementIndex)));
            menu.AddItem(CopyLabel,false,() =>
                CopyElement(elementIndex));
            var content = ClipboardContent.Deserialize(EditorGUIUtility.systemCopyBuffer);
            var canPaste = CanPaste(content);
            if (canPaste) menu.AddItem(PasteLabel,false,() =>
                OnNextGUIFrame(() => PasteElement(elementIndex, content)));
            else menu.AddDisabledItem(PasteLabel);

            if (displayAdd)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Insert Above"), false, () =>
                    OnNextGUIFrame(() => InsertElement(elementIndex)));
                menu.AddItem(new GUIContent("Insert Below"), false, () =>
                    OnNextGUIFrame(() => InsertElement(elementIndex + 1)));

            }
            if (displayAdd && displayRemove)
            {
                menu.AddSeparator("");
            }
            if (displayRemove)
            {
                menu.AddItem(new GUIContent("Remove"), false, () =>
                    OnNextGUIFrame(() => DeleteElement(elementIndex)));
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

        protected static readonly GUIStyle
        ContextMenuButtonStyle = "Icon.TrackOptions";

        protected GUIContent IconToolbarPlus
        {
            get { return defaultBehaviours.iconToolbarPlus; }
        }

        protected GUIContent IconToolbarPlusMore
        {
            get { return defaultBehaviours.iconToolbarPlusMore; }
        }

        protected GUIContent IconToolbarMinus
        {
            get { return defaultBehaviours.iconToolbarMinus; }
        }

        protected GUIStyle PreButton
        {
            get { return defaultBehaviours.preButton; }
        }

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

            var foldoutRect = position;
            foldoutRect.width -= 50;
            var property = serializedProperty;
            var wasExpanded = property.isExpanded;
            var isExpanded = EditorGUI.Foldout(foldoutRect, wasExpanded, m_Label);
            if (isExpanded != wasExpanded)
            {
                property.isExpanded = isExpanded;
            }

            DrawHeaderButtons(position);
        }

        private void DrawHeaderButtons(Rect position)
        {
            position.yMin += 3;
            float rightEdge = position.xMax;
            float leftEdge = rightEdge - 8f;
            if (displayAdd)
                leftEdge -= 25;
            if (displayRemove)
                leftEdge -= 25;
            position = new Rect(leftEdge, position.y, rightEdge - leftEdge, position.height);
            Rect addRect = new Rect(leftEdge + 4, position.y - 3, 25, 13);
            Rect removeRect = new Rect(rightEdge - 29, position.y - 3, 25, 13);
            if (displayAdd)
                DrawAddButton(addRect);
            if (displayRemove)
                DrawRemoveButton(removeRect);
        }

        private void DrawAddButton(Rect position)
        {
            var canAdd = onCanAddCallback == null || onCanAddCallback(this);
            var disabled = !canAdd;
            using (new EditorGUI.DisabledScope(disabled))
            {
                var style = PreButton;
                var content =
                    onAddDropdownCallback != null
                    ? IconToolbarPlusMore
                    : IconToolbarPlus;
                if (GUI.Button(position, content, style))
                {
                    if (onAddDropdownCallback != null)
                        onAddDropdownCallback(position, this);
                    else if (onAddCallback != null)
                        onAddCallback(this);
                    if ( onChangedCallback != null ) 
                        onChangedCallback.Invoke( this );
                }
            }
        }

        private void DrawRemoveButton(Rect position)
        {
            var disabled = index < 0 || index > count;
            if (disabled == false)
            {
                var canRemove = onCanRemoveCallback == null || onCanRemoveCallback(this);
                disabled |= !canRemove;
            }
            using (new EditorGUI.DisabledScope(disabled))
            {
                var style = PreButton;
                var content = IconToolbarMinus;
                if (GUI.Button(position, content, style))
                {
                    if ( onRemoveCallback != null ) 
                        onRemoveCallback.Invoke( this );
                    if ( onChangedCallback != null ) 
                        onChangedCallback.Invoke( this );
                }
            }
        }

        //----------------------------------------------------------------------

        private GUIContent m_Label = new GUIContent();

        private void UpdateLabel(GUIContent label)
        {
            m_Label.image = label.image;

            var tooltip = label.tooltip;
            if (string.IsNullOrEmpty(tooltip))
            {
                tooltip = serializedProperty.tooltip;
            }
            m_Label.tooltip = tooltip;

            var arraySize = serializedProperty.arraySize;

            var listHeaderFormat =
                (arraySize != 1)
                ? pluralListHeaderFormat
                : singularListHeaderFormat;

            var text = label.text ?? string.Empty;
            text = string.Format(listHeaderFormat, text, arraySize).Trim();
            m_Label.text = text;
        }

        //----------------------------------------------------------------------

        private readonly List<float> m_ElementHeights = new List<float>();

        private void UpdateElementHeights()
        {
            var primaryProperty = serializedProperty;
            var elementCount = primaryProperty.arraySize;
            m_ElementHeights.Clear();
            m_ElementHeights.Capacity = elementCount;
            for (int i = 0; i < elementCount; ++i)
                m_ElementHeights.Add(0f);

            if (primaryProperty.isExpanded)
            {
                var spacing = EditorGUIUtility.standardVerticalSpacing;
                var arrayCount = 0;
                foreach (var array in serializedProperties)
                {
                    for (int i = 0; i < elementCount; ++i)
                    {
                        var element = array.GetArrayElementAtIndex(i);
                        var elementHeight = GetElementHeight(element, i);
                        if (arrayCount > 0)
                            elementHeight += spacing;
                        switch (parallelListLayout)
                        {
                            case ParallelListLayout.Rows:
                                m_ElementHeights[i] += elementHeight;
                                break;
                            case ParallelListLayout.Columns:
                                m_ElementHeights[i] =
                                    Mathf.Max(
                                        m_ElementHeights[i],
                                        elementHeight);
                                break;
                        }
                    }
                    arrayCount += 1;
                }
                for (int i = 0; i < elementCount; ++i)
                {
                    var elementHeight = m_ElementHeights[i];
                    m_ElementHeights[i] = AddElementPadding(elementHeight);
                }
            }
        }

        //----------------------------------------------------------------------

        private void DrawHeaderCallback(Rect position)
        {
            // DoGUI draws the header content after the list is drawn
        }

        private void DrawFooterCallback(Rect position)
        {
            if (showFooterButtons)
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

        protected virtual float drawElementIndent { get { return 0; } }

        private void DrawElementCallback(
            Rect position,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            var primaryProperty = serializedProperty;
            if (primaryProperty.isExpanded)
            {
                RemoveElementPadding(ref position);
                position.xMin += drawElementIndent;
                switch (parallelListLayout)
                {
                    case ParallelListLayout.Rows:
                        DrawElementRows(position, elementIndex, isActive);
                        break;
                    case ParallelListLayout.Columns:
                        DrawElementColumns(position, elementIndex, isActive);
                        break;
                }
            }
        }

        private void DrawElementRows(Rect position, int elementIndex, bool isActive)
        {
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var loopCounter = 0;
            foreach (var array in serializedProperties)
            {
                if (loopCounter++ > 0)
                    position.y += spacing;

                var element = array.GetArrayElementAtIndex(elementIndex);
                position.height = GetElementHeight(element, elementIndex);
                DrawElement(position, element, elementIndex, isActive);
                position.y += position.height;
            }
        }

        private void DrawElementColumns(Rect position, int elementIndex, bool isActive)
        {
            const float columnSpacing = 5;
            var lastColumnXMax = position.xMax;
            var columnCount = serializedProperties.Length;
            var columnSpaceCount = columnCount - 1;
            var columnSpaceWidth = columnSpacing * columnSpaceCount;
            var columnWidth = (position.width - columnSpaceWidth) / columnCount;
            columnWidth = Mathf.Floor(columnWidth);
            position.width = columnWidth;
            var loopCounter = 0;
            foreach (var array in serializedProperties)
            {
                if (loopCounter++ > 0)
                    position.x += columnSpacing + columnWidth;

                if (loopCounter == columnCount)
                    position.xMax = lastColumnXMax;

                var element = array.GetArrayElementAtIndex(elementIndex);
                position.height = GetElementHeight(element, elementIndex);
                DrawElement(position, element, elementIndex, isActive);
            }
        }

        private void DrawElementBackgroundCallback(
            Rect position,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            var array = this.serializedProperty;
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
            var menuRect = Rect.zero;
            if (showElementHeader)
            {
                handleRect.width += 1;
                menuRect = position;
                menuRect.xMin = menuRect.xMax - 16;
            }
            else
            {
                handleRect.width = 19;
            }

            var isLeftMouseInMenuRect =
                @event.button == 0 &&
                menuRect.Contains(@event.mousePosition);

            var isRightMouseInHandleRect =
                @event.button == 1 &&
                handleRect.Contains(@event.mousePosition);

            var isMouseInRect =
                isLeftMouseInMenuRect ||
                isRightMouseInHandleRect;

            var isActiveElementIndex = index == elementIndex;

            switch (@event.type)
            {
                case EventType.MouseDown:
                    if (isMouseInRect)
                    {
                        EndEditingActiveTextField();
                        index = elementIndex;
                        return;
                    }
                    break;

                case EventType.MouseUp:
                    if (isMouseInRect && isActiveElementIndex)
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
            position.yMin += borderHeight;
            position.yMin += verticalSpacing;
            position.yMax -= verticalSpacing;
            position.yMax -= 1;
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

        protected static Deferred BackgroundColorScope(Color newColor)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = newColor;
            return new Deferred(() => GUI.backgroundColor = oldColor);
        }

        protected static Deferred ColorScope(Color newColor)
        {
            var oldColor = GUI.color;
            GUI.color = newColor;
            return new Deferred(() => GUI.color = oldColor);
        }

        protected static Deferred ColorAlphaScope(float a)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            return new Deferred(() => GUI.color = oldColor);
        }

        protected static Deferred IndentLevelScope(int indent = 1)
        {
            EditorGUI.indentLevel += indent;
            return new Deferred(() => EditorGUI.indentLevel -= indent);
        }

        protected IDisposable LabelWidthScope(float newLabelWidth)
        {
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = (int)newLabelWidth;
            return new Deferred(() =>
                EditorGUIUtility.labelWidth = oldLabelWidth);
        }

        //======================================================================

        protected static void TryDestroyImmediate(
            Object obj,
            bool allowDestroyingAssets = false)
        {
            try
            {
                if (obj != null)
                    Object.DestroyImmediate(obj, allowDestroyingAssets);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

    }

}

#endif // UNITY_EDITOR