using System;
using System.Diagnostics;
using System.Reflection;

namespace Synapse.Handlers.Legacy.RequestValidator
{
	internal static class Utils
	{
		const string _lines = "--------------------------";

		public static double ElapsedSeconds(this Stopwatch stopwatch)
		{
			return TimeSpan.FromMilliseconds( stopwatch.ElapsedMilliseconds ).TotalSeconds;
		}

		public static string GetMessagePadLeft(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadLeft( width, '.' ), message );
		}

		public static string GetMessagePadRight(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadRight( width, '.' ), message );
		}

		public static string GetHeaderMessage(string header)
		{
			return string.Format( "{1}  {0}  {1}", header, _lines );
		}

		public static string GetBuildDateVersion()
		{
			Assembly assm = Assembly.GetExecutingAssembly();
			Version version = assm.GetName().Version;
			DateTime buildDateTime = new System.IO.FileInfo( assm.Location ).LastWriteTime;

			return string.Format( "Version: {0}, Build DateTime: {1}", version, buildDateTime );
		}
	}

	public class EventArgs<T> : EventArgs
	{
		public EventArgs() : base() { }
		public EventArgs(T data)
			: base()
		{
			Data = data;
		}
		public T Data { get; private set; }
	}
}