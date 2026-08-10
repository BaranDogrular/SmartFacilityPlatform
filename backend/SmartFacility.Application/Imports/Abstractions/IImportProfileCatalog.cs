namespace SmartFacility.Application.Imports.Abstractions;

public interface IImportProfileCatalog
{
    IImportSourceProfile GetRequired(string profileKey);
}
