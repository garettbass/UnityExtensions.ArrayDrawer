using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    public class ArrayDrawer : ArrayDrawerBase
    {

        public FieldInfo fieldInfo { get; internal set; }

        public virtual bool CanCacheInspectorGUI(SerializedProperty property)
        {
            return true;
        }

        public virtual float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;

            if (property.isExpanded && HasVisibleChildFields(property))
            {
                var spacing = EditorGUIUtility.standardVerticalSpacing;
                foreach (var child in EnumerateChildProperties(property))
                {
                    height += spacing;
                    height +=
                        EditorGUI
                        .GetPropertyHeight(
                            child,
                            includeChildren: true
                        );
                }
            }

            return height;
        }

        public virtual void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            // EditorGUI.DrawRect(position, Color.yellow);

            position.height = EditorGUIUtility.singleLineHeight;
            DefaultPropertyField(position, property, label);

            if (property.isExpanded && HasVisibleChildFields(property))
            {
                var spacing = EditorGUIUtility.standardVerticalSpacing;

                using (IndentLevelScope())
                {
                    foreach (var child in EnumerateChildProperties(property))
                    {
                        position.y += spacing;
                        position.y += position.height;
                        position.height =
                            EditorGUI
                            .GetPropertyHeight(
                                child,
                                includeChildren: true
                            );

                        EditorGUI.PropertyField(
                            position,
                            child,
                            includeChildren: true
                        );

                    }
                }
            }
        }

        //----------------------------------------------------------------------

        protected static IEnumerable<SerializedProperty>
        EnumerateChildProperties(SerializedProperty parentProperty)
        {
            var parentPropertyPath = parentProperty.propertyPath;
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

        //----------------------------------------------------------------------

        private new void CanCacheInspectorGUI() { }

        private new void GetHeight() { }

        private new void OnGUI(Rect position) { }

        //----------------------------------------------------------------------

        private delegate bool DefaultPropertyFieldDelegate(
            Rect position,
            SerializedProperty property,
            GUIContent label);

        private static readonly DefaultPropertyFieldDelegate
        DefaultPropertyField =
            (DefaultPropertyFieldDelegate)
            Delegate.CreateDelegate(
                typeof(DefaultPropertyFieldDelegate),
                null,
                typeof(EditorGUI)
                .GetMethod(
                    "DefaultPropertyField",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
            );

        //----------------------------------------------------------------------

        private delegate bool HasVisibleChildFieldsDelegate(
            SerializedProperty property);

        private static readonly HasVisibleChildFieldsDelegate
        HasVisibleChildFields =
            (HasVisibleChildFieldsDelegate)
            Delegate.CreateDelegate(
                typeof(HasVisibleChildFieldsDelegate),
                null,
                typeof(EditorGUI)
                .GetMethod(
                    "HasVisibleChildFields",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
            );

        //----------------------------------------------------------------------

        private struct Deferred : IDisposable
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

        private Deferred IndentLevelScope()
        {
            EditorGUI.indentLevel += 1;
            return new Deferred(() => EditorGUI.indentLevel -= 1);
        }

        private Deferred ColorAlphaScope(float a)
        {
            var oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            return new Deferred(() => GUI.color = oldColor);
        }

    }

}