using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    public abstract class ArrayDrawerBase : DecoratorDrawer
    {

        internal ArrayDrawerBase() { }

        //----------------------------------------------------------------------

        public sealed override bool CanCacheInspectorGUI()
        {
            InjectArrayDrawer();
            return false;
        }

        public sealed override float GetHeight()
        {
            InjectArrayDrawer();
            return 0;
        }

        public sealed override void OnGUI(Rect position) { }

        //----------------------------------------------------------------------

        private void InjectArrayDrawer()
        {
            var propertyHandler = GetPropertyHandler();

            var propertyDrawer = GetPropertyDrawer(propertyHandler);

            Debug.Assert(propertyDrawer == null);

            propertyDrawer = new ArrayDrawerAdapter((ArrayDrawer)this);

            SetPropertyDrawer(propertyHandler, propertyDrawer);
        }

        //======================================================================

        private static readonly PropertyInfo
        s_PropertyHandlerCache =
            typeof(DecoratorDrawer)
            .Assembly
            .GetType("UnityEditor.ScriptAttributeUtility")
            .GetProperty(
                "propertyHandlerCache",
                BindingFlags.NonPublic |
                BindingFlags.Static
            );

        private static readonly FieldInfo
        s_PropertyHandlers =
            typeof(DecoratorDrawer)
            .Assembly
            .GetType("UnityEditor.PropertyHandlerCache")
            .GetField(
                "m_PropertyHandlers",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        internal object GetPropertyHandler()
        {
            var propertyHandlerCache =
                s_PropertyHandlerCache
                .GetValue(null, null);

            var propertyHandlerDictionary =
                (IDictionary)
                s_PropertyHandlers
                .GetValue(propertyHandlerCache);

            var propertyHandlers =
                propertyHandlerDictionary
                .Values;

            foreach (var propertyHandler in propertyHandlers)
            {
                var decoratorDrawers =
                    (List<DecoratorDrawer>)
                    s_PropertyHandler_DecoratorDrawers
                    .GetValue(propertyHandler);

                if (decoratorDrawers == null)
                    continue;

                var index = decoratorDrawers.IndexOf(this);
                if (index < 0)
                    continue;

                decoratorDrawers[index] = NullDrawer.Instance;

                return propertyHandler;
            }

            return null;
        }

        //======================================================================

        private static readonly Type
        s_PropertyHandler =
            typeof(DecoratorDrawer)
            .Assembly
            .GetType("UnityEditor.PropertyHandler");

        private static readonly FieldInfo
        s_PropertyHandler_PropertyDrawer =
            s_PropertyHandler
            .GetField(
                "m_PropertyDrawer",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        private static readonly FieldInfo
        s_PropertyHandler_DecoratorDrawers =
            s_PropertyHandler
            .GetField(
                "m_DecoratorDrawers",
                BindingFlags.NonPublic |
                BindingFlags.Instance
            );

        //----------------------------------------------------------------------

        internal static PropertyDrawer GetPropertyDrawer(object propertyHandler)
        {
            return
                (PropertyDrawer)
                s_PropertyHandler_PropertyDrawer
                .GetValue(propertyHandler);
        }

        internal static void SetPropertyDrawer(object propertyHandler, PropertyDrawer propertyDrawer)
        {
            s_PropertyHandler_PropertyDrawer
            .SetValue(propertyHandler, propertyDrawer);
        }

        //======================================================================

        private class NullDrawer : DecoratorDrawer
        {
            public static readonly DecoratorDrawer Instance = new NullDrawer();

            public sealed override bool CanCacheInspectorGUI()
            {
                return true;
            }

            public sealed override float GetHeight()
            {
                return 0;
            }

            public sealed override void OnGUI(Rect position)
            {
                //EditorGUI.DrawRect(position, Color.magenta);
            }
        }

    }

}