using Microsoft.Extensions.Logging;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using System.Net.WebSockets;

namespace PLang.Modules.MessageModule
{
	public class NostrClientManager
	{
		private NostrMultiWebsocketClient _client = null;

		public NostrMultiWebsocketClient GetClient(List<string> relayUrls)
		{
			if (_client != null) return _client;


			NostrWebsocketCommunicator[] relays = new NostrWebsocketCommunicator[relayUrls.Count];
			for (int i = 0; i < relayUrls.Count; i++)
			{
				relays[i] = new NostrWebsocketCommunicator(new Uri(relayUrls[i]));
			}

			var communicators = CreateCommunicators(relayUrls);
			ILogger<NostrWebsocketClient> nostrLogger = new Services.LoggerService.Logger<NostrWebsocketClient>();

			_client = new NostrMultiWebsocketClient(nostrLogger, communicators.ToArray());
			communicators.ForEach(x => x.Start());

			return _client;

		}
	
		private List<NostrWebsocketCommunicator> CreateCommunicators(List<string> relays)
		{
			var communicators = new List<NostrWebsocketCommunicator>();
			relays.ForEach(relay => communicators.Add(CreateCommunicator(new Uri(relay))));
			return communicators;
		}

		private NostrWebsocketCommunicator CreateCommunicator(Uri uri)
		{
			var comm = new NostrWebsocketCommunicator(uri, () =>
			{
				var client = new ClientWebSocket();
				client.Options.SetRequestHeader("Origin", "http://localhost");
				return client;
			});

			SetCommunicatorParam(comm, uri);

			return comm;
		}

		private void SetCommunicatorParam(NostrWebsocketCommunicator comm, Uri uri)
		{
			comm.Name = uri.Host;
			comm.ReconnectTimeout = TimeSpan.FromSeconds(30);
			comm.ErrorReconnectTimeout = TimeSpan.FromSeconds(60);


		}
	}
}
