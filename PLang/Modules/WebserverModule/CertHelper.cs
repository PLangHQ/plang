using PLang.Errors;
using PLang.Interfaces;
using System.Net;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static PLang.Modules.WebserverModule.Program;

namespace PLang.Modules.WebserverModule;

public interface ICertHelper
{
	(X509Certificate2? Certificate, IError? Error) GetOrCreateCert(CertInfo? certInfo = null);
}

public class CertHelper : ICertHelper
{
	private readonly IPLangFileSystem fileSystem;

	public CertHelper(IPLangFileSystem fileSystem)
	{
		this.fileSystem = fileSystem;
	}

	public (X509Certificate2?, IError?) GetOrCreateCert(CertInfo? certInfo = null)
	{
		if (certInfo != null)
		{
			string extension = fileSystem.Path.GetExtension(certInfo.FileName);
			if (extension != ".pfx") return (null, new Error("Certificate must end with .pfx"));

			if (!string.IsNullOrWhiteSpace(certInfo.FileName) && fileSystem.File.Exists(certInfo.FileName))
				return (X509CertificateLoader.LoadPkcs12(
					fileSystem.File.ReadAllBytes(certInfo.FileName), certInfo.Password, X509KeyStorageFlags.Exportable), null);
		}

		X509Certificate2? cert;
		if (certInfo == null)
		{
			cert = LoadCertFromStore("localhost");
			if (cert != null)
			{ 
				var now = DateTimeOffset.UtcNow;
				if (now >= cert.NotBefore && now <= cert.NotAfter)
				{
					if (cert != null) return (cert, null);
				}
			}
		}

		using var rsa = RSA.Create(2048);
		var req = new CertificateRequest("CN=localhost, O=PlangCert", rsa,
										 HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

		var san = new SubjectAlternativeNameBuilder();
		san.AddDnsName("localhost");
		san.AddIpAddress(IPAddress.Loopback);
		req.CertificateExtensions.Add(san.Build());                    

		req.CertificateExtensions.Add(
			new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
		req.CertificateExtensions.Add(
			new X509EnhancedKeyUsageExtension(
				new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));  

		var rawPfx = req.CreateSelfSigned(
						DateTimeOffset.UtcNow.AddDays(-1),
						DateTimeOffset.UtcNow.AddYears(2))
					 .Export(X509ContentType.Pfx);

		cert = X509CertificateLoader.LoadPkcs12(
					   rawPfx, password: null, X509KeyStorageFlags.Exportable);

		using (var my = new X509Store(StoreName.My, StoreLocation.CurrentUser))
		{ my.Open(OpenFlags.ReadWrite); my.Add(cert); }

		// 4️⃣  optional trust (CurrentUser\Root) – dev only
		if (true)
		{
			using var root = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
			root.Open(OpenFlags.ReadWrite);
			root.Add(cert);
		}

		return (cert, null);
	}


	X509Certificate2? LoadCertFromStore(string subjectName)
	{
		using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
		{
			store.Open(OpenFlags.ReadOnly);
			foreach (var c in store.Certificates.Find(
						 X509FindType.FindBySubjectName, "localhost", validOnly: false))
			{
				if (c.Extensions["2.5.29.17"] != null && c.HasPrivateKey &&
					DateTimeOffset.UtcNow <= c.NotAfter &&
					c.Subject.Contains($"O=PlangCert", StringComparison.OrdinalIgnoreCase))
					return c;
			}
		}
		return null;
	}
}


