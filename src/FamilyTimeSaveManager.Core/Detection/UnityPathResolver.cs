using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Detection;

/// <summary>
/// Unity resolves Application.persistentDataPath to %USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;,
/// which is not one of the standard Environment.SpecialFolder values, so it is composed here.
/// </summary>
public static class UnityPathResolver
{
    public const string DefaultCompany = "sgthale";
    public const string DefaultProduct = "Family Time";

    public static string PersistentDataPath(string? company, string? product)
    {
        var companyFolder = FileOps.SanitizeFileName(
            string.IsNullOrWhiteSpace(company) ? DefaultCompany : company,
            DefaultCompany);
        var productFolder = FileOps.SanitizeFileName(
            string.IsNullOrWhiteSpace(product) ? DefaultProduct : product,
            DefaultProduct);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            companyFolder,
            productFolder);
    }

    public static string PersistentDataPath(string? product = null) =>
        PersistentDataPath(DefaultCompany, product ?? DefaultProduct);
}
