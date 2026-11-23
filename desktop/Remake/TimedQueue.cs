// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace Remake {
 public static class TimedQueue {
  // A single serial loop: cancellation stops future requests; a failed write stops the queue.
  public static async Task Run<T>(IReadOnlyList<T> items, TimeSpan interval,
      Func<T,Task> execute, Action<int,T> waiting, CancellationToken cancellation) {
   if(interval<TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
   for(int i=0;i<items.Count;i++) {
    cancellation.ThrowIfCancellationRequested();
    waiting(i,items[i]);
    await Task.Delay(interval,cancellation);
    cancellation.ThrowIfCancellationRequested();
    await execute(items[i]);
   }
  }
 }
}
