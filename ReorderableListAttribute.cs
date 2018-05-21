using System;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    [AttributeUsage(AttributeTargets.Field)]
    public class ReorderableListAttribute : PropertyAttribute
    {

        public readonly bool subassets;

        public ReorderableListAttribute() { }

        public ReorderableListAttribute(bool subassets)
        {
            this.subassets = subassets;
        }

    }

}