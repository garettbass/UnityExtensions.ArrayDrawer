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

        public ReorderableListOfStructures(
            SerializedProperty property,
            Type listType,
            Type elementType)
        : base(property, listType, elementType)
        { }

        //----------------------------------------------------------------------

        protected override float GetElementHeight(
            SerializedProperty element,
            int elementIndex)
        {
            var height = 0f;

            if (showElementHeader)
            {
                height += headerHeight;
            }

            var count = 0;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            foreach (var property in EnumerateChildProperties(element))
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
            if (showElementHeader)
            {
                DrawElementHeader(position, elementIndex, isActive);
                position.y += headerHeight;
            }

            position.xMin += 12;

            var count = 0;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            foreach (var property in EnumerateChildProperties(element))
            {
                if (count++ > 0)
                    position.y += spacing;

                position.height = GetPropertyHeight(property);
                PropertyField(position, property);
                position.y += position.height;
            }
        }

        //----------------------------------------------------------------------

        protected static readonly new GUIStyle
        ElementBackgroundStyle = "CN EntryBackEven";

        protected override void DrawElementBackground(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            base.DrawElementBackground(
                position,
                element,
                elementIndex,
                isActive,
                isFocused
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

        protected static readonly GUIStyle
        HeaderBackgroundStyle = "Toolbar";

        private void DrawElementHeader(
            Rect position,
            int elementIndex,
            bool isActive)
        {
            position.height = headerHeight;

            var titleContent = m_TitleContent;

            titleContent.text =
                string.Format(
                    elementHeaderFormat,
                    elementIndex
                );

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
            }
        }

        //----------------------------------------------------------------------

        private static IEnumerable<SerializedProperty>
        EnumerateChildProperties(SerializedProperty parentProperty)
        {
            var iterator = parentProperty.Copy();
            var end = iterator.GetEndProperty();
            if (iterator.NextVisible(enterChildren: true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end))
                        yield break;

                    yield return iterator;
                }
                while (iterator.NextVisible(enterChildren: false));
            }
        }

    }

}

#endif // UNITY_EDITOR