using System;

namespace Contract.UI
{
    public interface IUIService
    {
        void ShowHUD();
        void HideHUD();
        void ShowPanel(string panelName);
        void HidePanel(string panelName);
        void ShowObjective(string objectiveText);
        void HideObjective();
        void ShowNotification(string message, float duration = 3f);
        void ShowMissionCompleted(string missionName, int reward);
        void ShowVictory();
        void ShowGameOver();
    }
}