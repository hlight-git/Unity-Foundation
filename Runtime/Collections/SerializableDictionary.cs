using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hlight.Foundation
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> :
        IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>,
        ISerializationCallbackReceiver
    {
        [Serializable]
        public sealed class Entry
        {
            public TKey key;
            public TValue value;
        }

        [SerializeField] private List<Entry> entries = new();
        [NonSerialized] private Dictionary<TKey, TValue> lookup;

        public SerializableDictionary()
        {
        }

        public SerializableDictionary(IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) throw new ArgumentNullException(nameof(dictionary));

            foreach (var item in dictionary)
                Add(item.Key, item.Value);
        }

        public TValue this[TKey key]
        {
            get => Lookup[key];
            set
            {
                Lookup[key] = value;

                var index = FindEntryIndex(key);
                if (index >= 0)
                    entries[index].value = value;
                else
                    entries.Add(new Entry { key = key, value = value });

                RemoveDuplicateEntries(key, index);
            }
        }

        public ICollection<TKey> Keys => Lookup.Keys;
        public ICollection<TValue> Values => Lookup.Values;
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Lookup.Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Lookup.Values;
        public int Count => Lookup.Count;
        public bool IsReadOnly => false;

        private Dictionary<TKey, TValue> Lookup => lookup ??= BuildLookup();

        public void Add(TKey key, TValue value)
        {
            Lookup.Add(key, value);
            entries.Add(new Entry { key = key, value = value });
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (!Lookup.TryAdd(key, value))
                return false;

            entries.Add(new Entry { key = key, value = value });
            return true;
        }

        public bool ContainsKey(TKey key) => Lookup.ContainsKey(key);

        public bool Remove(TKey key)
        {
            if (!Lookup.Remove(key))
                return false;

            RemoveEntries(key);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value) => Lookup.TryGetValue(key, out value);

        public void Clear()
        {
            Lookup.Clear();
            entries.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Lookup).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)Lookup).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!((ICollection<KeyValuePair<TKey, TValue>>)Lookup).Remove(item))
                return false;

            RemoveEntries(item.Key);
            return true;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Lookup.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
            Add(item.Key, item.Value);

        public void OnBeforeSerialize()
        {
            // Entries are the serialized source of truth and are kept in sync by
            // every mutation exposed by this type.
        }

        public void OnAfterDeserialize()
        {
            entries ??= new List<Entry>();
            lookup = BuildLookup();
        }

        private Dictionary<TKey, TValue> BuildLookup()
        {
            var result = new Dictionary<TKey, TValue>();
            var uniqueEntries = new List<Entry>(entries?.Count ?? 0);

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.key is null || !result.TryAdd(entry.key, entry.value))
                        continue;

                    uniqueEntries.Add(entry);
                }
            }

            entries = uniqueEntries;
            return result;
        }

        private int FindEntryIndex(TKey key)
        {
            var comparer = EqualityComparer<TKey>.Default;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && comparer.Equals(entry.key, key))
                    return index;
            }

            return -1;
        }

        private void RemoveEntries(TKey key)
        {
            var comparer = EqualityComparer<TKey>.Default;
            entries.RemoveAll(entry => entry != null && comparer.Equals(entry.key, key));
        }

        private void RemoveDuplicateEntries(TKey key, int preservedIndex)
        {
            if (preservedIndex < 0)
                return;

            var comparer = EqualityComparer<TKey>.Default;
            for (var index = entries.Count - 1; index > preservedIndex; index--)
            {
                var entry = entries[index];
                if (entry != null && comparer.Equals(entry.key, key))
                    entries.RemoveAt(index);
            }
        }
    }
}
