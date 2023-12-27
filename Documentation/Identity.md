# Understanding Built-in Identity in PLang

## What is Built-in Identity?

Built-in Identity in PLang offers a secure and private way of handling user identification, replacing traditional methods like email and password with a unique digital key.

## The Shift from Traditional Methods

Traditionally, identifying users online involved using email and password. PLang's Built-in Identity streamlines this, enhancing user privacy and security by using a unique digital ID.

## The Developer's Advantage

For developers, PLang's Built-in Identity system simplifies user authentication. There's no need to manage keys or complex processes; PLang handles everything, allowing developers to focus on building their application's core functionalities.

## In Summary

PLang's Built-in Identity provides a streamlined, secure experience for users and a hassle-free authentication system for developers.

## Want to Dig Deeper, Want to Program?

These examples show how you can use `%Identity%` in your PLang goal files to manage user interactions and data securely and efficiently.

### Examples

#### Managing User Data
```plang
- select balance from users where Identity=%Identity%, return 1 row
- write out %balance%
```
Securely fetch and manage user-specific data using the unique identity key.

#### Validating User Identity
You can add an event that runs before each goal to check if user is logged in.

Ensure legitimacy of user requests by verifying the presence of the unique identity key.

```plang
- if %Identity% is empty, give error "You need to sign your requests"
```

#### Access Control

Determine user access levels based on their unique identity.

```plang
- select accessLevel from userPermissions where Identity=%Identity%
- if %accessLevel% equals 'admin', call !GrantAdminAccess
```

#### Personalized User Experience
Tailor content based on user preferences linked to their unique identity.

```plang
- select preferences from userSettings where Identity=%Identity%
- customizeContent %preferences%
```

### C# - Advanced Programming

For developers interested in the technical workings of PLang's Built-in Identity, 
especially in C#, the following C# code snippets provide insight into how user requests 
are signed and verified. 

 
#### Signing Requests & Verifying Signatures
```csharp
public Dictionary<string, string> Sign(string data, string method, string url, string contract)
{
	var dict = new Dictionary<string, string>();
	DateTime created = SystemTime.UtcNow();
	string nonce = Guid.NewGuid().ToString();
	string dataToSign = StringHelper.CreateSignatureData(method, url, created.ToFileTimeUtc(), nonce, data, contract);

	var p = new Modules.BlockchainModule.Program(settings, context, null, null, null, null, null);
	string signedMessage = p.SignMessage(dataToSign).Result;
	string address = p.GetCurrentAddress().Result;

	dict.Add("X-Signature", signedMessage);
	dict.Add("X-Signature-Contract", contract);
	dict.Add("X-Signature-Created", created.ToFileTimeUtc().ToString());
	dict.Add("X-Signature-Nonce", nonce);
	dict.Add("X-Signature-Address", address);
	return dict;
}

public string VerifySignature(string body, string method, string url, Dictionary<string, string> validationHeaders)
{
	var signature = validationHeaders["X-Signature"];
	var created = validationHeaders["X-Signature-Created"];
	var nonce = validationHeaders["X-Signature-Nonce"];
	var address = validationHeaders["X-Signature-Address"];
	var contract = validationHeaders["X-Signature-Contract"] ?? "C0";

	string message = StringHelper.CreateSignatureData(method, url, long.Parse(created), nonce, body, contract);
	var p = new Modules.BlockchainModule.Program(settings, context, null, null, null, null, null);
	if (p.VerifySignature(message, signature, address).Result)
	{
		return address;
	}
	return null;
}
```


