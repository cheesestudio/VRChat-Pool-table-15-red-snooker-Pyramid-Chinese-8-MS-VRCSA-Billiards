#define EIJIS_SNOOKER15REDS
#define EIJIS_PYRAMID
#define EIJIS_CAROM
#define EIJIS_GUIDELINE2TOGGLE
#define EIJIS_PUSHOUT
#define EIJIS_CALLSHOT
#define EIJIS_SEMIAUTOCALL
#define EIJIS_10BALL

using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class MenuManager : UdonSharpBehaviour
{
    private readonly byte[] TIMER_VALUES = new byte[] { 0, 60, 45, 30, 15 };

    [SerializeField] private GameObject menuStart;
    [SerializeField] private GameObject menuJoinLeave;
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject menuLoad;
    [SerializeField] private GameObject menuOther;
    [SerializeField] private GameObject menuUndo;
    [SerializeField] private GameObject buttonSkipTurn;
    [SerializeField] private GameObject buttonSnookerUndo;
#if EIJIS_PUSHOUT
    [SerializeField] private GameObject buttonPushOut;
    [SerializeField] private Color buttonPushOutOnColor;
    private Color buttonPushOutOffColor;
#endif
#if EIJIS_CALLSHOT
    [SerializeField] private GameObject buttonCallLock;
    [SerializeField] private Color buttonCallLockOnColor;
    private Color buttonCallLockOffColor;
#endif
    [SerializeField] private TextMeshProUGUI[] lobbyNames;

    [SerializeField] private TextMeshProUGUI gameModeDisplay;
    [SerializeField] private TextMeshProUGUI timelimitDisplay;
    [SerializeField] private TextMeshProUGUI tableDisplay;
    [SerializeField] private TextMeshProUGUI physicsDisplay;

    private BilliardsModule table;

    private uint selectedTimer;
    private uint selectedTable;
    private uint selectedPhysics;

    private Vector3 joinMenuPosition;
    private Quaternion joinMenuRotation;
    private Vector3 joinMenuScale;
    public bool Initialized;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        if (!Initialized)
        {
            Initialized = true;
            Transform menuJoin = table.transform.Find("intl.menu/MenuAnchor/JoinMenu");
            if (menuJoin)
            {
                joinMenuPosition = menuJoin.localPosition;
                joinMenuRotation = menuJoin.localRotation;
                joinMenuScale = menuJoin.localScale;
            }
#if EIJIS_CAROM
            selectedTable = (uint)table.tableModelLocal;
#endif
#if EIJIS_PUSHOUT
            buttonPushOutOffColor = buttonPushOut.GetComponent<Image>().color;
#endif
#if EIJIS_CALLSHOT
            buttonCallLockOffColor = buttonCallLock.GetComponent<Image>().color;
#endif
        }

        _RefreshTimer();
        _RefreshPhysics();
        _RefreshTable();
        _RefreshToggleSettings();
        _RefreshLobby();
        _RefreshPlayerList();

        _DisableMenuJoinLeave();
        _DisableLobbyMenu();
        _DisableLoadMenu();
        _DisableSnookerUndoMenu();
        _DisableUndoMenu();
#if EIJIS_PUSHOUT
        _DisablePushOutMenu();
#endif
#if EIJIS_CALLSHOT
        _DisableCallLockMenu();
#endif
        _EnableStartMenu();

        cueSizeText.text = (cueSizeSlider.value / 10f).ToString("F1");
    }

    // public void _Tick()
    // {
    //     if (table.gameLive) return;
    // }

    public void _RefreshPlayerList()
    {
        int numPlayers = 0;
        int numPlayersOrange = 0;
        int numPlayersBlue = 0;
        for (int i = 0; i < 4; i++)
        {
            if (!table.teamsLocal && i > 1)
            {
                lobbyNames[i].text = string.Empty;
                continue;
            }
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(table.playerIDsLocal[i]);
            if (player == null)
            {
                lobbyNames[i].text = table._translations.Get("Free slot");
                //lobbyNames[i].text = "Free slot";
            }
            else
            {
                lobbyNames[i].text = table.graphicsManager._FormatName(player);
                numPlayers++;
                if (i % 2 == 0)
                    numPlayersOrange++;
                else
                    numPlayersBlue++;
            }
        }
        table.numPlayersCurrentOrange = numPlayersOrange;
        table.numPlayersCurrentBlue = numPlayersBlue;
        table.numPlayersCurrent = numPlayers;
    }

    public void _RefreshTimer()
    {
        int index = Array.IndexOf(TIMER_VALUES, (byte)table.timerLocal);
        selectedTimer = index == -1 ? 0 : (uint)index;
        if (index > -1)
        {
            if (TIMER_VALUES[index] == 0)
            {
                timelimitDisplay.text = table._translations.Get("No limit");
                //timelimitDisplay.text = "No limit";
            }
            else
            {
                timelimitDisplay.text = TIMER_VALUES[index].ToString("F0");
            }
        }
    }

    public void _RefreshGameMode()
    {
        string modeName = "";
        uint mode = (uint)table.GetProgramVariable("gameModeLocal");
        Transform selection = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/ModeSelection");
        Transform selectionPoint;
        switch (mode)
        {
            case 0:
                modeName = table.isChinese8Ball ? table._translations.Get("CN 8 Ball") : table._translations.Get("EN 8 Ball");
                //modeName = selectedTable == 2 ? "CN 8 Ball" : "EN 8 Ball";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/8ball");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 1:
                modeName = table._translations.Get("9 Ball");
                //modeName = "9 Ball";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/9ball");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 2:
                modeName = table._translations.Get("4 Ball JP");
                //modeName = "4 Ball JP";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/4ballJP");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 3:
                modeName = table._translations.Get("4 Ball KR");
                //modeName = "4 Ball KR";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/4ballKR");
                table.setTransform(selectionPoint, selection, true);
                break;
            case 4:
#if EIJIS_SNOOKER15REDS
                modeName = table._translations.Get("Snooker 15 Red");
                //modeName = "Snooker 15 Red";
#else
                modeName = "Snooker 6 Red";
#endif
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/6red");
                table.setTransform(selectionPoint, selection, true);
                break;
#if EIJIS_PYRAMID
            case BilliardsModule.GAMEMODE_PYRAMID:
                modeName = table._translations.Get("Russian Pyramid");
                //modeName = "Russian Pyramid";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/Pyramid");
                table.setTransform(selectionPoint, selection, true);
                break;
#endif
#if EIJIS_CAROM
            case BilliardsModule.GAMEMODE_3CUSHION:
                modeName = table._translations.Get("3-Cushion");
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/3Cushion");
                table.setTransform(selectionPoint, selection, true);
                break;
            case BilliardsModule.GAMEMODE_2CUSHION:
                modeName = table._translations.Get("2-Cushion");
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/2Cushion");
                table.setTransform(selectionPoint, selection, true);
                break;
            case BilliardsModule.GAMEMODE_1CUSHION:
                modeName = table._translations.Get("1-Cushion");
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/1Cushion");
                table.setTransform(selectionPoint, selection, true);
                break;
            case BilliardsModule.GAMEMODE_0CUSHION:
                modeName = table._translations.Get("0-Cushion");
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/0Cushion");
                table.setTransform(selectionPoint, selection, true);
                break;
#endif
#if EIJIS_10BALL
            case BilliardsModule.GAMEMODE_10BALL:
                modeName = table._translations.Get("10 Ball");
                //modeName = "10 Ball";
                selectionPoint = table.transform.Find("intl.menu/MenuAnchor/LobbyMenu/GameMode/SelectionPoints/10ball");
                table.setTransform(selectionPoint, selection, true);
                break;
#endif
        }
        gameModeDisplay.text = modeName;
    }
    public void _RefreshPhysics()
    {
        physicsDisplay.text = table._translations.Get((string)table.currentPhysicsManager.GetProgramVariable("PHYSICSNAME"));
    }

    public void _RefreshTable()
    {
        tableDisplay.text = table._translations.Get((string)table.tableModels[table.tableModelLocal].GetProgramVariable("TABLENAME")); // auto translate by cheese
    }

    public void _RefreshToggleSettings()
    {
        TeamsToggle_button.SetIsOnWithoutNotify(table.teamsLocal);
        GuidelineToggle_button.SetIsOnWithoutNotify(!table.noGuidelineLocal);
#if EIJIS_GUIDELINE2TOGGLE
        Guideline2Toggle_button.gameObject.SetActive(!table.noGuidelineLocal);
        Guideline2Toggle_button.SetIsOnWithoutNotify(!table.noGuideline2Local);
#endif
        LockingToggle_button.SetIsOnWithoutNotify(!table.noLockingLocal);
#if EIJIS_10BALL
        Wpa10BallRuleToggle_button.SetIsOnWithoutNotify(table.wpa10BallRuleLocal);
#endif
#if EIJIS_CALLSHOT
        RequireCallShotToggle_button.SetIsOnWithoutNotify(table.requireCallShotLocal);
#if EIJIS_SEMIAUTOCALL
        SemiAutoCallToggle_button.gameObject.SetActive(table.requireCallShotLocal);
        SemiAutoCallToggle_button.SetIsOnWithoutNotify(table.semiAutoCallLocal);
#endif
#endif
    }

    public void _RefreshLobby()
    {
        if (table.localPlayerDistant)
        { _DisableOtherMenu(); }
        else { _EnableOtherMenu(); }
        _RefreshToggleSettings();
        _RefreshPlayerList();
        _RefreshMenu();
    }

    public void _RefreshMenu()
    {
        if (table.localPlayerDistant)
        {
            _DisableLobbyMenu();
            _DisableStartMenu();
            _DisableLoadMenu();
            _DisableUndoMenu();
            _DisableMenuJoinLeave();
            return;
        }
        Transform table_base = table._GetTableBase().transform;
        Transform menu_Join = menuJoinLeave.transform;
        switch (table.gameStateLocal)
        {
            case 0://table idle
                _DisableLobbyMenu();
                _EnableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _DisableMenuJoinLeave();
                break;
            case 1://lobby
                if (table.isPlayer)
                    _EnableLobbyMenu();
                else
                    _DisableLobbyMenu();
                _DisableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _RefreshTeamJoinButtons();
                menu_Join.localPosition = joinMenuPosition;
                menu_Join.localRotation = joinMenuRotation;
                menu_Join.localScale = joinMenuScale;
                _EnableMenuJoinLeave();
                _RefreshTeamJoinButtons();
                break;
            case 2://game live
                _DisableLobbyMenu();
                _DisableStartMenu();
                _EnableLoadMenu();
                if (table.isPlayer)
                    _EnableUndoMenu();
                else
                    _DisableUndoMenu();
                Transform JOINMENU_SPOT = table_base.Find(".JOINMENU");
                if (JOINMENU_SPOT && menu_Join)
                    table.setTransform(JOINMENU_SPOT, menu_Join.transform);
                if (table.isBlueTeamFull && table.isOrangeTeamFull)
                {
                    if (table.isPlayer)
                    {
                        _EnableMenuJoinLeave();
                        _RefreshTeamJoinButtons();
                    }
                    else
                    {
                        _DisableMenuJoinLeave();
                    }
                }
                else
                {
                    _EnableMenuJoinLeave();
                    _RefreshTeamJoinButtons();
                }
                break;
            case 3://game ended/reset
                _DisableLobbyMenu();
                _EnableStartMenu();
                _DisableLoadMenu();
                _DisableUndoMenu();
                _DisableMenuJoinLeave();
                break;
        }
        Transform leave_Button = menu_Join.Find("LeaveButton");
        if (table.isPlayer)
            leave_Button.gameObject.SetActive(true);
        else
            leave_Button.gameObject.SetActive(false);
    }

    private void _RefreshTeamJoinButtons()
    {
        Transform join_Orange = menuJoinLeave.transform.Find("JoinOrange");
        Transform join_Blue = menuJoinLeave.transform.Find("JoinBlue");
        if (table.isOrangeTeamFull)
            join_Orange.gameObject.SetActive(false);
        else
            join_Orange.gameObject.SetActive(true);

        if (table.isBlueTeamFull)
            join_Blue.gameObject.SetActive(false);
        else
            join_Blue.gameObject.SetActive(true);
    }

    public void StartButton()
    {
        table._TriggerLobbyOpen();
    }
    public void JoinOrange()
    {
        table._TriggerJoinTeam(0);
    }
    public void JoinBlue()
    {
        table._TriggerJoinTeam(1);
    }
    public void LeaveButton()
    {
        table._TriggerLeaveLobby();
    }
    public void PlayButton()
    {
        table._TriggerGameStart();
    }
    public void Mode8Ball()
    {
        table._TriggerGameModeChanged(0);
    }
    public void Mode9Ball()
    {
        table._TriggerGameModeChanged(1);
    }
