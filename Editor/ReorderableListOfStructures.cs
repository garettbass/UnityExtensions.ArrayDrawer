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

        protected static readonly GUIStyle
        CN_EntryBackEven = "CN EntryBackEven";

        protected override void DrawElementBackground(
            Rect position,
            SerializedProperty element,
            int elementIndex,
            bool isActive,
            bool isFocused)
        {
            if (IsRepaint() && element != null)
            {
                var dark = CN_EntryBackEven;
                var darkRect = position;
                darkRect.xMin += 2;
                darkRect.xMax -= 2;
                darkRect.yMin += 1;
                darkRect.yMax -= 1;
                dark.Draw(darkRect, false, false, false, false);
            }
            base.DrawElementBackground(
                position,
                element,
                elementIndex,
                isActive,
                isFocused
            );
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