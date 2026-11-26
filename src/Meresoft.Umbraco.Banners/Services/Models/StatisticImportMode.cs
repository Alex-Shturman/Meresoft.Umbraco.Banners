namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>What an import does with a row that already exists for the same day, banner and zone.</summary>
public enum StatisticImportMode
{
    /// <summary>Adds the imported counts to the stored ones.</summary>
    Add = 0,

    /// <summary>Overwrites the stored counts with the imported ones.</summary>
    Replace = 1,
}
