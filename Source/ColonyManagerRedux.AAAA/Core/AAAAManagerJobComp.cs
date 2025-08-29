using System.Text.RegularExpressions;
using seekiworks_AllowedAreaAutomaticAdapter;

namespace ColonyManagerRedux.AAAA.Core;

[HotSwappable]
internal sealed class AAAAManagerJobComp : ManagerJobComp
{
    private new CompProperties_AAAAManagerJob Props => (CompProperties_AAAAManagerJob)base.Props;
    private readonly List<AAAAManagerJobCompField> Fields = [];

    protected override void Initialize()
    {
        foreach (var fieldProps in Props.fields)
        {
            var field = new AAAAManagerJobCompField(Parent, fieldProps);
            field.Initialize();
            Fields.Add(field);
        }
    }

    protected override float RenderSectionPostfix(
        string sectionColumn,
        string section,
        ManagerJob job,
        Vector2 pos,
        float width
    )
    {
        var start = pos;
        foreach (var field in Fields)
        {
            field.RenderSectionPostfix(sectionColumn, section, ref pos, width);
        }
        return pos.y - start.y;
    }

    protected override void PostExposeData()
    {
        foreach (var field in Fields)
        {
            field.PostExposeData();
        }
    }

    public void AllowedAreaChangeDangerMode()
    {
        foreach (var field in Fields)
        {
            field.AllowedAreaChangeDangerMode();
        }
    }

    public void AllowedAreaChangeNormalMode()
    {
        foreach (var field in Fields)
        {
            field.AllowedAreaChangeNormalMode();
        }
    }
}

