using System.Collections;
using System.Collections.Generic;

public abstract partial class KtaneModule
{
    private class CoroutineQueue
    {
        private readonly KtaneModule instance;
        private Queue<CoroutineEntry> Entries = new Queue<CoroutineEntry>();

        public bool CoroutineRunning = false;

        public void CallNext()
        {
            if (CoroutineRunning) return;
            if (Entries.Count > 0)
            {
                CoroutineRunning = true;
                instance.StartCoroutine(Entries.Dequeue().Routine);
            }
            else CoroutineRunning = false;
        }

        public void QueueRoutine(CoroutineEntry entry, bool SkipCall = false)
        {
            Entries.Enqueue(entry);
            if (!SkipCall) CallNext();
        }

        public void QueueRoutines(bool SplitYields = false, params IEnumerator[] routines)
        {
            foreach (var routine in routines) QueueRoutine(new CoroutineEntry(routine, SplitYields, this));
        }

        public void Reset()
        {
            Entries.Clear();
            CoroutineRunning = false;
        }

        public CoroutineQueue(KtaneModule inst)
        {
            instance = inst;
        }
    }

    private class CoroutineEntry
    {
        public readonly IEnumerator Routine;

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
}