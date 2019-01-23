using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal static class ReorderableListDrawerInjector
    {

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            UnityEngine.Profiling.Profiler.BeginSample("ReorderableListDrawerInjector");
            s_drawerKeySetDictionary.Add(typeof(List<>), s_drawerKeySet);
            ApplyToUnityObjectTypes();
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private static void ApplyToUnityObjectTypes()
        {
            var objType = typeof(UnityEngine.Object);
            var objAssembly = objType.Assembly;
            var objAssemblyFullName = objAssembly.FullName;

            var visited = new HashSet<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (AssemblyDependsOn(assembly, objAssemblyFullName))
                    foreach (var type in assembly.GetTypes())
                        if (type.IsClass &&
                            type.IsAbstract == false &&
                            objType.IsAssignableFrom(type))
                            ApplyToArraysAndListsInType(visited, type);
        }

        //----------------------------------------------------------------------

        private static bool AssemblyDependsOn(
            Assembly assembly,
            string dependencyFullName)
        {
            if (assembly.FullName == dependencyFullName)
                return true;

            foreach (var reference in assembly.GetReferencedAssemblies())
                if (reference.FullName == dependencyFullName)
                    return true;

            return false;
        }

        //----------------------------------------------------------------------

        private static void ApplyToArraysAndListsInType(
            HashSet<Type> visited,
            Type type)
        {
            if (visited.Add(type) == false)
                return;

            if (type.IsArray)
            {
                if (s_drawerKeySetDictionary.Contains(type) == false)
                    s_drawerKeySetDictionary.Add(type, s_drawerKeySet);

                ApplyToArraysAndListsInType(visited, type.GetElementType());
                return;
            }

            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (s_drawerKeySetDictionary.Contains(type) == false)
                    s_drawerKeySetDictionary.Add(type, s_drawerKeySet);

                ApplyToArraysAndListsInType(visited, type.GetGenericArguments()[0]);
                return;
            }

            const BindingFlags bindingFlags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            for (; type != null; type = type.BaseType)
                foreach (var field in type.GetFields(bindingFlags))
                    ApplyToArraysAndListsInType(visited, field.FieldType);
        }

        //----------------------------------------------------------------------

        private static readonly object s_drawerKeySet =
            CreateDrawerKeySet();

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

        private static readonly IDictionary s_drawerKeySetDictionary =
            GetDrawerKeySetDictionary();

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