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

            var serializableTypes =
                AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(GetTypes)
                .Where(IsSerializable)
                .ToArray();

            foreach (var serializableType in serializableTypes)
            {
                var arrayType = serializableType.MakeArrayType();

            }

            var elapsedMs = (DateTime.Now - start).TotalMilliseconds;

            Debug.LogFormat("ReorderableListInjector took {0} ms", elapsedMs);

            Debug.Log(
                string.Join(
                    "\n",
                    serializableTypes.Select(t => t.FullName).ToArray()
                )
            );
        }

        private static bool IsSerializable(Type type)
        {
            return
                type.IsPrimitive ||
                HasSerializableAttribute(type);
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

        //======================================================================

        private static readonly Type
        s_ScriptAttributeUtility =
            typeof(PropertyDrawer)
            .Assembly
            .GetType("UnityEditor.ScriptAttributeUtility");

        private static readonly Type
        s_DrawerKeySet =
            s_ScriptAttributeUtility
            .GetNestedType("UnityEditor.ScriptAttributeUtility+DrawerKeySet");

        private static readonly FieldInfo
        s_DrawerTypeForType =
            s_ScriptAttributeUtility
            .GetField(
                "s_DrawerTypeForType",
                BindingFlags.NonPublic |
                BindingFlags.Static
            );

    }

}