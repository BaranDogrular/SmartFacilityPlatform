using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class HistoricalWorkOrderImportProfile(ImportProfileOptions options)
    : ConfiguredImportSourceProfile(ImportProfileKeys.HistoricalWorkOrder, options);
