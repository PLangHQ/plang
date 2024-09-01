using MessagePack;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	internal class Formatter
	{
		public static byte[] ObjectToByteArray<T>(T obj)
		{
			return MessagePackSerializer.Serialize(obj);
		}

		public static T ByteArrayToObject<T>(byte[] byteArray)
		{
			return MessagePackSerializer.Deserialize<T>(byteArray);
		}
		public static byte[] ObjectToByteArray(object obj)
		{
			return MessagePackSerializer.Serialize(obj);
		}

		
	}
}
