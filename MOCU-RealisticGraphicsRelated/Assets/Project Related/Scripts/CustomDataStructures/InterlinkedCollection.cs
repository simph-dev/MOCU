using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CustomDataStructures
{
    /// <summary>
    /// Represents a collection that maintains a set of elements, allowing access to each element by any of its properties marked as a key
    /// </summary>
    public class InterlinkedCollection<T> : IEnumerable<T>
    {
        private Dictionary<object, Guid> elementToIdMap;    // here 'object' is one element from 'Data' ('Data' of <T> type)
        private Dictionary<Guid, T> idToDataMap;

        public InterlinkedCollection()
        {
            elementToIdMap = new();
            idToDataMap = new();
        }

        public T this[object key]
        {
            get => FindRelatedSet(key);
        }

        private T FindRelatedSet(object key)
        {
            if (!elementToIdMap.TryGetValue(key, out Guid id))
                throw new KeyNotFoundException($"The key was not found in the collection: {key}");

            return idToDataMap[id];
        }

        public void Add(T data)
        {
            if (data == null)
                throw new ArgumentNullException($"'data' can't be null");

            var id = Guid.NewGuid();
            idToDataMap[id] = data;

            foreach (var property in typeof(T).GetProperties())
            {
                var attribute = property.GetCustomAttribute<CanBeKeyAttribute>();
                var key = property.GetValue(data);

                // If property doesn't have attribute, isn't writable or key is duplicate -- error
                // If attribute is 'CanBeKey == false' or 'key == null' -- skip
                // Otherwise -- add (all valid properties can be keys)


                if (attribute == null)
                    throw new Exception($"Each field must have 'CanBeKey' attribute");

                if (!property.CanWrite)
                    throw new ArgumentException($"Property '{property.Name}' is not writable.");

                if (key != null && elementToIdMap.ContainsKey(key))
                    throw new Exception($"Duplicate key found: {key}. Key must be unique.");

                if (!attribute.CanBeKey || key == null || !IsValidDictionaryKey(key.GetType()))
                    continue;
                

                elementToIdMap[key] = id;
            }
        }

        public void UpdateSingleValue(object key, string propertyName, object newValue)
        {
            // If key is wrong, propertyName isn't valid, property doen't exist or newValue is duplicate of some key -- error
            // Delete oldKey if exists and add new key if it's valid
            // Update value in DATA


            if (!elementToIdMap.TryGetValue(key, out Guid id))
                throw new KeyNotFoundException($"No entry found for key: {key}");

            if (String.IsNullOrEmpty(propertyName))
                throw new ArgumentException($"Property '{propertyName}' can't be Null or Empty");


            T data = idToDataMap[id];
            var property = typeof(T).GetProperty(propertyName);
            

            if (property == null)
                throw new Exception($"Property '{propertyName}' not found on type {typeof(T).Name}");


            var attribute = property.GetCustomAttribute<CanBeKeyAttribute>();
            var oldValue = property.GetValue(data);

            if (attribute == null)
                throw new Exception($"Property '{propertyName}' must have 'CanBeKey' attribute");    // although this check has already been performed in the Add method

            if (object.Equals(oldValue, newValue))
                return;

            if (newValue != null && elementToIdMap.ContainsKey(newValue))
                throw new Exception($"Duplicate key found: {newValue}. Key must be unique.");

            if (oldValue != null && elementToIdMap.ContainsKey(oldValue))
                elementToIdMap.Remove(oldValue);

            if (newValue != null && attribute.CanBeKey && IsValidDictionaryKey(newValue.GetType()))
                elementToIdMap[newValue] = id;


            property.SetValue(data, newValue);
        }

        private bool IsValidDictionaryKey(Type type)
        {
            // Automatically pass reference types as valid dictionary keys
            if (!type.IsValueType)
                return true;

            // Check if type overrides Equals
            MethodInfo equalsMethod = type.GetMethod("Equals", new Type[] { typeof(object) });
            if (equalsMethod == null || equalsMethod.DeclaringType == typeof(object))
                return false;

            // Check if type overrides GetHashCode
            MethodInfo getHashCodeMethod = type.GetMethod("GetHashCode", Type.EmptyTypes);
            if (getHashCodeMethod == null || getHashCodeMethod.DeclaringType == typeof(object))
                return false;

            // Check if type implements IEquatable<type>
            Type equatableType = typeof(IEquatable<>).MakeGenericType(type);
            if (!equatableType.IsAssignableFrom(type))
                return false;

            return true;
        }


        public IEnumerator<T> GetEnumerator()
        {
            return idToDataMap.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }


    /// <summary>
    /// Specifies that a property of an object can be used as a key in an InterlinkedCollection.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CanBeKeyAttribute : Attribute
    {
        public bool CanBeKey { get; private set; }

        public CanBeKeyAttribute(bool canBeKey)
        {
            CanBeKey = canBeKey;
        }
    }

    /// <summary>
    /// Represents an element in an InterlinkedCollection with specified properties that can be used as keys.
    /// </summary>
    /*public class ExampleDataSet
    {
        [CanBeKey(true)]
        public string name { get; set; }

        [CanBeKey(true)]
        public int age { get; set; }

        [CanBeKey(false)]
        public bool isNormal { get; set; }
    }*/
}