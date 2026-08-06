namespace Warehouse.Domain;

using System;

public sealed record Address
{
    public string Line1 { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string line1, string city, string postalCode, string country) =>
        throw new NotImplementedException();

    public static Address Create(string line1, string city, string postalCode, string country) =>
        throw new NotImplementedException();

    public static Address Reconstruct(string line1, string city, string postalCode, string country) =>
        throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();
}
