using System;
using System.Collections.Generic;

[Serializable]
internal class CollectionWrapper<T>
{
    public List<T> items;
    public CollectionWrapper(List<T> source) => items = source;
    public CollectionWrapper() => items = [];
}