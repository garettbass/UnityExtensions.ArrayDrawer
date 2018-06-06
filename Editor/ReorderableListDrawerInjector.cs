using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal static class ReorderableListDrawerInjector
    {

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var drawerKeySet = CreateDrawerKeySet();
            var drawerKeySetDictionary = GetDrawerKeySetDictionary();
            //using (TimedScope.Begin())
            {
                drawerKeySetDictionary.Add(typeof(List<>), drawerKeySet);

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
                    if (drawerKeySetDictionary.Contains(arrayType))
                        continue;

                    drawerKeySetDictionary.Add(arrayType, drawerKeySet);
                }
            }
        }

        //----------------------------------------------------------------------

        private static object CreateDrawerKeySet()
        {
            // using (TimedScope.Begin())
            {
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

                return drawerKeySet;
            }
        }

        //----------------------------------------------------------------------

        private static IDictionary GetDrawerKeySetDictionary()
        {
            // using (TimedScope.Begin())
            {
                var ScriptAttributeUtility =
                    typeof(PropertyDrawer)
                    .Assembly
                    .GetType("UnityEditor.ScriptAttributeUtility");

                // ensure initialization of
                // ScriptAttributeUtility.s_DrawerTypeForType
                ScriptAttributeUtility
                .GetMethod(
                    "GetDrawerTypeForType",
                    BindingFlags.NonPublic |
                    BindingFlags.Static
                )
                .Invoke(null, new object[] { typeof(object) });

                return
                    (IDictionary)
                    ScriptAttributeUtility
                    .GetField(
                        "s_DrawerTypeForType",
                        BindingFlags.NonPublic |
                        BindingFlags.Static
                    )
                    .GetValue(null);
            }
        }

        //----------------------------------------------------------------------

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
                type.FullName.StartsWith("Unity");
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

        private struct TimedScope : IDisposable
        {
            private readonly string _description;

            private readonly StackTrace _stackTrace;

            private readonly DateTime _timeBegan;

            private DateTime _timeEnded;

            //------------------------------------------------------------------

            private TimedScope(string description)
            {
                var stackTrace = new StackTrace(2);
                _description = description ?? DefaultDescription(stackTrace);
                _stackTrace = new StackTrace(2);
                _timeEnded = default(DateTime);
                _timeBegan = DateTime.Now;
            }

            //------------------------------------------------------------------

            private static string DefaultDescription(StackTrace stackTrace)
            {
                var frame = stackTrace.GetFrame(0);
                var method = frame.GetMethod();
                var className = method.DeclaringType.Name;
                return string.Format("{0}.{1}()", className, method.Name);
            }

            //------------------------------------------------------------------

            public string Description { get { return _description; } }

            public bool HasEnded { get { return _timeEnded != default(DateTime); } }

            public StackTrace StackTrace { get { return _stackTrace; } }

            public DateTime TimeBegan { get { return _timeBegan; } }

            public TimeSpan TimeElapsed
            {
                get
                {
                    var endTime = HasEnded ? _timeEnded : DateTime.Now;
                    return endTime - _timeBegan;
                }
            }

            //------------------------------------------------------------------

            public static TimedScope Begin()
            {
                return new TimedScope(null);
            }

            public static TimedScope Begin(string description)
            {
                return new TimedScope(description);
            }

            public void End()
            {
                _timeEnded = DateTime.Now;
                Log(this);
            }

            //------------------------------------------------------------------

            void IDisposable.Dispose()
            {
                End();
            }

            //------------------------------------------------------------------

            public static void Log(TimedScope timedScope)
            {
                UnityEngine.Debug.Log(ToString(timedScope));
            }

            //------------------------------------------------------------------

            public override string ToString()
            {
                return ToString(this);
            }

            public static string ToString(TimedScope timedScope)
            {
                return
                    string
                    .Format(
                        "{0} took {1}\n{2}",
                        timedScope.Description,
                        ToString(timedScope.TimeElapsed),
                        timedScope.StackTrace.ToString()
                    );
            }

            public static string ToString(TimeSpan timeSpan)
            {
                var period = 0.0;
                var unit = "";
                if ((period = timeSpan.TotalDays) > 1)
                {
                    unit = "days";
                }
                else if ((period = timeSpan.TotalHours) > 1)
                {
                    unit = "hours";
                }
                else if ((period = timeSpan.TotalMinutes) > 1)
                {
                    unit = "minutes";
                }
                else if ((period = timeSpan.TotalSeconds) > 1)
                {
                    unit = "seconds";
                }
                else
                {
                    period = timeSpan.TotalMilliseconds;
                    unit = "milliseconds";
                }
                return
                    string
                    .Format(
                        "{0} {1}",
                        (int)period,
                        unit
                    );
            }

        }

    }

}