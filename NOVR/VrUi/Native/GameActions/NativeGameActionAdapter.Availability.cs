using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeGameActionAdapter
{
    public bool IsTopLevelMainMenuAvailable
    {
        get
        {
            // NOVR suppresses native rendering through Canvas/CanvasGroup;
            // an inactive GameObject instead means the game has closed its UI.
            // Avoid enumerating that retained hierarchy every flight frame.
            if (_originalMainCanvas == null || !_originalMainCanvas.activeInHierarchy)
                return false;
            if (!_mainMenuActionLabels.TryGetValue(NativeGameAction.SinglePlayer, out var singlePlayerLabels))
                return false;

            var buttons = GetButtons(includeInactive: false);
            for (var index = 0; index < buttons.Length; index++)
            {
                if (IsTopLevelMainMenuButton(buttons[index]) && ButtonMatches(buttons[index], singlePlayerLabels))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private Button[] GetButtons(bool includeInactive)
    {
        return _originalMainCanvas != null
            ? _originalMainCanvas.GetComponentsInChildren<Button>(includeInactive)
            : new Button[0];
    }
}
