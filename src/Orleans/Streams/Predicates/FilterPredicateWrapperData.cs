/*
Project Orleans Cloud Service SDK ver. 1.0
 
Copyright (c) Microsoft Corporation
 
All rights reserved.
 
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
associated documentation files (the ""Software""), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Reflection;
using System.Runtime.Serialization;
using Orleans.Runtime;

namespace Orleans.Streams
{
    public delegate bool StreamFilterPredicate(IStreamIdentity stream, object filterData, object item);

    /// <summary>
    /// This class is a [Serializable] function pointer to a static predicate method, used for stream filtering.
    /// The predicate function / lamda is not directly serialized, only the class / method info details required to reconstruct the function reference on the other side.
    /// Predicate filter functions must be staic (non-abstract) methods, so full class name and method name are sufficient info to rehydrate.
    /// </summary>
    [Serializable]
    internal class FilterPredicateWrapperData : IStreamFilterPredicateWrapper
    {
        public object FilterData { get; private set; }

        private string methodName;
        private string className;

        private const string SER_FIELD_CLASS  = "ClassName";
        private const string SER_FIELD_DATA   = "FilterData";
        private const string SER_FIELD_METHOD = "MethodName";

        [NonSerialized]
        private StreamFilterPredicate predicateFunc;

        internal FilterPredicateWrapperData(object filterData, StreamFilterPredicate pred)
        {
            CheckFilterPredicateFunc(pred); // Assert expected pre-conditions are always true.

            FilterData = filterData;
            predicateFunc = pred;

            DehydrateStaticFunc(pred);
        }

        #region ISerializable methods
        protected FilterPredicateWrapperData(SerializationInfo info, StreamingContext context)
        {
            FilterData = info.GetValue(SER_FIELD_DATA, typeof(object));
            methodName = info.GetString(SER_FIELD_METHOD);
            className  = info.GetString(SER_FIELD_CLASS);

            predicateFunc = RehydrateStaticFuncion(className, methodName);
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(SER_FIELD_DATA,   FilterData);
            info.AddValue(SER_FIELD_METHOD, methodName);
            info.AddValue(SER_FIELD_CLASS,  className);
        }
        #endregion

        public bool ShouldReceive(IStreamIdentity stream, object filterData, object item)
        {
            if (predicateFunc == null)
            {
                predicateFunc = RehydrateStaticFuncion(className, methodName);
            }
            return predicateFunc(stream, filterData, item);
        }

        private static StreamFilterPredicate RehydrateStaticFuncion(string funcClassName, string funcMethodName)
        {
            Type funcClassType = CachedTypeResolver.Instance.ResolveType(funcClassName);
            MethodInfo method = funcClassType.GetMethod(funcMethodName);
            StreamFilterPredicate pred = (StreamFilterPredicate) method.CreateDelegate(typeof(StreamFilterPredicate));
#if DEBUG
            CheckFilterPredicateFunc(pred); // Assert expected pre-conditions are always true.
#endif
            return pred;
        }

        private void DehydrateStaticFunc(StreamFilterPredicate pred)
        {
#if DEBUG
            CheckFilterPredicateFunc(pred); // Assert expected pre-conditions are always true.
#endif
            MethodInfo method = pred.Method;
            className = method.DeclaringType.FullName;
            methodName = method.Name;
        }

        /// <summary>
        /// Check that the user-supplied stream predicate function is valid.
        /// Stream predicate functions must be static and not abstract.
        /// </summary>
        /// <param name="func"></param>
        private static void CheckFilterPredicateFunc(StreamFilterPredicate predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate", "Stream Filter predicate function must not be null.");
            }

            MethodInfo method = predicate.Method;

            if (!method.IsStatic || method.IsAbstract)
            {
                throw new ArgumentException("Stream Filter predicate function must be static and not abstract.");
            }
        }

        public override string ToString()
        {
            return string.Format("StreamFilterFunction:Class={0},Method={1}", className, methodName);
        }
    }
}
