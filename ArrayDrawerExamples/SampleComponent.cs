using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.ArrayDrawerExamples
{

    public class SampleComponent : MonoBehaviour
    {

        //[ReorderableList]
        public List<int> intList = new List<int> { 1, 2, 3 };

        //[ReorderableList]
        public string[] stringArray = new[] { "a", "b", "c" };

        //[ReorderableList]
        public SampleStruct[] structArray;

        [ReorderableList(elementsAreSubassets = true)]
        public SampleSubasset[] subassetArray;

    }

}