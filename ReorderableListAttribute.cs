using System;
using UnityEngine;

namespace UnityExtensions
{

    [AttributeUsage(AttributeTargets.Field)]
    public class ReorderableListAttribute : PropertyAttribute
    {

        public bool disableDragging;

        public bool elementsAreSubassets;

        public string elementHeaderFormat;

        public ReorderableListAttribute() { }

        public ReorderableListAttribute(bool elementsAreSubassets)
        {
            this.elementsAreSubassets = elementsAreSubassets;
        }

    }

}