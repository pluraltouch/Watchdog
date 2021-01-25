﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace WatchdogClient
{
    internal class Worker
    {
        private readonly TaskScheduler _callbackThread;

        public Worker() : this(CurrentTaskScheduler)
        {
        }

        public Worker(TaskScheduler callbackThread)
        {
            _callbackThread = callbackThread;
        }

        private static TaskScheduler CurrentTaskScheduler =>
            (SynchronizationContext.Current != null
                ? TaskScheduler.FromCurrentSynchronizationContext()
                : TaskScheduler.Default);

        public void DoWork(Action action)
        {
            new Task(DoWorkImpl, action, CancellationToken.None, TaskCreationOptions.LongRunning).Start();
        }

        private void DoWorkImpl(object oAction)
        {
            var action = (Action) oAction;
            try
            {
                action();
                Callback(Succeed);
            }
            catch (Exception e)
            {
                Callback(() => Fail(e));
            }
        }

        private void Succeed()
        {
            if (Succeeded != null)
            {
                Succeeded();
            }
        }

        private void Fail(Exception exception)
        {
            if (Error != null)
            {
                Error(exception);
            }
        }

        private void Callback(Action action)
        {
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, _callbackThread);
        }

        public event WorkerSucceededEventHandler Succeeded;
        public event WorkerExceptionEventHandler Error;
    }

    internal delegate void WorkerSucceededEventHandler();

    internal delegate void WorkerExceptionEventHandler(Exception exception);
}