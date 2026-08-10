using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class WorkOrderImportProfile(ImportProfileOptions options)
    : ConfiguredImportSourceProfile(ImportProfileKeys.WorkOrder, options);
