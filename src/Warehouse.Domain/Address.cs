namespace Warehouse.Domain;

using System;

/// <summary>
/// Destination of a shipment. A value object: no identity, compared by value,
/// and validated once at construction so a Shipment can never hold a half-filled address.
/// </summary>
public sealed record Address
{
    public string Line1 { get; }
    public string City { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string line1, string city, string postalCode, string country)
    {
        Line1 = line1;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public static Address Create(string line1, string city, string postalCode, string country)
    {
        if (string.IsNullOrWhiteSpace(line1))
            throw new ArgumentException("Address line 1 is required.");

        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.");

        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required.");

        return new Address(line1.Trim(), city.Trim(), (postalCode ?? string.Empty).Trim(), country.Trim());
    }

    // Rehydrates from persisted columns without re-running creation invariants,
    // mirroring Product.Reconstruct.
    public static Address Reconstruct(string line1, string city, string postalCode, string country) =>
        new(line1, city, postalCode, country);

    public override string ToString() =>
        string.IsNullOrWhiteSpace(PostalCode)
            ? $"{Line1}, {City}, {Country}"
            : $"{Line1}, {City} {PostalCode}, {Country}";
}
