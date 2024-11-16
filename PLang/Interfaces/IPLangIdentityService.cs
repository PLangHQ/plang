﻿namespace PLang.Interfaces;

public class Identity
{
    public Identity(string name, string identifier, object? value, bool isDefault = false)
    {
        Name = name;
        Identifier = identifier;
        Created = DateTime.Now;
        Value = value;
        IsDefault = isDefault;
    }

    public string Name { get; set; }
    public string Identifier { get; set; }
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime Created { get; set; }
    public object? Value { get; private set; }

    public void ClearValue()
    {
        Value = null;
    }
}

public interface IPLangIdentityService
{
    public Identity CreateIdentity(string name, bool setAsDefault = false);
    public Identity GetIdentity(string name);
    public Identity SetIdentity(string name);
    Identity GetCurrentIdentity();
    public Identity? ArchiveIdentity(string identifier);
    public IEnumerable<Identity> GetAllIdentities();
    public IEnumerable<Identity> GetIdentities();


    public Task<bool> Authenticate(Dictionary<string, string> keyValues);
    Identity GetCurrentIdentityWithPrivateKey();
    void UseSharedIdentity(string? appId = null);
}