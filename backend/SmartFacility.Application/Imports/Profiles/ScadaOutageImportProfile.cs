using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class ScadaOutageImportProfile(ImportProfileOptions options)
    : ConfiguredImportSourceProfile(ImportProfileKeys.ScadaOutage, options);
