using System.Collections.Generic;
using System.Collections;
using System;

public class ShalowList<T> : IEnumerable<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
{
    readonly IReadOnlyList<T> data;
    readonly int startIndex, count;

    public int Count { get => count; }

    //*****************************************************************************************

    public ShalowList(IReadOnlyList<T> iData, int iStartIndex, int iCount)
    {
        if(iCount < 0 || iStartIndex < 0)
            throw new ArgumentException("iCount < 0 || iStartIndex < 0");

        if(startIndex + iCount > iData.Count)
            throw new ArgumentException("startIndex + iCount > iData.Count");

        data = iData;
        startIndex = iStartIndex;
        count = iCount;
    }

    public T this[int index] { get => data[index+startIndex]; }

    //*****************************************************************************************

    public IEnumerator<T> GetEnumerator()
    {
        for(int i=startIndex; i<startIndex+count; i++)
            yield return data[i];
    }
 
    //*****************************************************************************************

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    //*****************************************************************************************
}