[HotSwappable]
internal sealed class AAAAManagerJobCompField(
    ManagerJob parent,
    AAAAManagerJobCompFieldProperties props
)
{
    private bool _useAAAAEvacuation;
    private List<Area?> _previousAreas = [];
    private bool _hasWarnedAboutNullAreas;

    public void Initialize() => _useAAAAEvacuation = props.defaultValue;

    public void RenderSectionPostfix(
        string sectionColumn,
        string section,
        ref Vector2 pos,
        float width
    )
    {
        if (
            !string.IsNullOrEmpty(props.hideIfFieldWithNameIsFalse)
            && GetJobBooleanField(props.hideIfFieldWithNameIsFalse) is FieldInfo booleanFieldFalse
        )
        {
            if (!(bool)booleanFieldFalse.GetValue(parent))
            {
                return;
            }
        }
        if (
            !string.IsNullOrEmpty(props.hideIfFieldWithNameIsTrue)
            && GetJobBooleanField(props.hideIfFieldWithNameIsTrue) is FieldInfo booleanFieldTrue
        )
        {
            if ((bool)booleanFieldTrue.GetValue(parent))
            {
                return;
            }
        }
        if (props.sectionColumn == sectionColumn && props.section == section)
        {
            DrawUseAAAAEvacuation(ref pos, width);
        }
    }

    private void DrawUseAAAAEvacuation(ref Vector2 pos, float width) =>
        Utilities.DrawToggle(
            ref pos,
            width,
            props.toggleLabelKey.Translate(),
            props.toggleTooltipKey.Translate(),
            ref _useAAAAEvacuation
        );

    private const string ScribeLabelInfix = "AAAAEvacuation";

    public void PostExposeData()
    {
        Scribe_Values.Look(
            ref _useAAAAEvacuation,
            props.scribeLabelPrefix + ScribeLabelInfix + "Enabled",
            defaultValue: props.defaultValue
        );
        Scribe_Collections.Look(
            ref _previousAreas,
            props.scribeLabelPrefix + ScribeLabelInfix + "PreviousAreas",
            LookMode.Reference
        );

        if (Scribe.mode == LoadSaveMode.PostLoadInit && _previousAreas == null)
        {
            _previousAreas = [];
        }
    }

    public void AllowedAreaChangeDangerMode()
    {
        if (!_useAAAAEvacuation)
        {
            return;
        }

        var jobAreaField = GetJobAreaField();
        if (jobAreaField == null)
        {
            return;
        }

        if (jobAreaField.FieldType == typeof(Area))
        {
            var previousArea = (Area?)jobAreaField.GetValue(parent);
            var safeArea = GetSafeAreaFor(previousArea);
            if (safeArea != null)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"[AAAAManagerJobComp] Setting job '{parent.Def.defName}' area from '{previousArea?.Label ?? "null"}' to '{safeArea.Label}'"
                );
                jobAreaField.SetValue(parent, safeArea);
            }
            else
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"[AAAAManagerJobComp] Could not find safe area for job '{parent.Def.defName}'"
                );
            }

            _previousAreas.Add(previousArea);
        }
        else if (typeof(ICollection<Area>).IsAssignableFrom(jobAreaField.FieldType))
        {
            var jobAreaCollection = (ICollection<Area?>?)jobAreaField.GetValue(parent);
            if (jobAreaCollection == null)
            {
                ColonyManagerReduxMod.Instance.LogWarningOnce(
                    $"[AAAAManagerJobComp] Job '{parent.Def.defName}''s {jobAreaField.Name} had a null previous areas value, we can't change the job area",
                    ref _hasWarnedAboutNullAreas
                );
                return;
            }

            _previousAreas.AddRange(jobAreaCollection);

            var safeAreas = jobAreaCollection
                .Select(originalArea =>
                {
                    Area? safeArea = GetSafeAreaFor(originalArea);
                    safeArea ??= originalArea; // if we can't find a safe area, use the original area
                    return safeArea;
                })
                .ToList();

            if (jobAreaCollection is Area?[] array)
            {
                // We can't clear an array, so we have to special-case it
                for (var i = 0; i < array.Length; i++)
                {
                    array[i] = safeAreas[i];
                }
            }
            else
            {
                try
                {
                    jobAreaCollection.Clear();
                    foreach (var safeArea in safeAreas)
                    {
                        jobAreaCollection.Add(safeArea);
                    }
                }
                catch (Exception ex)
                {
                    ColonyManagerReduxMod.Instance.LogException(
                        $"[AAAAManagerJobComp] Error while changing job area for '{parent.Def.defName}' using field {jobAreaField.Name}",
                        ex
                    );
                }
            }
        }
    }

    public void AllowedAreaChangeNormalMode()
    {
        if (!_useAAAAEvacuation)
        {
            return;
        }

        var jobAreaField = GetJobAreaField();
        if (jobAreaField == null)
        {
            return;
        }

        if (jobAreaField.FieldType == typeof(Area))
        {
            jobAreaField.SetValue(parent, _previousAreas[0]);
            _previousAreas.Clear();
        }
        else if (typeof(ICollection<Area>).IsAssignableFrom(jobAreaField.FieldType))
        {
            var jobAreaCollection = (ICollection<Area?>?)jobAreaField.GetValue(parent);
            if (jobAreaCollection == null)
            {
                ColonyManagerReduxMod.Instance.LogWarningOnce(
                    $"[AAAAManagerJobComp] Job '{parent.Def.defName}''s {jobAreaField.Name} had a null previous areas value, we can't change the job area",
                    ref _hasWarnedAboutNullAreas
                );
                return;
            }

            if (jobAreaCollection is Area?[] array)
            {
                // We can't clear an array, so we have to special-case it
                for (var i = 0; i < array.Length; i++)
                {
                    array[i] = _previousAreas[i];
                }
            }
            else
            {
                try
                {
                    jobAreaCollection.Clear();
                    foreach (var previousArea in _previousAreas)
                    {
                        jobAreaCollection.Add(previousArea);
                    }
                }
                catch (Exception ex)
                {
                    ColonyManagerReduxMod.Instance.LogException(
                        $"[AAAAManagerJobComp] Error while changing job area for '{parent.Def.defName}' using field {jobAreaField.Name}",
                        ex
                    );
                }
            }
            _previousAreas.Clear();
        }
    }

    private bool _hasReportedMissingJobAreaField;
    private bool _hasReportedInvalidJobAreaFieldType;

    private FieldInfo? GetJobAreaField()
    {
        // Handle danger mode activation
        var field = AccessTools.Field(parent.GetType(), props.jobAreaFieldName);

        if (field == null)
        {
            ColonyManagerReduxMod.Instance.LogErrorOnce(
                $"[AAAAManagerJobComp] Could not find field '{props.jobAreaFieldName}' on job '{parent.Def.defName}'",
                ref _hasReportedMissingJobAreaField
            );
            return null;
        }

        if (
            field.FieldType == typeof(Area)
            || typeof(ICollection<Area>).IsAssignableFrom(field.FieldType)
        )
        {
            return field;
        }

        ColonyManagerReduxMod.Instance.LogErrorOnce(
            $"[AAAAManagerJobComp] Field '{props.jobAreaFieldName}' on job '{parent.Def.defName}' "
                + $"is of type {field.FieldType.Name}, which is not a supported type. "
                + "Supported types are: Area, ICollection<Area>",
            ref _hasReportedInvalidJobAreaFieldType
        );

        return null;
    }

    private readonly Dictionary<string, Boxed<bool>> _hasReportedMissingBooleanField = [];
    private readonly Dictionary<string, Boxed<bool>> _hasReportedInvalidBooleanFieldType = [];

    private FieldInfo? GetJobBooleanField(string fieldName)
    {
        if (
            !_hasReportedMissingBooleanField.TryGetValue(
                fieldName,
                out var hasReportedMissingBooleanField
            )
        )
        {
            hasReportedMissingBooleanField = new Boxed<bool>();
            _hasReportedMissingBooleanField.Add(fieldName, hasReportedMissingBooleanField);
        }
        if (
            !_hasReportedInvalidBooleanFieldType.TryGetValue(
                fieldName,
                out var hasReportedInvalidBooleanFieldType
            )
        )
        {
            hasReportedInvalidBooleanFieldType = new Boxed<bool>();
            _hasReportedInvalidBooleanFieldType.Add(fieldName, hasReportedInvalidBooleanFieldType);
        }

        // Handle danger mode activation
        var field = AccessTools.Field(parent.GetType(), fieldName);

        if (field == null)
        {
            ColonyManagerReduxMod.Instance.LogErrorOnce(
                $"[AAAAManagerJobComp] Could not find field '{fieldName}' on job '{parent.Def.defName}'",
                ref hasReportedMissingBooleanField.RefValue
            );
            return null;
        }

        if (field.FieldType == typeof(bool))
        {
            return field;
        }

        ColonyManagerReduxMod.Instance.LogErrorOnce(
            $"[AAAAManagerJobComp] Field '{props.jobAreaFieldName}' on job '{parent.Def.defName}' "
                + $"is of type {field.FieldType.Name}, but this field expects a boolean value.",
            ref hasReportedInvalidBooleanFieldType.RefValue
        );

        return null;
    }

    private Area_Allowed? GetSafeAreaFor(Area? previousArea) =>
        parent
            .Manager.map.areaManager.AllAreas.OfType<Area_Allowed>()
            .FirstOrDefault(area =>
                Regex.IsMatch(
                    area.Label,
                    (previousArea == null ? "NoAreaAllowed".Translate() : previousArea.Label)
                        + Utility.GetSuffix,
                    RegexOptions.IgnoreCase | RegexOptions.ECMAScript
                )
            );
}
