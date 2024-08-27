using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethereum.JsonRpc.Client;
using Nostr.Client.Client;
using Nostr.Client.Requests;
using Nostr.Client.Responses;
using NSubstitute;
using OpenQA.Selenium;
using PLang.Modules.MessageModule;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLangTests.Modules.MessageModule
{
	[TestClass]
	public class ProgramTests : BasePLangTest
	{
		Program p;
		INostrClient nostrClient;
		[TestInitialize]
		public void Init() {
			base.Initialize();


			var nostrKeys = new List<NostrKey>();
			settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(x =>
			{
				return nostrKeys;
			});
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>()))
				.Do(callInfo =>
				{
					nostrKeys = callInfo.Arg<List<NostrKey>>();
				});

			nostrClient = Substitute.For<INostrClient>();
			nostrClient.Streams.Returns(new NostrClientStreams());
			nostrClient.Streams.EventStream.Subscribe();

			p = new Program(settings, logger, pseudoRuntime, engine, llmServiceFactory, nostrClient, signingService, outputStreamFactory, 
				outputSystemStreamFactory, errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory, fileSystem);
			p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, llmServiceFactory, settings, null, null);
		}

		[TestMethod]	
		public async Task GetPublicKey_Test()
		{
			var publicKey = await p.GetPublicKey();
			Assert.IsNotNull(publicKey);
			Assert.IsTrue(publicKey.StartsWith("npub"));

		}

		[TestMethod]
		public async Task SetCurrentAccount_Test()
		{
			var nostrKeys = new List<NostrKey>();
			nostrKeys.Add(new NostrKey("Default", "nsec16wd0mtj8pxpp39c5u959zwqde4mn4e3cluruz0y3ukmw70aa5gsqvsx5dq", "539a392f4c5165811450b8829169da34c27e92becfe1706b8908f6b78d9c7b46", "npub12wdrjt6v29jcz9zshzpfz6w6xnp8ay47elshq6ufprmt0rvu0drqz77ead")
			{
				IsDefault = true
			});
			nostrKeys.Add(new NostrKey("Main", "nsec1ajkuf9da93nezxrphjz8mhhjajmcejunx6a5lrr6pgfr2lc8s0lsa0m72v", "0e219da61e976242d0d33d50f529b43dd03bb13823fcdb46a90d1e23c227257f", "npub1pcsemfs7ja3y95xn84g022d58hgrhvfcy07dk34fp50z8s38y4lsq62wf7"));

			settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(x =>
			{
				return nostrKeys;
			});
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>()))
				.Do(callInfo =>
				{
					nostrKeys = callInfo.Arg<List<NostrKey>>();
				});

			p = new Program(settings, logger, pseudoRuntime, engine, llmServiceFactory, null, signingService, outputStreamFactory, 
				outputSystemStreamFactory, errorHandlerFactory, errorSystemHandlerFactory, askUserHandlerFactory, fileSystem);
			p.Init(container, null, null, null, memoryStack, logger, context, typeHelper, null, settings, null, null);


			await p.SetCurrentAccount("Main");

			Assert.AreEqual(1, context[Program.CurrentAccountIdx]);
		}
		/*
		[TestMethod]
		public async Task Listen_Test()
		{


			await p.Listen("GoalName", "VariableName");

			nostrClient.Received(1).Send(Arg.Is<NostrRequest>(p => p.Subscription == "timeline:pubkey:follows"));
			//nostrClient.Streams.EventStream.ReceivedWithAnyArgs(1).Subscribe();
			int i = 0;
		}

		[TestMethod]
		public async Task SendPrivateMessage_Test() {

			var npubReceiverPublicKey = "npub1pcsemfs7ja3y95xn84g022d58hgrhvfcy07dk34fp50z8s38y4lsq62wf7";
			var content = "hello %year% plang world";

			memoryStack.Put("year", DateTime.Now.Year);

			await p.SendPrivateMessage(content, npubReceiverPublicKey);

			nostrClient.Received(1).Send(Arg.Any<NostrEventRequest>());
		}
		*/

	}
}
