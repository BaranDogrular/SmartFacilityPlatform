using SmartFacility.Application.Imports.Abstractions;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class ImportProfileCatalog(IEnumerable<IImportSourceProfile> profiles)
    : IImportProfileCatalog
{
    private readonly IReadOnlyDictionary<string, IImportSourceProfile> _profiles = profiles
        .ToDictionary(profile => profile.Key, StringComparer.OrdinalIgnoreCase);

    public IImportSourceProfile GetRequired(string profileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);

        return _profiles.GetValueOrDefault(profileKey)
            ?? throw new InvalidOperationException($"Import profile '{profileKey}' is not registered.");
    }
}
