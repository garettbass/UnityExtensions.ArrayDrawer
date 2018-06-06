using System;
using UnityEditor;
using UnityEngine;

namespace UnityExtensions
{

    [AttributeUsage(AttributeTargets.Field)]
    public class ReorderableListAttribute : PropertyAttribute
    {

        public readonly bool elementsAreSubassets;

        public ReorderableListAttribute() { }

        public ReorderableListAttribute(bool elementsAreSubassets)
        {
            this.elementsAreSubassets = elementsAreSubassets;
        }

    }

}