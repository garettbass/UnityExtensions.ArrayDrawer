#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal class ReorderableListOfStructures : ReorderableListOfValues
    {

        public override bool showElementHeader
        {
            get { return hasElementHeaderFormat; }
        }

        protected float idealLabelWidth;

        public ReorderableListOfStructures(
            ReorderableListAttribute attribute,
            SerializedProperty property,
            Type listType,
            Type elementType)
        : base(attribute, property, listType, elementType)
        { }

        //----------------------------------------------------------------------

        protected override float GetElementHeight(
            SerializedProperty element,
            int elementIndex)
        {
            var properties = element.EnumerateChildProperties();
            return GetElementHeight(properties);
        }

        protected float GetElementHeight(
            IEnumerable<SerializedProperty> properties)
        {
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var height = 0f;

            if (showElementHeader)
            {
                height += headerHeight + spacing;
            }

            idealLabelWidth = 0f;
            var labelStyle = EditorStyles.label;
            var labelContent = new GUIContent();

            var propertyCount = 0;
            foreach (var property in properties)
            {
                if (propertyCount++ > 0)
                    height += spacing;

                height += GetPropertyHeight(property);

                labelContent.text = property.displayName;
                var minLabelWidth = labelStyle.CalcSize(labelContent).x;
                idealLabelWidth = Mathf.Max(idealLabelWidth, minLabelWidth);
            }
            idealLabelWidth += 8;
            return height;
        }

        //----------------------------------------------------------------------

        protected override float drawElementIndent { get { return 12; } }

        protected override void DrawElement(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive)
        {
            var properties = element.EnumerateChildProperties();
            DrawElement(position, properties, elementIndex, isActive);
        }

        protected void DrawElement(
            Rect position,
            IEnumerable<SerializedProperty> properties,
            int elementIndex,
            bool isActive)
        {
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            if (showElementHeader)
            {
                DrawElementHeader(position, elementIndex, isActive);
                position.y += headerHeight + spacing;
            }

            var labelWidth = Mathf.Min(idealLabelWidth, position.width * 0.4f);
            using (LabelWidthScope(labelWidth))
            {
                var propertyCount = 0;
                foreach (var property in properties)
                {
                    if (propertyCount++ > 0)
                        position.y += spacing;

                    position.height = GetPropertyHeight(property);
                    PropertyField(position, property);
                    position.y += position.height;
                }
            }
        }

        //----------------------------------------------------------------------

        protected static readonly GUIStyle
        HeaderBackgroundStyle = "Toolbar";

        private void DrawElementHeader(
            Rect position,
            int elementIndex,
            bool isActive)
        {
            position.xMin -= drawElementIndent;
            position.height = headerHeight;

            var titleContent = m_TitleContent;

            titleContent.text =
                hasElementHeaderFormat
                ? string.Format(elementHeaderFormat, elementIndex)
                : elementIndex.ToString();

            var titleStyle = EditorStyles.boldLabel;

            var titleWidth =
                titleStyle
                .CalcSize(titleContent)
                .x;

            if (IsRepaint())
            {
                var fillRect = position;
                fillRect.xMin -= draggable ? 18 : 4;
                fillRect.xMax += 4;
                fillRect.y -= 2;

                var fillStyle = HeaderBackgroundStyle;

                using (ColorAlphaScope(0.75f))
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

                var menuRect = position;
                menuRect.xMin = menuRect.xMax - 16;
                menuRect.yMin += 4;
                ContextMenuButtonStyle.Draw(menuRect, false, false, false, false);
            }
        }

    }

}

#endif // UNITY_EDITOR