using UnityEngine;
using UnityEngine.UIElements;


// TODO: make all Handlers public and get access o them like "ui.experimentTab.ConsoleInfo()"
public class UiHandler : ManagedMonoBehaviour
{
    private GameObject mainDisplayGameObject;
    private GameObject secondDisplayGameObject;

    private UiReferences _mainDisplayUiReferences;
    private UiReferences _secondDisplayUiReferences;

    private VisualElement _activeTabOnMainUiScreen;
    private VisualElement _activeTabOnSecondaryUiScreen;


    public UiReferences mainUiScreen;          // use this (instead of _mainDisplayUiReferences)
    public UiReferences secondaryUiScreen;     // use this (instead of _secondDisplayUiReferences)

    private bool _InDualDisplayMode;



    public override void ManagedAwake()
    {
        mainDisplayGameObject = GameObject.Find("GUI_main_monitor");
        secondDisplayGameObject = GameObject.Find("GUI_second_monitor");

        _mainDisplayUiReferences = mainDisplayGameObject.GetComponent<UiReferences>();
        _secondDisplayUiReferences = secondDisplayGameObject.GetComponent<UiReferences>();

        ApplyDefaultSettings();
    }

    public override void ManagedStart()
    {
        AddEventListeners();

        // default tabs to show (first secondary, than main (in case there is only one monitor))
        TabHasBeenClicked(secondaryUiScreen.elements.debugTab.tabBtn);
        TabHasBeenClicked(mainUiScreen.elements.experimentTab.tabBtn);

        mainUiScreen.root.Focus();  // test: remove autofocus from TextField's
    }





    private void ApplyDefaultSettings()
    {
        var dualDisplayModeFlag = false; // todo: read from config
        _InDualDisplayMode = dualDisplayModeFlag && Display.displays.Length > 1;
        Display.displays[0].Activate();         // activation of main display (without it window isn't fullscreen)


        if (_InDualDisplayMode)
        {
            mainUiScreen = _mainDisplayUiReferences;
            secondaryUiScreen = _secondDisplayUiReferences;
            Display.displays[1].Activate();     // activation of second display
        }
        else
        {
            mainUiScreen = _mainDisplayUiReferences;
            secondaryUiScreen = _mainDisplayUiReferences;

            secondDisplayGameObject.SetActive(false);
            GameObject.Find("SecondMonitorCamera").GetComponent<Camera>().enabled = false;
        }
    }

    private void AddEventListeners()
    {
        // HEADER TABS
        foreach (var tab in mainUiScreen.GetHeaderTabs())
        {
            tab.pickingMode = PickingMode.Position;
            tab.RegisterCallback<ClickEvent>(eventObj => { TabHasBeenClicked((VisualElement)eventObj.currentTarget); });
            // adds class to clicked elem (currentTarget, not just target!)
        }

        // EXIT / MINIMIZE GAME
        ((VisualElement)mainUiScreen.elements.window.modals.closeBtn).RegisterCallback<ClickEvent>(eventObj         => { ShowExitConfirmationModalWindow(); });
        ((VisualElement)mainUiScreen.elements.window.modals.minimizeBtn).RegisterCallback<ClickEvent>(eventObj      => { MinimizeGame(); });

        ((VisualElement)mainUiScreen.elements.window.modals.confirmCloseBtn).RegisterCallback<ClickEvent>(eventObj  => { ConfirmGameQuit(); });
        ((VisualElement)mainUiScreen.elements.window.modals.cancelCloseBtn).RegisterCallback<ClickEvent>(eventObj   => { CancelGameQuit(); });

        ((VisualElement)secondaryUiScreen.elements.window.modals.minimizeBtn).RegisterCallback<ClickEvent>(eventObj => { MinimizeSecondDisplay(); });
    }

    private void TabHasBeenClicked(VisualElement clickedTab)
    {
        if (_InDualDisplayMode) WinAPI.RestoreWindow("Unity Secondary Display");   // todo: maybe first of all minimize and then restore (if minimized not via code it doesn't count and woun't be restored)
        if (clickedTab == _activeTabOnMainUiScreen || clickedTab == _activeTabOnSecondaryUiScreen) return;


        if (_InDualDisplayMode)
        {
            if (clickedTab == mainUiScreen.GetElement("experiment-tab"))
            {
                OpenTabOnMainScreen(clickedTab);
                ShowBody(clickedTab.name, mainUiScreen);
            }
            else
            {
                OpeneTabOnSecondaryScreen(clickedTab);
                ShowBody(clickedTab.name, secondaryUiScreen);
            }
        }
        else // only one display
        {
            OpenTabOnMainScreen(clickedTab);
            ShowBody(clickedTab.name, mainUiScreen);
        }
    }

    private void OpenTabOnMainScreen(VisualElement clickedTab)
    {
        // Tab is "Active" if there is only one display
        if (_activeTabOnMainUiScreen != null) _activeTabOnMainUiScreen.RemoveFromClassList("isActive");
        _activeTabOnMainUiScreen = clickedTab;
        _activeTabOnMainUiScreen.AddToClassList("isActive");

        // todo: change classes names later
    }

    private void OpeneTabOnSecondaryScreen(VisualElement clickedTab)
    {
        // Tab is "Opened" if it is opened on second display
        if (_activeTabOnSecondaryUiScreen != null) _activeTabOnSecondaryUiScreen.RemoveFromClassList("isOpened");
        _activeTabOnSecondaryUiScreen = clickedTab;
        _activeTabOnSecondaryUiScreen.AddToClassList("isOpened");

        // todo: change classes names later
    }

    private void ShowBody(string tabName, UiReferences uiReferences)
    {
        foreach (var body in ((VisualElement)uiReferences.elements.window.body).Children())
        {
            body.style.display = DisplayStyle.None;
        }

        if (tabName == "") return;
        uiReferences.GetElement(uiReferences.GetTabBodyRelation(tabName)).style.display = DisplayStyle.Flex;    // todo: remake
    }


    // MODAL WINDOW

    private void CloseGame() {
        #if UNITY_EDITOR    // This code will be executed only in the editor

        UnityEditor.EditorApplication.isPlaying = false;

        #else   // This code will be executed in the assembled game

        Application.Quit();

        #endif
    }

    private void MinimizeGame() {
        // Firstly minimizes main display
        WinAPI.MinimizeGameWindow();

        // And after that -- second display (imitates click on second display btn. unity somehow knows which screen minimize, but can't minimize both automaticaly)
        var clickEvent = new ClickEvent();
        clickEvent.target = ((VisualElement)secondaryUiScreen.elements.window.modals.minimizeBtn);
        ((VisualElement)secondaryUiScreen.elements.window.modals.minimizeBtn).SendEvent(clickEvent);
    }

    private void MinimizeSecondDisplay() {
        WinAPI.MinimizeGameWindow();
    }

    private void ShowExitConfirmationModalWindow()
    {
        ((VisualElement)mainUiScreen.elements.window.modals.modalWindows).style.display = DisplayStyle.Flex;
        ((VisualElement)mainUiScreen.elements.window.modals.exitWindow).style.display = DisplayStyle.Flex;
    }

    private void CloseExitConfirmationModalWindow()
    {
        ((VisualElement)mainUiScreen.elements.window.modals.modalWindows).style.display = DisplayStyle.None;
        ((VisualElement)mainUiScreen.elements.window.modals.exitWindow).style.display = DisplayStyle.None;
    }

    private void ConfirmGameQuit()
    {
        CloseGame();
    }

    private void CancelGameQuit()
    {
        CloseExitConfirmationModalWindow();
    }
}