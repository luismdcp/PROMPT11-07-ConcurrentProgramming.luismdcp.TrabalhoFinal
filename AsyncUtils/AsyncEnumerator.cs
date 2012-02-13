using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskUtils;

namespace Async {
	
	public static class AsyncEnumerator
	{
		//
		// Executes asynchronously - but sequentially - all tasks returned by the specified enumerator.
		//
		public static Task Run(this IEnumerator<Task> source)
		{
			return run_internal<object>(source);
		}

		public static Task Run(this IEnumerable<Task> source)
		{
			return Run(source.GetEnumerator());
		}

		//
		// Same as above but allows the caller to fetch the result of the last task.
		//
		public static Task<T> Run<T>(this IEnumerator<Task> source)
		{
			return run_internal<T>(source);
		}

		public static Task<T> Run<T>(this IEnumerable<Task> source)
		{
			return Run<T>(source.GetEnumerator());
		}

		//
		// Same as above but allows for type inference.
		//

		public static Task<T> Run<T>(this IEnumerator<Task<T>> source)
		{
			return run_internal<T>(source);
		}

		private static Task<T> run_internal<T>(IEnumerator<Task> source)
		{
			var proxy = new TaskCompletionSource<T>();
			Action<Task> cont = null;

			cont = (t) =>
			{
				//Console.WriteLine("See if there is another Task!");
				if (!source.MoveNext())
				{
					//Console.WriteLine("End Iteration!");
					proxy.SetFromTask(t);
					return;
				}

				Task current = source.Current;
				if (current.Status == TaskStatus.Created)
				{
					//Console.WriteLine("Start next Task!");
					current.Start();
				}

				current.ContinueWith(cont);
			};
			cont(null);
			return proxy.Task;
		}
	}
 
}
