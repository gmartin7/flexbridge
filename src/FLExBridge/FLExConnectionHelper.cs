﻿using System;
using System.ServiceModel;
using System.Threading;

namespace FLExBridge
{
	/// <summary>
	/// This class encapsulates the code related to service communication with FLEx.
	/// </summary>
	class FLExConnectionHelper : IDisposable
	{
		private ServiceHost host;
		private IFLExBridgeService pipe;
		/// <summary>
		/// Constructs the helper setting up the local service endpoint and opening
		/// </summary>
		internal FLExConnectionHelper()
		{
			bool hostOpened = true;
			try
			{
				host = new ServiceHost(typeof(FLExService),
									   new[] { new Uri("net.pipe://localhost/FLExEndpoint") });
				//open host ready for business
				host.AddServiceEndpoint(typeof(IFLExService), new NetNamedPipeBinding(), "FLExPipe");
				host.Open();
			}
			catch (Exception)
			{
				//There may be another copy of FLExBridge running, but we need to try and wakeup FLEx before we quit.
				hostOpened = false;
			}
			ChannelFactory<IFLExBridgeService> pipeFactory =
				new ChannelFactory<IFLExBridgeService>(new NetNamedPipeBinding(),
													   new EndpointAddress("net.pipe://localhost/FLExBridgeEndpoint/FLExPipe"));
			pipe = pipeFactory.CreateChannel();
			try
			{
				//Notify FLEx that we are ready to receive requests.
				//(if we failed to create the host we still want to do this so FLEx can wake up)
				pipe.BridgeReady();
			}
			catch(Exception)
			{
				Console.WriteLine("FLEx isn't listening.");
				pipe = null; //FLEx isn't listening.
			}
			if(!hostOpened)
			{
				throw new ApplicationException("FLExBridge already running.");
			}
		}

		/// <summary>
		/// Signals FLEx through 2 means that the bridge work has been completed.
		/// A direct message to FLEx if it is listening, and by allowing the BridgeWorkOngoing method to complete
		/// </summary>
		internal void SignalBridgeWorkComplete()
		{
			//wake up the BridgeWorkOngoing message and let it return to FLEx.
			//This is unnecessary except to avoid an exception on the FLEx side, just trying to be nice.
			Monitor.Enter(FLExService.waitObject);
			Monitor.PulseAll(FLExService.waitObject);
			Monitor.Exit(FLExService.waitObject);
			//open a channel to flex and send the message.
			try
			{
				if(pipe != null)
					pipe.BridgeWorkComplete(true);
			}
			catch (Exception)
			{
				Console.WriteLine("FLEx isn't listening.");//It isn't fatal if FLEx isn't listening to us.
			}
		}
		/// <summary>
		/// Interface for the service which FLEx implements
		/// </summary>
		[ServiceContract]
		private interface IFLExBridgeService
		{
			[OperationContract]
			void BridgeWorkComplete(bool changesReceived);

			[OperationContract]
			void BridgeReady();
		}

		/// <summary>
		/// Our service with methods that FLEx can call.
		/// </summary>
		[ServiceBehavior(UseSynchronizationContext = false)] //Create new threads for the services, don't tie them into the main UI thread.
		private class FLExService : IFLExService
		{
			static internal object waitObject = new object();
			static internal bool workComplete;
			public void BridgeWorkOngoing()
			{
				Monitor.Enter(waitObject);
				while(!workComplete)
				{
					try
					{
						Monitor.Wait(waitObject);
						workComplete = true;
					}
					catch (ThreadInterruptedException)
					{
						//this exception is known as a spurious interrupt, very rare, usually comes from bad hardware
						//doesn't mean we were done, so try and wait again
					}
					catch(Exception)
					{
						//all other exceptions we are considering an end of normal operation
						workComplete = true;
					}
				}
				Monitor.Exit(waitObject);
			}

		}

		[ServiceContract]
		private interface IFLExService
		{
			[OperationContract]
			void BridgeWorkOngoing();
		}

		public void Dispose()
		{
			host.Close();
		}
	}
}