#if EIJIS_10BALL
    public void Mode10Ball()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_10BALL);
    }
#endif
    public void Mode4Ball()
    {
        table._TriggerGameModeChanged(2);
    }
    public void Mode4BallKR()
    {
        table._TriggerGameModeChanged(3);
    }
#if EIJIS_CAROM
    public void Mode3Cushion()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_3CUSHION);
    }
    public void Mode2Cushion()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_2CUSHION);
    }
    public void Mode1Cushion()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_1CUSHION);
    }
    public void Mode0Cushion()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_0CUSHION);
    }
#endif
    public void ModeSnooker6Red()
    {
        table._TriggerGameModeChanged(4);
    }
#if EIJIS_PYRAMID
    public void ModePyramid()
    {
        table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_PYRAMID);
    }
#endif
    [SerializeField] private Toggle TeamsToggle_button;
    public void TeamsToggle()
    {
        table._TriggerTeamsChanged(TeamsToggle_button.isOn);
    }
    [SerializeField] private Toggle GuidelineToggle_button;
    public void GuidelineToggle()
    {
        table._TriggerNoGuidelineChanged(!GuidelineToggle_button.isOn);
    }
#if EIJIS_GUIDELINE2TOGGLE
    [SerializeField] private Toggle Guideline2Toggle_button;
    public void Guideline2Toggle()
    {
        table._TriggerNoGuideline2Changed(!Guideline2Toggle_button.isOn);
    }
