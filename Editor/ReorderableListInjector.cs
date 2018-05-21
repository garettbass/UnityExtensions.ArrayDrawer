using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal static class ReorderableListInjector
    {

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var start = DateTime.Now;

            var ScriptAttributeUtility =
                typeof(PropertyDrawer)
                .Assembly
                .GetType("UnityEditor.ScriptAttributeUtility");

            var GetDrawerTypeForType =
                ScriptAttributeUtility
                .GetMethod(
                    "GetDrawerTypeForType",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                );

            // ensure initialization of
            // ScriptAttributeUtility.s_DrawerTypeForType
            GetDrawerTypeForType.Invoke(null, new object[] { typeof(object) });

            var DrawerTypeForType =
                (IDictionary)
                ScriptAttributeUtility
                .GetField(
                    "s_DrawerTypeForType",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
                .GetValue(null);

            var DrawerKeySet =
                typeof(PropertyDrawer)
                .Assembly
                .GetType("UnityEditor.ScriptAttributeUtility+DrawerKeySet");

            var DrawerKeySet_drawer =
                DrawerKeySet
                .GetField(
                    "drawer",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                );

            var DrawerKeySet_type =
                DrawerKeySet
                .GetField(
                    "type",
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance
                );

            var drawerType = typeof(ReorderableListDrawer);
            var drawerKeySet = Activator.CreateInstance(DrawerKeySet);
            DrawerKeySet_drawer.SetValue(drawerKeySet, drawerType);
            DrawerKeySet_type.SetValue(drawerKeySet, typeof(IList));

            DrawerTypeForType.Add(typeof(List<>), drawerKeySet);

            var serializableTypes =
                AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(GetTypes)
                .Where(IsSerializable);

            foreach (var serializableType in serializableTypes)
            {
                var arrayType = serializableType.MakeArrayType();
                if (DrawerTypeForType.Contains(arrayType))
                    continue;

                DrawerTypeForType.Add(arrayType, drawerKeySet);
            }

            var elapsedMs = (DateTime.Now - start).TotalMilliseconds;

            Debug.LogFormat("ReorderableListInjector took {0} ms", elapsedMs);
        }

        private static bool IsSerializable(Type type)
        {
            return
                type.IsPrimitive ||
                HasSerializableAttribute(type) ||
                IsPublicUnityEngineValueType(type);
        }

        private static bool IsPublicUnityEngineValueType(Type type)
        {
            return
                type.IsPublic &&
                type.IsValueType &&
                type.FullName.StartsWith("UnityEngine.");
        }

        private static bool HasSerializableAttribute(Type type)
        {

            return
                Attribute
                .GetCustomAttributes(type, inherit: true)
                .OfType<SerializableAttribute>()
                .Any();
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return
                    assembly != null
                    ? assembly.GetTypes()
                    : Type.EmptyTypes;
            }
            catch (ReflectionTypeLoadException)
            {
                return Type.EmptyTypes;
            }
        }

    }

}