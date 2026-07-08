using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CustomDataStructures
{
    /// <summary>
    /// Represents a thread-safe, observable concurrent queue.
    /// Allows enqueueing items from any thread and processes them in a separate thread.
    /// Enables additional processing through an event mechanism.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    public class ObservableConcurrentQueue<T> : ConcurrentQueue<T>
    {
        // Delegate for the primary item processing function.
        public delegate void ProcessItemDelegate(T item);
        private ProcessItemDelegate processItem;

        // Event for additional processing (like logging, debugging etc).
        public event Action<T> AdditionalProcessing;

        private SemaphoreSlim signal = new SemaphoreSlim(0);
        private Thread workerThread;
        private volatile bool isRunning = true;

        /// <param name="processItem">The main processing function for the queue items</param>
        public ObservableConcurrentQueue(ProcessItemDelegate processItem)
        {
            this.processItem = processItem ?? throw new ArgumentNullException(nameof(processItem));
            workerThread = new Thread(Worker);
            workerThread.Start();
        }


        public new void Enqueue(T item)
        {
            base.Enqueue(item);
            signal.Release();   // Signal the worker thread for processing (is ok even if called a few times before actual processing, will just do a few empty "while" cycles)
        }

        /// <summary>
        /// The worker method that runs in a separate thread to process queue items.
        /// </summary>
        private void Worker()
        {
            while (isRunning)
            {
                signal.Wait();                          // Wait for an item to be enqueued

                while (this.TryDequeue(out T item))
                {
                    processItem(item);                  // Main processing of the item
                    AdditionalProcessing?.Invoke(item); // Additional processing (e.g., logging)
                }
            }
        }

        /// <summary>
        /// Stops the processing thread, but ensures the worker thread finishes all tasks
        /// </summary>
        public void Stop()
        {
            isRunning = false;
            signal.Release();
        }

        /// <summary>
        /// Disposes the semaphore.
        /// </summary>
        public void Dispose()
        {
            signal.Dispose();
        }
    }
}
