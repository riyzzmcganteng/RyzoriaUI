namespace RyzoriaUI.Models;

public sealed record AndroidDevice(
    string Serial,
    string State,
    string Manufacturer,
    string Model,
    string DeviceCode,
    string AndroidVersion,
    string Sdk,
    string Battery,
    string Ram,
    string Storage,
    string Resolution,
    string Dpi,
    string ConnectionType,
    bool Authorized
);
