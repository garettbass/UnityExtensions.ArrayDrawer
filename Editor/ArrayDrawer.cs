using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            return
                SerializedPropertyUtility
                .EnumerateChildProperties(parentProperty);
        }

        //----------------------------------------------------------------------

        protected static IEnumerable<SerializedProperty>
        EnumerateElementProperties(SerializedProperty arrayProperty)
        {
            return EnumerateChildProperties(arrayProperty).Skip(1);
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
        s_DefaultPropertyField =
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

        protected static bool DefaultPropertyField(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            return s_DefaultPropertyField(position, property, label);
        }

        //----------------------------------------------------------------------

        private delegate bool HasVisibleChildFieldsDelegate(
            SerializedProperty property);

        private static readonly HasVisibleChildFieldsDelegate
        s_HasVisibleChildFields =
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

        protected static bool HasVisibleChildFields(
            SerializedProperty property)
        {
            return s_HasVisibleChildFields(property);
        }

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

        protected IDisposable IndentLevelScope(int indent = 1)
        {
            EditorGUI.indentLevel += indent;
            return new Deferred(() => EditorGUI.indentLevel -= indent);
        }

    }

}