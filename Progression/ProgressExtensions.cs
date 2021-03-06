﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Progression.Core;
using Progression.ProgressCalculators;

namespace Progression
{
    [DebuggerNonUserCode]
    public static class ProgressExtensions
    {
        #region: WithProgress methods :

        // Note: These methods wrap an IEnumerable, monitoring progress as the sequence is enumerated.

        /// <summary> Tracks progress as the source is enumerated. </summary>
        /// <param name="source">The Count property will be used to calculate progress as items are enumerated.</param>
        public static ProgressEnumerator<T> WithProgress<T>(this ICollection<T> source)
        {
            return new ProgressEnumerator<T>(source, Progress.BeginFixedTask(source.Count));
        }

        /// <summary> Tracks progress as the source is enumerated. </summary>
        /// <param name="source"></param>
        /// <param name="sourceCount">Used to calculate progress as items are enumerated. If the count is unknown, use the "WithProgressUnknown" overload.</param>
        public static ProgressEnumerator<T> WithProgress<T>(this IEnumerable<T> source, int sourceCount)
        {
            return new ProgressEnumerator<T>(source, Progress.BeginFixedTask(sourceCount));
        }

        /// <summary> Tracks progress as the source is enumerated.
        /// Progress is calculated proportionally for each step.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="stepWeights">
        /// Determines the weight of each step.
        /// For example, the file sizes of a copy operation.
        /// </param>
        public static ProgressEnumerator<T> WithProgress<T>(this IEnumerable<T> source, float[] stepWeights)
        {
            return new ProgressEnumerator<T>(source, Progress.BeginWeightedTask(stepWeights));
        }

        /// <summary> Tracks progress as the source is enumerated.
        /// 
        /// Since the number of items is unknown,
        /// as tasks complete, the progress will get nearer completion,
        /// but will never reach 100%.
        /// </summary>
        /// <param name="source">Note: If the source is a Collection, then the Count will be used and the estimatedSteps will be ignored.</param>
        /// <param name="estimatedCount">
        /// Determines how "unknown" progress is calculated.  This should be a rough estimate of the number of steps expected.
        /// As steps are completed, progress gets closer to 100%, but never reaches it.
        /// </param>
        /// <param name="estimatedWeight">
        /// A value between 0.0 and 1.0 that determines how much weight to place on the estimated steps.
        /// For example, if estimatedSteps is 100 and estimatedWeight is .75,
        /// then when 100 steps have completed, progress will be at 75%.
        /// 
        /// This value cannot equal 0.0 or 1.0.
        /// </param>
        public static ProgressEnumerator<T> WithProgressUnknown<T>(this IEnumerable<T> source, int estimatedCount, float estimatedWeight)
        {
            // Just in case the source is a Collection or List, we can use the Count so that the task isn't "Unknown":
            var sourceCollection = source as ICollection<T>;
            return new ProgressEnumerator<T>(source, (sourceCollection != null) ? Progress.BeginFixedTask(sourceCollection.Count) : Progress.BeginUnknownTask(estimatedCount, estimatedWeight));
        }

        /// <summary> Tracks progress as the source is enumerated. </summary>
        /// <param name="source"></param>
        /// <param name="calculator">A custom progress calculator</param>
        public static ProgressEnumerator<T> WithProgress<T>(this IEnumerable<T> source, IProgressCalculator calculator)
        {
            return new ProgressEnumerator<T>(source, Progress.BeginCustomTask(calculator));
        }

        #endregion
    }

    /// <summary> Wraps an enumerable source, reporting progress as the source is enumerated. </summary>
    /// <remarks>
    /// It would have been way easier to just use the "yield return" feature,
    /// but this allows us to provide strongly-typed "chainable" methods
    /// such as SetCallback, SetMaxDepth, and SetTaskKey.
    /// </remarks>
    public class ProgressEnumerator<T> : IEnumerable<T>, IEnumerator<T>
    {
        public ProgressEnumerator(IEnumerable<T> source, ProgressTask progress)
        {
            this.source = source;
            this.progress = progress;
        }

        public override string ToString()
        {
            return progress.ToString();
        }
        
        #region: IEnumerable / IEnumerator Wrapper :

        private IEnumerable<T> source;
        private ProgressTask progress;

        private IEnumerator<T> sourceEnumerator;
        public IEnumerator<T> GetEnumerator()
        {
            sourceEnumerator = source.GetEnumerator();
            return this;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public bool MoveNext()
        {
            if (sourceEnumerator.MoveNext())
            {
                progress.NextStep();
                return true;
            }
            else
            {
                progress.EndTask();
                return false;
            }
        }

        public T Current { get { return this.sourceEnumerator.Current; } }
        object IEnumerator.Current { get { return this.sourceEnumerator.Current; } }

        public void Dispose()
        {
            sourceEnumerator.Dispose();
            progress.Dispose();
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        #endregion

        #region: Chainable Progress methods :

        /// <summary> Attaches the callback to fire when progress is reported.
        /// 
        /// This is usually called at the beginning of the task.
        /// Returns the current progress task, so that methods may be chained.
        /// </summary>
        /// <param name="callback">Attach a callback to the ProgressChanged event</param>
        public ProgressEnumerator<T> SetCallback(ProgressChangedCallback callback)
        {
            progress.SetCallback(callback);
            return this;
        }
        /// <summary> Attaches the callback to fire when progress is reported.
        /// 
        /// This is usually called at the beginning of the task.
        /// Returns the current progress task, so that methods may be chained.
        /// </summary>
        /// <param name="callback">Attach a callback to the ProgressChanged event</param>
        /// <param name="maxDepth"> An integer value that determines the maximum number of nested progress tasks. Progress reported at deeper levels will be ignored. All negative values are equivalent to "Auto". </param>
        public ProgressEnumerator<T> SetCallback(ProgressChangedCallback callback, ProgressDepth maxDepth)
        {
            progress.SetCallback(callback, maxDepth);
            return this;
        }
        /// <summary> Changes the current task's TaskKey. 
        /// Returns the current progress task, so that methods may be chained.
        /// </summary>
        /// <param name="newTaskKey">Identifies the task being performed.  Can be used for displaying progress.</param>
        public ProgressEnumerator<T> SetTaskKey(string newTaskKey)
        {
            progress.SetTaskKey(newTaskKey);
            return this;
        }
        /// <summary> Changes the current task's TaskKey. 
        /// Returns the current progress task, so that methods may be chained.
        /// </summary>
        /// <param name="newTaskKey">Identifies the task being performed.  Can be used for displaying progress.</param>
        /// <param name="newTaskArg">Provides additional info about the task being performed</param>
        public ProgressEnumerator<T> SetTaskKey(string newTaskKey, object newTaskArg)
        {
            progress.SetTaskKey(newTaskKey, newTaskArg);
            return this;
        }
        /// <summary> An integer value that determines the maximum number of nested progress tasks. Progress reported at deeper levels will be ignored. All negative values are equivalent to "Auto". 
        /// Returns the current progress task, so that methods may be chained.
        /// </summary>
        /// <param name="maxDepth"> An integer value that determines the maximum number of nested progress tasks. Progress reported at deeper levels will be ignored. All negative values are equivalent to "Auto". </param>
        public ProgressEnumerator<T> SetMaxDepth(ProgressDepth maxDepth)
        {
            progress.SetMaxDepth(maxDepth);
            return this;
        }

        #endregion

    }

}
