/*
 * File: Assets/Scripts/Game/Utility/ExtensionUtil.cs
 * Project: Demo
 * Company: com.luckygz
 * Porter: D.S.Qiu
 * Create Date: 7/23/2015 8:34:01 PM
 */
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

namespace Utility
{
    public static class IEnumeratorExtension
    {
        public static IEnumerator<TParent> Upcast<TParent, TChild>(this IEnumerator<TChild> source) 
            where TChild : TParent
        {
            while (source.MoveNext())
            {
                yield return source.Current;
            }
        }
    }

    public static class IEnumerableExtension
    {
        public static IEnumerable<TBase> Upcast<TBase, TDerived>(this IEnumerable<TDerived> source)
            where TDerived : TBase
        {
            foreach (TDerived item in source)
            {
                yield return item;
            }
        }

        /// <summary>
        ///   Perform the <paramref name="action" /> on each item in the list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            if (action == null || collection == null)
            {
                return collection;
            }

            foreach (var e in collection)
            {
                action(e);
            }

            return collection;
        }
    }


    public static class ExtensionUtility
    {
        public static T Cast<T>(this object obj) where T:class
        {
            return null;
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static int GetCount<T>(this T[] array)
        {
            if (IsNullOrEmpty(array))
                return 0;
            return array.Length;
        }

        public static int GetCount(this ICollection array)
        {
            if (IsNullOrEmpty(array))
                return 0;
            return array.Count;
        }

        public static bool IsNullOrEmpty<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }

        public static bool IsNullOrEmpty(this ICollection array)
        {
            return array == null || array.Count == 0;
        }

        public static string ToString(object value)
        {
            if (value is Array)
                return ToDebugString(value as Array);
			else if(value is IDictionary)
				return ToDebugString(value as IDictionary);
            else if (value is ICollection)
                return ToDebugString(value as ICollection);
            return value.ToString();
        }

        //只做调试调用
        public static string ToDebugString(this Array array)
        {
            if (!IsNullOrEmpty(array))
            {
                string elements = string.Empty;
                for (int i = 0; i < array.Length; i++)
                {
                    if (array.GetValue(i) == null)
                        elements += "null";
                    else
                        elements += ToString(array.GetValue(i));
                    if (i != array.Length - 1)
                        elements += ",";
                }
                return "[Length(" + array.Length + "):" + elements + "]";
                
            }
            return "[]";
        }

        //只做调试调用
        public static string ToDebugString(this ICollection array)
        {
            if (!IsNullOrEmpty(array))
            {
                string elements = string.Empty;
                int index = 0;
                foreach (var element in array)
                {
                    if (element == null)
                        elements += "null";
                    else
                        elements += ToString(element);
                    if (index != array.Count - 1)
                        elements += ",";
                    index++;
                }
                
                return "[Length(" + array.Count + "):" + elements + "]";

            }
            return "[]";
        }

        //只做调试调用
        public static string ToDebugString(this IDictionary array)
        {
            if (!IsNullOrEmpty(array))
            {
                ICollection keys = array.Keys;
                ICollection values = array.Values;
                    
                string elements = string.Empty;
                int index = 0;
                foreach (var element in keys)
                {
                    if (element == null)
                        elements += "null";
                    else
                        elements += ToString(element) + ":" + ToString(array[element]);
                    if (index != array.Count - 1)
                        elements += ",";
                    index++;
                }

                return "[Length(" + array.Count + "):" + elements + "]";

            }
            return "[]";
        }

        /// <summary>
        /// Merge the specified Dictionaries into source Dictionary.
        /// <param name="override_existing">If True, merge will override existing values</param>
        public static bool Merge<K, V>(this Dictionary<K,V> variable, bool override_existing, params Dictionary<K, V>[] others)
        {
            if (variable.IsNullOrEmpty())
            {
                return false;
            }
            bool result = true;
            try
            {
                foreach (var src in others)
                {
                    foreach (KeyValuePair<K, V> pair in src)
                    {
                        if (override_existing)
                            variable.Cover(pair.Key, pair.Value);
                        else if (!variable.ContainsKey(pair.Key))
                            variable.Add(pair.Key, pair.Value);
                    }
                }
            }
            catch
            {
                result = false;
            }

            return result;
        }

        public static void Cover<T, V>(this IDictionary dict, T key, V value)
        {
            if (dict.IsNullOrEmpty())
            {
                return;
            }
            if (dict.Contains(key))
                dict[key] = value;
            else
                dict.Add(key, value);
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }

        public static Transform DeepSearch(this Transform t, string s)
        {
            Transform dt = t.Find(s);
            if (dt != null)
                return dt;
            else
            {
                foreach (Transform child in t)
                {
                    dt = DeepSearch(child, s);
                    if (dt != null)
                        return dt;
                }
            }
            return null;
        }
    }
}
