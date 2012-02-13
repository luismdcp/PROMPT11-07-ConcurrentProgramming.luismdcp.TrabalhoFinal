using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Async
{
	public static class IOExtensions
	{
		// Streams
		public static Task WriteAsync(this Stream stream, byte[] buffer, int offset, int count)
		{

            return Task.Factory.FromAsync(stream.BeginWrite, (Action<IAsyncResult>)stream.EndWrite, buffer, offset, count, null);
            //return TaskBuilders.FromAsync(stream.BeginWrite, (Action<IAsyncResult>)stream.EndWrite, buffer, offset, count, null);
        }

		public static Task<int> ReadAsync(this Stream stream, byte[] buffer, int offset, int count)
		{
            return Task.Factory.FromAsync(stream.BeginRead, (Func<IAsyncResult, int>)stream.EndRead, buffer, offset, count, null);
            //return TaskBuilders.FromAsync(stream.BeginRead, (Func<IAsyncResult, int>)stream.EndRead, buffer, offset, count, null);
        }

		//WebClient
		public static Task<string> DownloadStringTaskAsync(this WebClient webClient, Uri address)
		{
			var tcs = new TaskCompletionSource<string>();
			webClient.DownloadStringCompleted += (o, ae) =>
			{
				if (ae.Cancelled)
					tcs.SetCanceled();
				else if (ae.Error != null)
					tcs.SetException(ae.Error);
				else
				{
					tcs.SetResult(ae.Result);
				}
			};
			webClient.DownloadStringAsync(address);
			return tcs.Task;
		}

		//WebRequest
		public static Task<WebResponse> WebRequestTaskAsync(this WebRequest wr)
		{
			return Task.Factory.FromAsync<WebResponse>(wr.BeginGetResponse, wr.EndGetResponse, null);
			//return TaskBuilders.FromAsync(wr.BeginGetResponse, wr.EndGetResponse);
		}
	}
}
