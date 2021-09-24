using System.Collections;
using System.Collections.Generic;

public class TradeTestQueue : IEnumerable<(int, int)>
{
    public IEnumerator<(int, int)> GetEnumerator()
    {
        while(!PhilosopherStone.Program.cancelled) 
            yield return TradeTest.GetNextTest();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}