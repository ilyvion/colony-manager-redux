// Dialog_GravshipJobConflict.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using Verse.Sound;

namespace ColonyManagerRedux;

/// <summary>
/// Prompts the player to choose which manager jobs to keep when a landing gravship's manager
/// database and the map it's landing on both already have manager jobs configured.
/// </summary>
internal sealed class Dialog_GravshipJobConflict(
    int localJobCount,
    int gravshipJobCount,
    Action keepLocalJobs,
    Action keepGravshipJobs,
    Action keepBothJobs,
    Action chooseIndividually
) : Window
{
    private const float ButtonWidth = 150f;
    private const float ButtonHeight = 40f;

    public override Vector2 InitialSize => new(660f, 220f);

    public override void PreOpen()
    {
        base.PreOpen();

        // The player must explicitly pick which jobs to keep; there's no sensible default to
        // fall back to if they dismiss the dialog without choosing.
        closeOnCancel = false;
        closeOnClickedOutside = false;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var y = 0f;

        Text.Font = GameFont.Medium;
        Widgets.Label(
            new Rect(0f, y, inRect.width, Text.LineHeight),
            "ColonyManagerRedux.Gravship.JobConflictTitle".Translate()
        );
        y += Text.LineHeight + 10f;
        Text.Font = GameFont.Small;

        var text = "ColonyManagerRedux.Gravship.JobConflictText".Translate(
            gravshipJobCount,
            localJobCount
        );
        var textHeight = Text.CalcHeight(text, inRect.width);
        Widgets.Label(new Rect(0f, y, inRect.width, textHeight), text);

        var buttonY = inRect.height - ButtonHeight;
        var totalButtonsWidth = (4 * ButtonWidth) + (3 * 8f);
        var buttonX = (inRect.width - totalButtonsWidth) / 2f;

        var keepLocalRect = new Rect(buttonX, buttonY, ButtonWidth, ButtonHeight);
        var keepBothRect = new Rect(buttonX + ButtonWidth + 8f, buttonY, ButtonWidth, ButtonHeight);
        var keepGravshipRect = new Rect(
            buttonX + (2 * (ButtonWidth + 8f)),
            buttonY,
            ButtonWidth,
            ButtonHeight
        );
        var chooseIndividuallyRect = new Rect(
            buttonX + (3 * (ButtonWidth + 8f)),
            buttonY,
            ButtonWidth,
            ButtonHeight
        );

        if (
            Widgets.ButtonText(
                keepLocalRect,
                "ColonyManagerRedux.Gravship.KeepLocalJobs".Translate()
            )
        )
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            keepLocalJobs();
            Close();
        }
        if (
            Widgets.ButtonText(keepBothRect, "ColonyManagerRedux.Gravship.KeepBothJobs".Translate())
        )
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            keepBothJobs();
            Close();
        }
        if (
            Widgets.ButtonText(
                keepGravshipRect,
                "ColonyManagerRedux.Gravship.KeepGravshipJobs".Translate()
            )
        )
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            keepGravshipJobs();
            Close();
        }
        if (
            Widgets.ButtonText(
                chooseIndividuallyRect,
                "ColonyManagerRedux.Gravship.ChooseIndividually".Translate()
            )
        )
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            chooseIndividually();
            Close();
        }
    }
}
#endif // !v1_5
