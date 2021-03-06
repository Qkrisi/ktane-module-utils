using System.Collections;
using System.Collections.Generic;

public abstract partial class KtaneModule
{
    protected class SkipRequeue
    {
        public readonly object Value;
        
        public SkipRequeue(object value)
        {
            Value = value;
        }
    }    

    private class CoroutineQueue
    {
        private Queue<CoroutineEntry> Entries = new Queue<CoroutineEntry>();

        public bool CoroutineRunning = false;

        public void CallNext()
        {
            if (CoroutineRunning) return;
            if (Entries.Count > 0)
            {
                CoroutineRunning = true;
                var entry = Entries.Dequeue();
                entry.Module.StartCoroutine(entry.Routine);
            }
            else CoroutineRunning = false;
        }

        public void QueueRoutine(CoroutineEntry entry, bool SkipCall = false)
        {
            Entries.Enqueue(entry);
            if (!SkipCall) CallNext();
        }

        public void QueueRoutines(KtaneModule module, bool SplitYields = false, params IEnumerator[] routines)
        {
            foreach (var routine in routines) QueueRoutine(new CoroutineEntry(routine, SplitYields, this, module));
        }

        public void Reset()
        {
            Entries.Clear();
            CoroutineRunning = false;
        }
    }

    private class CoroutineEntry
    {
        public readonly IEnumerator Routine;
        public readonly KtaneModule Module;

        private IEnumerator PatchRoutine(IEnumerator routine, bool Split, CoroutineQueue instance)
        {
            if (!Split) yield return routine;
            else
            {
                while (routine.MoveNext())
                {
                    var CurrentOBJ = routine.Current;
                    if (CurrentOBJ!=null && CurrentOBJ.GetType() == typeof(SkipRequeue)) yield return ((SkipRequeue)CurrentOBJ).Value;
                    else
                    {
                        yield return CurrentOBJ;
                        instance.QueueRoutine(new CoroutineEntry(routine, true, instance, Module), true);
                        break;
                    }
                }
            }
            instance.CoroutineRunning = false;
            instance.CallNext();
        }

        public CoroutineEntry(IEnumerator routine, bool Split, CoroutineQueue instance, KtaneModule module)
        {
            Module = module;
            Routine = PatchRoutine(routine, Split, instance);
        }
    }
}
