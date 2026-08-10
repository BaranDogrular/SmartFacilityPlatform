using SmartFacility.Application.Imports.Models;

namespace SmartFacility.Application.Imports.Profiles;

public sealed class ScadaAlarmImportProfile(ImportProfileOptions options)
    : ConfiguredImportSourceProfile(ImportProfileKeys.ScadaAlarm, options);
