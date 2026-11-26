using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.Text;

namespace PLang.Modules.IrohModule.Tests
{
	/// <summary>
	/// Two local endpoints without relay talking over loopback. These run the native iroh library, so a use after free
	/// or a double free shows up as the test process crashing.
	/// </summary>
	[TestClass]
	public class IrohTransportTests
	{
		private const string Alpn = "plang/test/1";
		private static readonly byte[] Answer = Encoding.UTF8.GetBytes("answer");

		private static IrohEndpoint Bind() => IrohEndpoint.Bind(IrohKeys.Generate().Secret, Alpn, useRelay: false);

		private static async Task Echo(IrohStream stream, IrohConnection connection)
		{
			await Task.Yield();
			var length = new byte[4];
			if (stream.ReadExactly(length, 4, 5000))
			{
				int size = (length[0] << 24) | (length[1] << 16) | (length[2] << 8) | length[3];
				stream.ReadExactly(new byte[size], size, 5000);
			}
			stream.WriteAndFinish(Answer);
		}

		[TestMethod]
		public void IsTicket_ShortTicketWithoutRelayIsATicket()
		{
			using var server = Bind();
			string ticket = server.Ticket();

			Assert.IsTrue(IrohEndpoint.IsTicket(ticket));
			Assert.IsFalse(IrohEndpoint.IsTicket(server.Id));
		}

		[TestMethod]
		public void Request_ManyOnOneConnection_EveryStreamIsReleased()
		{
			using var server = Bind();
			var serving = Task.Factory.StartNew(() => server.Serve(Echo), TaskCreationOptions.LongRunning);
			using var client = Bind();
			using var connection = client.Connect(server.Ticket());

			// More requests than the 100 concurrent streams a connection allows, so a stream the listener never frees blocks the 101st.
			var requests = Task.Run(() =>
			{
				for (int i = 0; i < 150; i++)
				{
					CollectionAssert.AreEqual(Answer, connection.Request(Encoding.UTF8.GetBytes("{}"), null, 5000, 5000, 1024));
				}
			});

			Assert.IsTrue(requests.Wait(TimeSpan.FromSeconds(30)), "requests stopped being answered");
			requests.GetAwaiter().GetResult();
		}

		[TestMethod]
		public void WaitUntilClosed_ThenRequestAndDispose_FailCleanly()
		{
			using var server = Bind();
			var accepting = Task.Run(server.Accept);
			using var client = Bind();
			var connection = client.Connect(server.Ticket());
			var serverConnection = accepting.Result!;

			var waiting = Task.Factory.StartNew(connection.WaitUntilClosed, TaskCreationOptions.LongRunning);
			Thread.Sleep(200);
			Assert.IsFalse(waiting.IsCompleted, "WaitUntilClosed returned while the connection was open");

			serverConnection.Dispose();
			Assert.IsTrue(waiting.Wait(TimeSpan.FromSeconds(10)));
			connection.Dispose();

			Assert.IsTrue(connection.IsDisposed);
			Assert.ThrowsException<IrohConnectionEndedException>(() => connection.Request(Encoding.UTF8.GetBytes("{}"), null, 1000, 1000, 1024));
			connection.Dispose();
		}

		[TestMethod]
		public void Dispose_WhileAnotherThreadWaitsUntilClosed_ClosesWithinSeconds()
		{
			using var server = Bind();
			var accepting = Task.Run(server.Accept);
			using var client = Bind();
			var connection = client.Connect(server.Ticket());
			using var serverConnection = accepting.Result!;

			var waiting = Task.Factory.StartNew(connection.WaitUntilClosed, TaskCreationOptions.LongRunning);
			Thread.Sleep(200);
			connection.Dispose();

			Assert.IsTrue(waiting.Wait(TimeSpan.FromSeconds(5)));
			Assert.IsNull(serverConnection.AcceptStream(), "the peer should see the connection closed");
		}

		[TestMethod]
		public void Dispose_WhileServeWaitsForAPeer_ReturnsAtOnceAndServeEndsWithTheNextPeer()
		{
			var server = Bind();
			string ticket = server.Ticket();
			var serving = Task.Factory.StartNew(() => server.Serve(Echo), TaskCreationOptions.LongRunning);
			Thread.Sleep(200);

			var watch = Stopwatch.StartNew();
			server.Dispose();
			Assert.IsTrue(watch.ElapsedMilliseconds < 1000, $"Dispose blocked for {watch.ElapsedMilliseconds} ms");
			Assert.IsTrue(server.IsClosed);
			Assert.IsNull(server.Accept());
			Assert.ThrowsException<IrohException>(() => server.Ticket());

			using var client = Bind();
			try { client.Connect(ticket).Dispose(); } catch (IrohException) { }
			Assert.IsTrue(serving.Wait(TimeSpan.FromSeconds(10)), "Serve did not end after the endpoint was closed");
			server.Dispose();
		}

		[TestMethod]
		public void Serve_WrongProtocolPeer_IsReportedAndGoodPeersAreStillServed()
		{
			using var server = Bind();
			int failures = 0;
			var serving = Task.Factory.StartNew(() => server.Serve(Echo, _ => Interlocked.Increment(ref failures)), TaskCreationOptions.LongRunning);
			string ticket = server.Ticket();

			using (var wrong = IrohEndpoint.Bind(IrohKeys.Generate().Secret, "other/1", useRelay: false))
			{
				Assert.ThrowsException<IrohException>(() => wrong.Connect(ticket));
			}

			using var client = Bind();
			using var connection = client.Connect(ticket);
			CollectionAssert.AreEqual(Answer, connection.Request(Encoding.UTF8.GetBytes("{}"), null, 5000, 5000, 1024));
			Assert.IsTrue(failures >= 1);
		}
	}
}