#endif
    [SerializeField] private Toggle LockingToggle_button;
    public void LockingToggle()
    {
        table._TriggerNoLockingChanged(!LockingToggle_button.isOn);
    }
#if EIJIS_10BALL
    [SerializeField] private Toggle Wpa10BallRuleToggle_button;
    public void Wpa10BallRuleToggle()
    {
        table._TriggerWpa10BallRuleChanged(Wpa10BallRuleToggle_button.isOn);
    }
#endif
#if EIJIS_CALLSHOT
    [SerializeField] private Toggle RequireCallShotToggle_button;
    public void RequireCallShotToggle()
    {
        table._TriggerRequireCallShotChanged(RequireCallShotToggle_button.isOn);
    }
#if EIJIS_SEMIAUTOCALL
    [SerializeField] private Toggle SemiAutoCallToggle_button;
    public void SemiAutoCallToggle()
    {
        table._TriggerSemiAutoCallChanged(SemiAutoCallToggle_button.isOn);
    }
#endif
#endif
    public void TimeRight()
    {
        if (selectedTimer > 0)
            selectedTimer--;
        else
            selectedTimer = 4;

        table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
    }
    public void TimeLeft()
    {
        if (selectedTimer < 4)
            selectedTimer++;
        else
            selectedTimer = 0;

        table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
    }
    public void TableRight()
    {
        if (selectedTable == table.tableModels.Length - 1)
            selectedTable = 0;
        else
            selectedTable++;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void TableLeft()
    {
        if (selectedTable == 0)
            selectedTable = (uint)table.tableModels.Length - 1;
        else
            selectedTable--;

        table._TriggerTableModelChanged(selectedTable);
    }
    public void PhysicsRight()
    {
        if (selectedPhysics == table.PhysicsManagers.Length - 1)
            selectedPhysics = 0;
        else
            selectedPhysics++;

        table._TriggerPhysicsChanged(selectedPhysics);
    }
    public void PhysicsLeft()
    {
        if (selectedPhysics == 0)
            selectedPhysics = (uint)table.PhysicsManagers.Length - 1;
        else
            selectedPhysics--;

        table._TriggerPhysicsChanged(selectedPhysics);
    }

    public Slider cueSmoothingSlider;
    public TextMeshProUGUI cueSmoothingText;
    public void setCueSmoothing()
    {
        float newSmoothing = cueSmoothingSlider.value / 10f;
        table.cueControllers[0].setSmoothing(newSmoothing);
        table.cueControllers[1].setSmoothing(newSmoothing);
        cueSmoothingText.text = newSmoothing.ToString("F1");
    }

    public Slider cueSizeSlider;
    public TextMeshProUGUI cueSizeText;
    public void setCueSize()
    {
        float newScale = cueSizeSlider.value / 10f;
        float newThickness = table.tableHook.cueThicknessSlider.value /10f;
        table.cueControllers[0].setScale(newScale, newThickness);
        table.cueControllers[1].setScale(newScale, newThickness);
        cueSizeText.text = newScale.ToString("F1");
    }

    [NonSerialized] public UIButton inButton;
    public void _OnButtonPressed() { onButtonPressed(inButton); }
    private void onButtonPressed(UIButton button)
    {
        if (button.name == "StartButton")
        {
            table._TriggerLobbyOpen();
        }
        else if (button.name == "JoinOrange")
        {
            table._TriggerJoinTeam(0);
        }
        else if (button.name == "JoinBlue")
        {
            table._TriggerJoinTeam(1);
        }
        else if (button.name == "LeaveButton")
        {
            table._TriggerLeaveLobby();
        }
        else if (table.localPlayerId > -1)
        {
            if (button.name == "PlayButton")
            {
                table._TriggerGameStart();
            }
            else if (button.name == "8Ball")
            {
                table._TriggerGameModeChanged(0);
            }
            else if (button.name == "9Ball")
            {
                table._TriggerGameModeChanged(1);
            }
#if EIJIS_10BALL
            else if (button.name == "10Ball")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_10BALL);
            }
#endif
            else if (button.name == "4Ball" || button.name == "4BallJP")
            {
                table._TriggerGameModeChanged(2);
            }
            else if (button.name == "4BallKR")
            {
                table._TriggerGameModeChanged(3);
            }
#if EIJIS_CAROM
            else if (button.name == "3Cushion")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_3CUSHION);
            }
            else if (button.name == "2Cushion")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_2CUSHION);
            }
            else if (button.name == "1Cushion")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_1CUSHION);
            }
            else if (button.name == "0Cushion")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_0CUSHION);
            }
