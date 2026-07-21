// CompProperties_AAAAManagerJob.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux.AAAA.Core;

internal sealed class CompProperties_AAAAManagerJob : ManagerJobCompProperties
{
    public List<AAAAManagerJobCompFieldProperties> fields = [];

    public CompProperties_AAAAManagerJob()
    {
        compClass = typeof(AAAAManagerJobComp);
    }

    public override IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        foreach (var error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        foreach (var field in fields)
        {
            foreach (var error in field.ConfigErrors())
            {
                yield return error;
            }
        }

        // Check if any of the fields have the same jobAreaFieldName
        var duplicateJobAreaFieldNames = fields
            .GroupBy(f => f.jobAreaFieldName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicate in duplicateJobAreaFieldNames)
        {
            yield return $"Duplicate jobAreaFieldName found: '{duplicate}'.";
        }

        // Check if any of the fields have the same scribeLabel
        var duplicateScribeLabels = fields
            .GroupBy(f => f.scribeLabelPrefix)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicate in duplicateScribeLabels)
        {
            yield return $"Duplicate scribeLabel found: '{duplicate}'.";
        }
    }
}

internal sealed class AAAAManagerJobCompFieldProperties
{
    [NoTranslate]
    public string sectionColumn;

    [NoTranslate]
    public string section;

    [NoTranslate]
    public string scribeLabelPrefix;

    [NoTranslate]
    public string jobAreaFieldName;

    [NoTranslate]
    public string hideIfFieldWithNameIsFalse;

    [NoTranslate]
    public string hideIfFieldWithNameIsTrue;

    [NoTranslate]
    public string toggleLabelKey = "ColonyManagerRedux.AAAA.UseEvacuation";

    [NoTranslate]
    public string toggleTooltipKey = "ColonyManagerRedux.AAAA.UseEvacuation.Tip";

    public bool defaultValue;

    public IEnumerable<string> ConfigErrors()
    {
        if (string.IsNullOrEmpty(sectionColumn))
        {
            yield return "sectionColumn is not set.";
        }

        if (string.IsNullOrEmpty(section))
        {
            yield return "section is not set.";
        }

        if (string.IsNullOrEmpty(jobAreaFieldName))
        {
            yield return "jobAreaFieldName is not set.";
        }

        if (string.IsNullOrEmpty(scribeLabelPrefix))
        {
            yield return "scribeLabel is not set.";
        }
    }
}
