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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.CodeGeneration;
using Orleans.Serialization;
using UnitTests.GrainInterfaces;

namespace UnitTests.Serialization
{
    /// <summary>
    /// Summary description for SerializationTests
    /// </summary>
    [TestClass]
    public class SerializationTestsJsonTypes
    {
        [TestInitialize]
        public void InitializeForTesting()
        {
            SerializationManager.InitializeForTesting();
        }

        [TestMethod, TestCategory("BVT"), TestCategory("Functional"), TestCategory("Serialization"), TestCategory("JSON")]
        public void SerializationTests_JObject_Example1()
        {
            const string json = @"{ 
                    CPU: 'Intel', 
                    Drives: [ 
                            'DVD read/writer', 
                            '500 gigabyte hard drive' 
                           ] 
                   }"; 
 
            JObject input = JObject.Parse(json);
            JObject output = SerializationManager.RoundTripSerializationForTesting(input);
            Assert.AreEqual(input.ToString(), output.ToString());
        }

        [RegisterSerializerAttribute]
        public class JObjectSerializationExample1
        {
            static JObjectSerializationExample1()
            {
                Register();
            }

            public static object DeepCopier(object original)
            {
                // I assume JObject is immutable, so no need to deep copy.
                // Alternatively, can copy via JObject.ToString and JObject.Parse().
                return original;
            }

            public static void Serializer(object untypedInput, BinaryTokenStreamWriter stream, Type expected)
            {
                var input = (JObject)(untypedInput);
                string str = input.ToString();
                SerializationManager.Serialize(str, stream);
            }

            public static object Deserializer(Type expected, BinaryTokenStreamReader stream)
            {
                var str = (string)(SerializationManager.Deserialize(typeof(string), stream));
                return JObject.Parse(str);
            }

            public static void Register()
            {
                SerializationManager.Register(typeof(JObject), DeepCopier, Serializer, Deserializer);
            }
        }
        
        [TestMethod, TestCategory("BVT"), TestCategory("Functional"), TestCategory("Serialization"), TestCategory("JSON")]
        public void SerializationTests_Json_InnerTypes_TypeNameHandling()
        {
            var original = new RootType();
            var str = JsonConvert.SerializeObject(original, JsonSerializationExample2.Settings);
            var jsonDeser = JsonConvert.DeserializeObject<RootType>(str, JsonSerializationExample2.Settings);
            // JsonConvert fully deserializes the object back into RootType and InnerType since we are using TypeNameHandling.All setting.
            Assert.AreEqual(typeof(InnerType), original.MyDictionary["obj1"].GetType());
            Assert.AreEqual(typeof(InnerType), jsonDeser.MyDictionary["obj1"].GetType());
            Assert.AreEqual(original, jsonDeser);

            // Orleans's SerializationManager also deserializes everything correctly, but it serializes it into its own binary format
            var orleansDeser = SerializationManager.RoundTripSerializationForTesting(original);
            Assert.AreEqual(typeof(InnerType), jsonDeser.MyDictionary["obj1"].GetType());
            Assert.AreEqual(original, orleansDeser);
    
            Console.WriteLine("Done OK.");
        }

        [TestMethod, TestCategory("BVT"), TestCategory("Functional"), TestCategory("Serialization"), TestCategory("JSON")]
        public void SerializationTests_Json_InnerTypes_NoTypeNameHandling()
        {
            var original = new RootType();
            var str = JsonConvert.SerializeObject(original);
            var jsonDeser = JsonConvert.DeserializeObject<RootType>(str);
            // Here we don't use TypeNameHandling.All setting, therefore the full type information is not preserved.
            // As a result JsonConvert leaves the inner types as JObjects after Deserialization.
            Assert.AreEqual(typeof(InnerType), original.MyDictionary["obj1"].GetType());
            Assert.AreEqual(typeof(JObject), jsonDeser.MyDictionary["obj1"].GetType());
            // The below Assert actualy fails since jsonDeser has JObjects instead of InnerTypes!
            // Assert.AreEqual(original, jsonDeser);
    
            // If we now take this half baked object: RootType at the root and JObject in the leaves
            // and pass it through .NET binary serializer it would fail on this object since JObject is not marked as [Serializable]
            // and therefore .NET binary serializer cannot serialize it.

            // If we now take this half baked object: RootType at the root and JObject in the leaves
            // and pass it through Orleans serializer it would work:
            // RootType will be serialized with Orleans custom serializer that will be generated since RootType is defined
            // in GrainInterfaces assembly and markled as [Serializable].
            // JObject that is referenced from RootType will be serialized with JsonSerialization_Example2 below.

            var orleansJsonDeser = SerializationManager.RoundTripSerializationForTesting(jsonDeser);
            Assert.AreEqual(typeof(JObject), orleansJsonDeser.MyDictionary["obj1"].GetType());
            // The below assert fails, but only since JObject does not correctly implement Equals.
            //Assert.AreEqual(jsonDeser, orleansJsonDeser);

            Console.WriteLine("Done OK.");
        }

        /// <summary>
        /// A different way to configure Json serializer.
        /// </summary>
        [RegisterSerializer]
        public class JsonSerializationExample2
        {
            internal static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.All
            };

            static JsonSerializationExample2()
            {
                Register();
            }

            public static object DeepCopier(object original)
            {
                // I assume JObject is immutable, so no need to deep copy.
                // Alternatively, can copy via JObject.ToString and JObject.Parse().
                return original;
            }

            public static void Serialize(object obj, BinaryTokenStreamWriter stream, Type expected)
            {
                var str = JsonConvert.SerializeObject(obj, Settings);
                SerializationManager.Serialize(str, stream);
            }

            public static object Deserialize(Type expected, BinaryTokenStreamReader stream)
            {
                var str = (string)SerializationManager.Deserialize(typeof(string), stream);
                return JsonConvert.DeserializeObject(str, expected);
            }

            public static void Register()
            {
                foreach (var type in new[]
                    {
                        typeof(JObject), typeof(JArray), typeof(JToken), typeof(JValue), typeof(JProperty), typeof(JConstructor), 
                    })
                {
                    SerializationManager.Register(type, DeepCopier, Serialize, Deserialize);
                }
            }
        }
    }
}