#endif
            else if (button.name == "Snooker6Red")
            {
                table._TriggerGameModeChanged(4);
            }
#if EIJIS_PYRAMID
            else if (button.name == "Pyramid")
            {
                table._TriggerGameModeChanged(BilliardsModule.GAMEMODE_PYRAMID);
            }
#endif
            else if (button.name == "TeamsToggle")
            {
                table._TriggerTeamsChanged(button.toggleState);
            }
            else if (button.name == "GuidelineToggle")
            {
                table._TriggerNoGuidelineChanged(!button.toggleState);
            }
#if EIJIS_GUIDELINE2TOGGLE
            else if (button.name == "Guideline2Toggle")
            {
                table._TriggerNoGuideline2Changed(!button.toggleState);
            }
#endif
            else if (button.name == "LockingToggle")
            {
                table._TriggerNoLockingChanged(!button.toggleState);
            }
#if EIJIS_10BALL
            else if (button.name == "Wpa10BallRuleToggle")
            {
                table._TriggerWpa10BallRuleChanged(button.toggleState);
            }
#endif
#if EIJIS_CALLSHOT
            else if (button.name == "RequireCallShotToggle")
            {
                table._TriggerRequireCallShotChanged(button.toggleState);
            }
#if EIJIS_SEMIAUTOCALL
            else if (button.name == "SemiAutoCallToggle")
            {
                table._TriggerSemiAutoCallChanged(button.toggleState);
            }
