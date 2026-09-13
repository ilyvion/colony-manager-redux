// Dialog_ManualGroupName.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class Dialog_ManualGroupName : Window
{
    private const int MaxNameLength = 28;

    private readonly string _title;
    private readonly IReadOnlyCollection<string> _existingGroups;
    private readonly string? _originalName;
    private readonly Action<string> _onNamed;

    private string _curName;
    private bool _focusedNameField;
    private int _startAcceptingInputAtFrame;

    public Dialog_ManualGroupName(
        string title,
        IReadOnlyCollection<string> existingGroups,
        string? currentName,
        Action<string> onNamed
    )
    {
        _title = title;
        _existingGroups = existingGroups;
        _originalName = currentName;
        _onNamed = onNamed;
        _curName = currentName ?? "";

        doCloseX = true;
        forcePause = true;
        closeOnAccept = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
    }

    private bool AcceptsInput => _startAcceptingInputAtFrame <= Time.frameCount;

    public override Vector2 InitialSize => new(280f, 175f);

    public void WasOpenedByHotkey() => _startAcceptingInputAtFrame = Time.frameCount + 1;

    /// <summary>
    /// Whether <paramref name="name"/> collides with one of <paramref name="existingGroups"/>,
    /// ignoring case - except when it's simply the name <paramref name="originalName"/> already
    /// has, which must not be rejected as a duplicate of itself. Kept free of GUI/game-state
    /// dependencies so it can be unit tested directly.
    /// </summary>
    internal static bool IsDuplicateGroupName(
        string name,
        string? originalName,
        IReadOnlyCollection<string> existingGroups
    ) =>
        !string.Equals(name, originalName, StringComparison.OrdinalIgnoreCase)
        && existingGroups.Contains(name, StringComparer.OrdinalIgnoreCase);

    private AcceptanceReport NameIsValid(string name) =>
        name.Length == 0 ? false
        : IsDuplicateGroupName(name, _originalName, _existingGroups)
            ? "ColonyManagerRedux.Overview.ManualGroup.NameInUse".Translate(name)
        : true;

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        var accept = false;
        if (
            Event.current.type == EventType.KeyDown
            && (
                Event.current.keyCode == KeyCode.Return
                || Event.current.keyCode == KeyCode.KeypadEnter
            )
        )
        {
            accept = true;
            Event.current.Use();
        }

        var rect = new Rect(inRect);
        Text.Font = GameFont.Medium;
        rect.height = Text.LineHeight + 10f;
        Widgets.Label(rect, _title);
        Text.Font = GameFont.Small;

        var text = Widgets_TextField.TextField(
            new Rect(0f, rect.height, inRect.width, 35f),
            _curName,
            "ManualGroupNameField",
            MaxNameLength
        );
        if (AcceptsInput)
        {
            _curName = text;
        }
        else
        {
            (
                (TextEditor)
                    GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl)
            ).SelectAll();
        }

        if (!_focusedNameField)
        {
            UI.FocusControl("ManualGroupNameField", this);
            _focusedNameField = true;
        }

        if (
            !(
                Widgets.ButtonText(
                    new Rect(15f, inRect.height - 35f - 10f, inRect.width - 15f - 15f, 35f),
                    "OK".Translate()
                ) || accept
            )
        )
        {
            return;
        }

        var report = NameIsValid(_curName);
        if (!report.Accepted)
        {
            Messages.Message(
                report.Reason.NullOrEmpty() ? "NameIsInvalid".Translate() : report.Reason,
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return;
        }

        _onNamed(_curName);
        _ = Find.WindowStack.TryRemove(this);
    }
}
