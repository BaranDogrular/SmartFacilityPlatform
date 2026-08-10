using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class AssetImportProfile(ImportProfileOptions options)
    : ConfiguredImportSourceProfile(ImportProfileKeys.Asset, options);
