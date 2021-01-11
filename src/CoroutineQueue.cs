using System.Collections;
using System.Collections.Generic;

internal class CoroutineQueue
{
    private readonly KtaneModule instance;
    private Queue<CoroutineEntry> Entries = new Queue<CoroutineEntry>();
    
    internal bool CoroutineRunning = false;

    internal void CallNext()
    {
        if (CoroutineRunning) return;
        if (Entries.Count > 0)
        {
            CoroutineRunning = true;
            instance.StartCoroutine(Entries.Dequeue().Routine);
        }
        else CoroutineRunning = false;
    }

    internal void QueueRoutine(CoroutineEntry entry, bool SkipCall = false)
    {
        Entries.Enqueue(entry);
        if(!SkipCall) CallNext();
    }

    internal void QueueRoutines(bool SplitYields = false, params IEnumerator[] routines)
    {
        foreach (var routine in routines) QueueRoutine(new CoroutineEntry(routine, SplitYields, this));
    }

    internal void Reset()
    {
        Entries.Clear();
        CoroutineRunning = false;
    }
        
    internal CoroutineQueue(KtaneModule inst)
    {
        instance = inst;
    }
}

internal class CoroutineEntry
{
    internal readonly IEnumerator Routine;

    private IEnumerator PatchRoutine(IEnumerator routine, bool Split, CoroutineQueue instance)
    {
        if (!Split) yield return routine;
        else if (routine.MoveNext())
        {
            yield return routine.Current;
            instance.QueueRoutine(new CoroutineEntry(routine, true, instance), true);
        }
        instance.CoroutineRunning = false;
        instance.CallNext();
    }
    
    internal CoroutineEntry(IEnumerator routine, bool Split, CoroutineQueue instance)
    {
        Routine = PatchRoutine(routine, Split, instance);
    }
}