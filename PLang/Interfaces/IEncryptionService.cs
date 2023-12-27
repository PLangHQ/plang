using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Interfaces
{
	public interface IEncryption
	{
		T Decrypt<T>(string data);
		string Encrypt(object data);
		void GenerateKey();
		string GetKeyHash();
	}
}