#endif
#endif
            else if (button.name == "TimeRight")
            {
                if (selectedTimer > 0)
                {
                    selectedTimer--;

                    table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
                }
            }
            else if (button.name == "TimeLeft")
            {
                if (selectedTimer < 3)
                {
                    selectedTimer++;

                    table._TriggerTimerChanged(TIMER_VALUES[selectedTimer]);
                }
            }
            else if (button.name == "TableRight")
            {
                if (selectedTable == table.tableModels.Length - 1) { return; }
                selectedTable++;

                table._TriggerTableModelChanged(selectedTable);
            }
            else if (button.name == "TableLeft")
            {
                if (selectedTable == 0) { return; }
                selectedTable--;

                table._TriggerTableModelChanged(selectedTable);
            }
            else if (button.name == "PhysicsRight")
            {
                if (selectedPhysics == table.PhysicsManagers.Length - 1) { return; }
                {
                    selectedPhysics++;

                    table._TriggerPhysicsChanged(selectedPhysics);
                }
            }
            else if (button.name == "PhysicsLeft")
            {
                if (selectedPhysics == 0) { return; }
                {
                    selectedPhysics--;

                    table._TriggerPhysicsChanged(selectedPhysics);
                }
            }
        }
    }

    private void joinTeam(int id)
    {
        // Create new lobby
        if (!table.lobbyOpen)
        {
            table._TriggerLobbyOpen();
        }

        table._LogInfo("joining table on team " + id);

        if (table.localPlayerId == -1)
        {
            table._TriggerJoinTeam(id);
        }
    }

    public void _EnableLobbyMenu()
    {
        menuLobby.SetActive(true);
    }

    public void _DisableLobbyMenu()
    {
        menuLobby.SetActive(false);
    }

    public void _EnableStartMenu()
    {
        menuStart.SetActive(true);
    }

    public void _DisableStartMenu()
    {
        menuStart.SetActive(false);
    }

    public void _EnableLoadMenu()
    {
        menuLoad.SetActive(true);
    }

    public void _DisableLoadMenu()
    {
        menuLoad.SetActive(false);
    }

    public void _EnableOtherMenu()
    {
        menuOther.SetActive(true);
    }

    public void _DisableOtherMenu()
    {
        menuOther.SetActive(false);
    }

    public void _EnableUndoMenu()
    {
        menuUndo.SetActive(true);
    }

    public void _DisableUndoMenu()
    {
        menuUndo.SetActive(false);
    }

    public void _EnableSkipTurnMenu()
    {
        buttonSkipTurn.SetActive(true);
    }

    public void _DisableSkipTurnMenu()
    {
        buttonSkipTurn.SetActive(false);
    }
    public void _EnableSnookerUndoMenu()
    {
        buttonSnookerUndo.SetActive(true);
    }

    public void _DisableSnookerUndoMenu()
    {
        buttonSnookerUndo.SetActive(false);
    }

    public void _EnableMenuJoinLeave()
    {
        menuJoinLeave.SetActive(true);
    }

    public void _DisableMenuJoinLeave()
    {
        menuJoinLeave.SetActive(false);
    }
#if EIJIS_PUSHOUT
    
    public void _EnablePushOutMenu()
    {
        buttonPushOut.SetActive(true);
    }

    public void _DisablePushOutMenu()
    {
        buttonPushOut.SetActive(false);
    }
    
    public void _StateChangePushOutMenu(bool state)
    {
        buttonPushOut.GetComponent<Image>().color = state ? buttonPushOutOnColor : buttonPushOutOffColor;
    }
#endif
#if EIJIS_CALLSHOT
    
    public void _EnableCallLockMenu()
    {
        buttonCallLock.SetActive(true);
    }

    public void _DisableCallLockMenu()
    {
        buttonCallLock.SetActive(false);
    }

    public void _StateChangeCallLockMenu(bool state)
    {
        buttonCallLock.GetComponent<Image>().color = state ? buttonCallLockOnColor : buttonCallLockOffColor;
    }
#endif
}
