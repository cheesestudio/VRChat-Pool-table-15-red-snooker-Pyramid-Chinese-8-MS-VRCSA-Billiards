#define EIJIS_SNOOKER15REDS
#define NPC_GIZMOS  // 开启后在游戏内可视化NPC计算逻辑，关闭后不影响编译

using System;
using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PracticeManager : UdonSharpBehaviour
{
    private BilliardsModule table;

    [SerializeField] private TextMeshProUGUI testReportText; // assign in Inspector for test report display

    [Header("AI Opponent")]
    [HideInInspector] public string npcDisplayName = "Paruma"; // AI名字(key)，通过翻译系统自动本地化

    // 获取翻译后的NPC名字
    public string npcLocalizedName
    {
        get
        {
            if (table != null && table._translations != null)
                return table._translations.Get(npcDisplayName);
            return npcDisplayName;
        }
    }

    // --- Undo/Redo (existing) ---
    private object[] history = new object[128];
    private int currentPtr;
    private int latestPtr;

    // --- NPC AI State Machine ---
    private const int NPC_IDLE = 0;
    private const int NPC_CALCULATING = 1;
    private const int NPC_CHARGING = 2;
    private const int NPC_DELAYING = 3;
    private const int NPC_SHOOTING = 4;
    private const int NPC_OBSERVING = 5;
    private int npcState = NPC_IDLE;
    private float npcTimer;
    private float npcChargeDuration;
    private float npcChargeElapsed;
    private Vector3 npcAimDir;
    private float npcPower;
    private float npcSpinValue; // -1..1: negative=draw, 0=stun, positive=follow
    private bool npcBallPlaced; // prevent re-placing ball-in-hand on next idle tick
    private int npcTargetBall; // which ball NPC decided to shoot
    private int npcTargetPocket; // which pocket NPC decided to aim for
    private int npcShotType; // 0=直接 1=翻袋 2=K球 3=轻碰
    private float npcCutAngle; // 切角(度)
    private float npcBestScore; // 选球分数
    private int npcKickCushion = -1; // 勾库时实际选择的库边 (0=top,1=bottom,2=left,3=right)
    private int npcKickCushion2 = -1; // 两库勾库的第二个库边
    private Vector3 npcFirstImpactPos = Vector3.zero; // 预测的母球第一次撞击点
    private int npcGroupId = -1; // cached NPC group: 0=solids, 1=stripes, -1=uninitialized
    private int npcFrameDelay; // frames to wait after sim ends before NPC can fire (fixes AI-vs-AI timing bug)

    // --- Repeat shot detection (prevent infinite loops) ---
    private int _lastShotBall = -1;
    private int _lastShotPocket = -1;
    private int _repeatCount = 0;

    // --- Safety shot fail counter (break "无法击球" loop) ---
    private int _safetyFailCount = 0;

    // --- Corruption reset counter (prevent infinite corruption loop) ---
    private int _corruptionResetCount = 0;
    private const int MAX_CORRUPTION_RESETS = 5;

    // --- Failed break detection: all balls on right half → full power break ---
    private bool _isFirstNpcShot = true; // reset when game restarts
    private bool _gameWasLive = false; // track gameLive edge for reset
    private bool _restartScheduled = false; // prevent duplicate auto-restart in test mode

    // --- Test Mode ---
    public bool testMode; // public so BilliardsModule can check for turn bypass
    public bool debug; // debug模式：开启时不记录AI计算日记
    private bool _testModeStarted; // track if _StartTestMode already called
    private int testShotCount;
    private const int MAX_TEST_SHOTS = 500;
    private const int MAX_TEST_GAMES = 30;
    private int testGameCount;
    private int testOneShotClearCount;
    // Per-shot recorded data
    private uint[] testPocketedBefore = new uint[MAX_TEST_SHOTS];
    private uint[] testPocketedAfter = new uint[MAX_TEST_SHOTS];
    private int[] testTargetBall = new int[MAX_TEST_SHOTS];
    private int[] testPocketIdx = new int[MAX_TEST_SHOTS]; // 0-5 pocket index, -1=unknown
    private float[] testPower = new float[MAX_TEST_SHOTS];
    private float[] testSpin = new float[MAX_TEST_SHOTS];
    private Vector3[] testAimDir = new Vector3[MAX_TEST_SHOTS];
    private bool[] testFoul = new bool[MAX_TEST_SHOTS];
    private int[] testShotType = new int[MAX_TEST_SHOTS]; // 0=直接 1=翻袋 2=K球 3=轻碰
    private float[] testCutAngle = new float[MAX_TEST_SHOTS]; // 切角(度)
    private bool[] testIsFreeBall = new bool[MAX_TEST_SHOTS]; // 自由球
    private bool[] testTableOpen = new bool[MAX_TEST_SHOTS]; // 台面开放
    private int[] testNpcGroup = new int[MAX_TEST_SHOTS]; // NPC组别 -1=无 0=全色 1=花色
    private float[] testBestScore = new float[MAX_TEST_SHOTS]; // 选球分数
    private float[] testFirstImpactX = new float[MAX_TEST_SHOTS]; // 母球第一次撞击点X
    private float[] testFirstImpactZ = new float[MAX_TEST_SHOTS]; // 母球第一次撞击点Z
    // Snapshot of ball positions before shot (only 8 balls tracked for brevity: 0,1,2-9 solids,10-15 stripes)
    private string[] testSnapBefore = new string[MAX_TEST_SHOTS];
    private string[] testSnapAfter = new string[MAX_TEST_SHOTS];

    // --- Physics constants ---
    private const float BALL_RADIUS = 0.028575f;
    private const float BALL_DIAMETER = 0.05715f;
    private const float BALL_DIAMSQR = 0.003266f; // BALL_DIAMETER²
    private const float PATH_CLEARANCE = 0.062f; // BALL_DIAMETER + ~5mm margin
    private const float MIN_POWER = 0.32f;
    private const float MAX_POWER = 0.42f;

    // Debug helper: skip AI logs when debug=false
    private void _Log(string msg)
    {
        if (debug) table._LogInfo(msg);
    }

    [Header("NPC T-Point Offset")]
    [HideInInspector] public float cornerToSideOffset = 0.35f; // 角袋T点向中袋偏移比例 (0~1) — 运行时从ModelData读取

    // --- NPC pocket positions ---
    private Vector3[] npcPockets = new Vector3[6];
    private Vector3[] npcPocketsOriginal = new Vector3[6]; // before side offset

    // --- Position play output (replaces ref parameter, blocked in UdonSharp) ---
    private float _posPlaySpin;

    // ===================== INIT & TICK =====================

    public void _Init(BilliardsModule table_)
    {
        table = table_;
        _Clear();
    }

    [HideInInspector] public bool _npcNameDisplayed = false;

    public void _Tick()
    {
        // Auto-start test mode when testMode is set to true externally
        if (testMode && !_testModeStarted)
        {
            _testModeStarted = true;
            _StartTestMode();
        }
        if (!testMode) _testModeStarted = false;

        // Only the game joiner (orange team player) runs NPC calculations
        if (table.localPlayerId < 0) return;
        // Orange team = slots 0,2 → localPlayerId % 2 == 0
        if (table.localPlayerId % 2 != 0 && !testMode) return;

        if (table.npcEnabledLocal && table.is8Ball && (table.isPracticeMode || testMode) && (table.isOrangeTeamFull || testMode))
        {
            if (!_npcNameDisplayed)
            {
                _npcNameDisplayed = true;
                if (table.graphicsManager != null)
                    table.graphicsManager._SetNpcName(npcLocalizedName);
            }
            _NpcTick();
        }
        else
        {
            if (_npcNameDisplayed)
            {
                _npcNameDisplayed = false;
                if (table.graphicsManager != null)
                    table.graphicsManager._ClearNpcName();
            }
        }
    }

    // ===================== UNDO/REDO (existing, unchanged) =====================

    public void _Clear()
    {
        Array.Clear(history, 0, history.Length);
        currentPtr = 0;
        latestPtr = 0;
    }

    public void _Record()
    {
        int stateIdLocal = table.networkingManager.stateIdSynced;

        if (stateIdLocal == currentPtr) return;

        if (stateIdLocal < 0 || stateIdLocal >= 1024) return;

        currentPtr = stateIdLocal;

        if (currentPtr >= history.Length)
        {
            int newSize = history.Length * 2;
            if (newSize < currentPtr) newSize = currentPtr;

            object[] newHistory = new object[newSize];
            Array.Copy(history, newHistory, history.Length);
            history = newHistory;
        }

        object oldValue = history[currentPtr];
        object newValue = table._SerializeInMemoryState();

        history[currentPtr] = newValue;

        if (oldValue != null && !table._AreInMemoryStatesEqual((object[])oldValue, (object[])newValue))
        {
            latestPtr = currentPtr;
        }
        else if (stateIdLocal > latestPtr)
        {
            latestPtr = stateIdLocal;
        }

        _Log("recording state current=" + currentPtr + " latest=" + latestPtr);
    }

    public void _Undo()
    {
        if (!table.isPlayer) { return; }
        int newPtr = pop(false);
        if (newPtr == -1)
        {
            table._IndicateError();
            return;
        }
        load(newPtr);
    }

    public void _SnookerUndo()
    {
#if EIJIS_SNOOKER15REDS
        if (!table.isSnooker) { return; }
#else
        if (!table.isSnooker6Red) { return; }
#endif
        if (table.foulStateLocal == 0 || table.fourBallCueBallLocal == 0) { return; }
        if (!table.isMyTurn()) { return; }

        int newPtr = pop(true);
        if (newPtr == -1)
        {
            table._IndicateError();
            return;
        }
        load_SnookerUndo(currentPtr - newPtr);
    }

    public void _Redo()
    {
        if (!table.isPlayer) { return; }
        int newPtr = push();
        if (newPtr == -1)
        {
            table._IndicateError();
            return;
        }
        load(newPtr);
    }

    private int push()
    {
        int newPtr = currentPtr;
        while (newPtr < latestPtr)
        {
            newPtr++;
            if (history[newPtr] == null) continue;
            return newPtr;
        }
        return -1;
    }

    private int pop(bool snookerUndo)
    {
        int newPtr = currentPtr;
        while (newPtr > 0)
        {
            newPtr--;
            if (history[newPtr] == null) continue;
            object[] state = (object[])history[newPtr];
            if (snookerUndo)
            {
                if ((byte)state[4] == (byte)table.localTeamId)
                {
                    continue;
                }
            }
            if ((byte)state[9] == 0 || (byte)state[9] == 2)
            {
                return newPtr;
            }
        }
        return -1;
    }

    private void load_SnookerUndo(int amountBack)
    {
        if (table.isLocalSimulationRunning)
        {
            _Log("interrupting simulation and loading new state");
        }
        object[] state = (object[])history[currentPtr - amountBack];
        object[] curState = (object[])history[currentPtr];
        state[2] = curState[2];
        state[5] = (uint)6;
        state[6] = false;
        state[8] = curState[8];
        table._LoadInMemoryState(state, currentPtr + 1);
        table._IndicateSuccess();
    }

    private void load(int newPtr)
    {
        if (table.isLocalSimulationRunning)
        {
            _Log("interrupting simulation and loading new state");
        }
        object[] state = (object[])history[newPtr];
        table._LoadInMemoryState(state, newPtr);
        table._IndicateSuccess();
    }

    // ===================== TEST MODE =====================

    public void _StartTestMode()
    {
        testMode = true;
        testShotCount = 0;
        testGameCount = 0;
        testOneShotClearCount = 0;
        if (testReportText != null) testReportText.gameObject.SetActive(false);
        table._LogInfo("[TEST] 测试模式已启用, 自动开始游戏... 共" + MAX_TEST_GAMES + "局");
        // Auto-open lobby, join team, and start game
        if (!table.lobbyOpen)
        {
            table._TriggerLobbyOpen();
            SendCustomEventDelayedSeconds(nameof(_AutoJoinAndStart), 0.5f);
        }
        else if (table.isOrangeTeamFull)
        {
            table._TriggerGameStart();
        }
        else
        {
            _AutoJoinAndStart();
        }
    }

    public void _AutoJoinAndStart()
    {
        if (!testMode) return;
        if (!table.lobbyOpen)
        {
            table._TriggerLobbyOpen(); // StartButton
            SendCustomEventDelayedSeconds(nameof(_AutoJoinAndStart), 0.5f);
            return;
        }
        if (table.localPlayerId == -1)
        {
            table._TriggerJoinTeam(0); // join orange team
            SendCustomEventDelayedSeconds(nameof(_AutoJoinAndStart), 0.3f);
            return;
        }
        table._TriggerGameStart(); // PlayButton
        SendCustomEventDelayedSeconds(nameof(_AutoTryStart), 0.5f);
    }

    public void _AutoTryStart()
    {
        if (!testMode) return;
        if (table.gameLive) return; // already started
        table._TriggerGameStart(); // PlayButton retry
    }

    public void _AutoRestartGame()
    {
        if (!testMode) return;
        if (table.lobbyOpen)
        {
            if (table.localPlayerId == -1)
            {
                table._TriggerJoinTeam(0);
                SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.3f);
                return;
            }
            table._TriggerGameStart(); // PlayButton
            SendCustomEventDelayedSeconds(nameof(_AutoTryStart), 0.5f);
        }
        else
        {
            table._TriggerLobbyOpen(); // StartButton
            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.5f);
        }
    }

    public void _StopTestMode()
    {
        testMode = false;
        table._LogInfo("[TEST] 测试模式已停止, 共" + testGameCount + "局, " + testShotCount + "条射击数据, 一杆清台: " + testOneShotClearCount + "次");
        _DumpTestLog();
    }

    private string _SnapshotBalls()
    {
        // Record ALL 16 balls: ballIndex:x,z  or ballIndex:P(ocketed)
        string s = "";
        for (int i = 0; i <= 15; i++)
        {
            if ((table.ballsPocketedLocal & (1u << i)) != 0)
            {
                s += i + ":P ";
            }
            else
            {
                Vector3 p = table.ballsP[i];
                s += i + ":" + p.x.ToString("F3") + "," + p.z.ToString("F3") + " ";
            }
        }
        return s.Trim();
    }

    private void _RecordShotPre()
    {
        if (testShotCount >= MAX_TEST_SHOTS) return;
        testSnapBefore[testShotCount] = _SnapshotBalls();
        testPocketedBefore[testShotCount] = table.ballsPocketedLocal;
        testTargetBall[testShotCount] = npcTargetBall;
        testPocketIdx[testShotCount] = npcTargetPocket;
        testPower[testShotCount] = npcPower;
        testSpin[testShotCount] = npcSpinValue;
        testAimDir[testShotCount] = npcAimDir;
        testShotType[testShotCount] = npcShotType;
        testCutAngle[testShotCount] = npcCutAngle;
        testIsFreeBall[testShotCount] = table.isReposition;
        testTableOpen[testShotCount] = table.isTableOpenLocal;
        testNpcGroup[testShotCount] = npcGroupId;
        testBestScore[testShotCount] = npcBestScore;
        testFirstImpactX[testShotCount] = 0f; // will be filled in _RecordShotPost
        testFirstImpactZ[testShotCount] = 0f;
        testShotCount++;
    }

    private void _RecordShotPost(bool foul)
    {
        if (testShotCount <= 0) return;
        int idx = testShotCount - 1;
        testSnapAfter[idx] = _SnapshotBalls();
        testPocketedAfter[idx] = table.ballsPocketedLocal;
        testFoul[idx] = foul;
        // Record actual first impact position from physics
        testFirstImpactX[idx] = table.firstHitPos.x;
        testFirstImpactZ[idx] = table.firstHitPos.z;

        // Log this shot
        uint pBefore = testPocketedBefore[idx];
        uint pAfter = testPocketedAfter[idx];
        string pocketedStr = "";
        for (int b = 0; b <= 15; b++)
        {
            bool wasIn = (pBefore & (1u << b)) != 0;
            bool nowIn = (pAfter & (1u << b)) != 0;
            if (!wasIn && nowIn) pocketedStr += b + " ";
            if (wasIn && !nowIn) pocketedStr += b + "(出袋!) ";
        }
        string targetStatus = "";
        if (testTargetBall[idx] >= 0 && testTargetBall[idx] <= 15)
        {
            targetStatus = " 目标球前=" + ((pBefore & (1u << testTargetBall[idx])) != 0 ? "已进" : "台面")
                + " 目标球后=" + ((pAfter & (1u << testTargetBall[idx])) != 0 ? "已进" : "台面");
        }
        string typeStr = testShotType[idx] == 0 ? "直接" : (testShotType[idx] == 1 ? "翻袋" : (testShotType[idx] == 2 ? "K球" : "轻碰"));
        table._LogInfo("[TEST Shot " + idx + "] " + typeStr
            + " 目标球=" + testTargetBall[idx]
            + " 洞口=" + testPocketIdx[idx]
            + " 切角=" + testCutAngle[idx].ToString("F1") + "°"
            + " 力度=" + testPower[idx].ToString("F3")
            + " 旋转=" + testSpin[idx].ToString("F2")
            + " 分=" + testBestScore[idx].ToString("F2")
            + " 犯规=" + foul
            + (testIsFreeBall[idx] ? " 自由球" : "")
            + " 进袋=" + (pocketedStr.Length > 0 ? pocketedStr.Trim() : "无")
            + " 撞击=(" + testFirstImpactX[idx].ToString("F3") + "," + testFirstImpactZ[idx].ToString("F3") + ")"
            + targetStatus);
    }

    private void _DumpTestLog()
    {
        // Build full report string
        string report = "========== TEST REPORT ==========\n";
        report += "Ball IDs: 0=cue 1=8ball 2-9=solids 10-15=stripes\n";
        report += "Positions: x,z in table-local coords | P=pocketed\n";
        report += "Table: " + (table.k_TABLE_WIDTH * 2).ToString("F3") + " x " + (table.k_TABLE_HEIGHT * 2).ToString("F3") + "\n";
        report += "Total Shots: " + testShotCount + "\n";

        int madeCount = 0;
        int foulCount = 0;
        for (int i = 0; i < testShotCount; i++)
        {
            uint diff = testPocketedBefore[i] ^ testPocketedAfter[i];
            uint newIn = diff & testPocketedAfter[i];
            for (int b = 1; b <= 15; b++)
            {
                if ((newIn & (1u << b)) != 0) madeCount++;
            }
            if (testFoul[i]) foulCount++;
        }
        report += "Balls Pocketed: " + madeCount + "  Fouls: " + foulCount + "\n";
        report += "Final Table: " + _SnapshotBalls() + "\n";
        report += "---------------------------------\n";

        for (int i = 0; i < testShotCount; i++)
        {
            string typeStr = testShotType[i] == 0 ? "直接" : (testShotType[i] == 1 ? "翻袋" : (testShotType[i] == 2 ? "K球" : "轻碰"));
            string groupStr = testNpcGroup[i] == 0 ? "全色" : (testNpcGroup[i] == 1 ? "花色" : "无");
            report += "#" + i
                + " " + typeStr
                + " T=" + testTargetBall[i]
                + " P=" + testPocketIdx[i]
                + " cut=" + testCutAngle[i].ToString("F1") + "°"
                + " pow=" + testPower[i].ToString("F3")
                + " spin=" + testSpin[i].ToString("F2")
                + " foul=" + (testFoul[i] ? "1" : "0")
                + " score=" + testBestScore[i].ToString("F2")
                + (testIsFreeBall[i] ? " 自由球" : "")
                + " " + groupStr
                + " open=" + (testTableOpen[i] ? "1" : "0")
                + " aim=(" + testAimDir[i].x.ToString("F3") + "," + testAimDir[i].z.ToString("F3") + ")"
                + "\n  pre=[" + testSnapBefore[i] + "]\n"
                + "  post=[" + testSnapAfter[i] + "]\n";
        }
        report += "========== END ==========";

        // Log to console
        _Log(report);

        // Display on in-world UI panel
        if (testReportText != null)
        {
            testReportText.text = report;
            testReportText.gameObject.SetActive(true);
        }
    }

    // ===================== NPC AI =====================

    private void _NpcTick()
    {
        // Early exit: stop NPC immediately when game ends (prevents extra shot after game-over)
        if (testMode && npcState != NPC_IDLE && npcState != NPC_OBSERVING && !table.gameLive)
        {
            _Log("[NPC] 游戏已结束,停止NPC");
            _NpcStop();
            if (testMode)
            {
                testGameCount++;
                _corruptionResetCount = 0; // game ended normally, reset corruption counter
                bool oneShot = testShotCount <= 1;
                if (oneShot) testOneShotClearCount++;
                table._LogInfo("[TEST] 第" + testGameCount + "/" + MAX_TEST_GAMES + "局结束, 共" + testShotCount + "杆" + (oneShot ? " ★一杆清台!" : ""));
                testShotCount = 0;
                if (testGameCount >= MAX_TEST_GAMES)
                {
                    table._LogInfo("[TEST] 完成" + MAX_TEST_GAMES + "局! 一杆清台次数: " + testOneShotClearCount);
                    _StopTestMode();
                }
                else
                {
                    _restartScheduled = true;
                    SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 1.0f);
                }
            }
            return;
        }

        // Reset NPC group when game restarts (new game detected: gameLive=true but no shots yet)
        if (npcState == NPC_IDLE && table.gameLive && testMode && testShotCount == 0)
        {
            // If game restarted after corruption reset, check retry limit
            if (_corruptionResetCount > 0)
            {
                if (_corruptionResetCount >= MAX_CORRUPTION_RESETS)
                {
                    _Log("[NPC] 腐败重置次数超限(" + _corruptionResetCount + "),停止测试");
                    _StopTestMode();
                    return;
                }
                _Log("[NPC] 腐败重置后重新开始 (第" + _corruptionResetCount + "/" + MAX_CORRUPTION_RESETS + "次)");
            }
            _restartScheduled = false;
            npcGroupId = -1;
            _lastShotBall = -1;
            _lastShotPocket = -1;
            _repeatCount = 0;
            _safetyFailCount = 0;
            _isFirstNpcShot = true;
        }

        // Reset first shot flag when game starts (for normal practice mode, not just test mode)
        if (npcState == NPC_IDLE && table.gameLive && !_gameWasLive)
        {
            _isFirstNpcShot = true;
        }
        _gameWasLive = table.gameLive;

        switch (npcState)
        {
            case NPC_IDLE:
                // Handle game end in test mode — NPC might already be idle when gameLive flips
                // (e.g. NPC_OBSERVING transitioned to IDLE before _FlushBuffer() processed game-end sync)
                if (testMode && !table.gameLive && !_restartScheduled)
                {
                    _Log("[NPC] 游戏已结束(Idle检测),停止NPC");
                    testGameCount++;
                    _corruptionResetCount = 0;
                    bool oneShot = testShotCount <= 1;
                    if (oneShot) testOneShotClearCount++;
                    table._LogInfo("[TEST] 第" + testGameCount + "/" + MAX_TEST_GAMES + "局结束, 共" + testShotCount + "杆" + (oneShot ? " ★一杆清台!" : ""));
                    testShotCount = 0;
                    if (testGameCount >= MAX_TEST_GAMES)
                    {
                        table._LogInfo("[TEST] 完成" + MAX_TEST_GAMES + "局! 一杆清台次数: " + testOneShotClearCount);
                        _StopTestMode();
                    }
                    else
                    {
                        _restartScheduled = true;
                        SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 1.0f);
                    }
                    break;
                }
                // Wait one frame after sim ends so _FlushBuffer() can sync teamIdLocal
                if (npcFrameDelay > 0)
                {
                    npcFrameDelay--;
                    break;
                }
                if (npcTimer > 0f)
                {
                    npcTimer -= Time.deltaTime;
                    break;
                }
                // Don't start new calculation if game is already over
                if (testMode && !table.gameLive) break;

                // Detect and fix corrupted ballsPocketedLocal at game start
                // If all balls 2-15 appear pocketed but game is live, the sync state is wrong
                if (table.gameLive && (table.ballsPocketedLocal & 0xFFFCu) == 0xFFFCu)
                {
                    _Log("[NPC] 检测到ballsPocketedLocal异常(0x" + table.ballsPocketedLocal.ToString("X8") + "),尝试修正...");
                    // Scan actual ball positions to determine true pocketed state
                    uint corrected = 0x3u; // balls 0,1 always "pocketed" in bitmask sense
                    int actuallyOnTable = 0;
                    for (int i = 2; i <= 15; i++)
                    {
                        Vector3 p = table.ballsP[i];
                        // Ball is pocketed if position is far outside table bounds in x,z OR y
                        // (Chinese 8-ball rack places pocketed balls at y=5,0,-5,...)
                        if (Mathf.Abs(p.x) > table.k_TABLE_WIDTH + 0.2f
                            || Mathf.Abs(p.z) > table.k_TABLE_HEIGHT + 0.2f
                            || Mathf.Abs(p.y) > 1.0f)
                        {
                            corrected |= (1u << i);
                        }
                        else
                        {
                            actuallyOnTable++;
                        }
                    }
                    // Only fix if at least 2 balls are genuinely on the table (not on rack)
                    // Single ball on table could be 8-ball, which is correct
                    if (actuallyOnTable >= 2 || corrected == table.ballsPocketedLocal)
                    {
                        if (corrected != table.ballsPocketedLocal)
                        {
                            _Log("[NPC] 修正ballsPocketedLocal: 0x" + table.ballsPocketedLocal.ToString("X8") + " → 0x" + corrected.ToString("X8") + " 台面球数=" + actuallyOnTable);
                            table.ballsPocketedLocal = corrected;
                        }
                        else
                        {
                            _Log("[NPC] ballsPocketedLocal与实际球位一致,无需修正 台面球数=" + actuallyOnTable);
                        }
                    }
                    else
                    {
                        _Log("[NPC] 只有" + actuallyOnTable + "个球在台面(可能是8-ball或架球位),跳过修正");
                    }
                }

                // Detect ball position corruption: if any object ball is outside table bounds,
                // the physics simulation has corrupted the game state.
                // Bitmask fix alone is not enough — ball positions must also be valid.
                if (table.gameLive)
                {
                    bool positionsCorrupted = false;
                    for (int i = 2; i <= 15; i++)
                    {
                        if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
                        Vector3 p = table.ballsP[i];
                        if (Mathf.Abs(p.x) > table.k_TABLE_WIDTH + 0.2f
                            || Mathf.Abs(p.z) > table.k_TABLE_HEIGHT + 0.2f
                            || Mathf.Abs(p.y) > 2.0f)
                        {
                            positionsCorrupted = true;
                            break;
                        }
                    }
                    if (positionsCorrupted)
                    {
                        if (testMode)
                        {
                            _corruptionResetCount++;
                            _Log("[NPC] 检测到球位置腐败(球在台面外),重置游戏... (第" + _corruptionResetCount + "/" + MAX_CORRUPTION_RESETS + "次)");
                            _safetyFailCount = 0;
                            _repeatCount = 0;
                            _lastShotBall = -1;
                            _lastShotPocket = -1;
                            table._TriggerGameReset();
                            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 1.0f);
                        }
                        else
                        {
                            _Log("[NPC] 检测到球位置腐败,跳过本回合");
                            _safetyFailCount = 0;
                        }
                        break;
                    }
                }

                if (!table.isLocalSimulationRunning && (table.teamIdLocal == 1 || testMode))
                {
                    // Ball-in-hand: place cue ball optimally before shooting
                    if (table.isReposition && !npcBallPlaced)
                    {
                        _NpcPlaceCueBall();
                        npcBallPlaced = true;
                        npcTimer = 0.3f;
                        break;
                    }
                    // After placing ball, isReposition may still be true (network delay)
                    // Skip placement check and proceed to calculate shot

                    // Failed break detection: all balls on right half → full power break
                    if (_isFirstNpcShot && _IsAllBallsOnRightHalf())
                    {
                        _isFirstNpcShot = false;
                        _Log("[NPC] 检测到玩家开球失败(15球全在右半边),满力开球");
                        // Constrain cue ball to kitchen area (left quarter)
                        float kitchenLine = -table.k_TABLE_WIDTH * 0.5f;
                        if (table.ballsP[0].x > kitchenLine)
                        {
                            table.ballsP[0] = new Vector3(kitchenLine, table.ballsP[0].y, table.ballsP[0].z);
                            table._TriggerPlaceBall(0);
                            _Log("[NPC] 开球:白球移至开球线 x=" + kitchenLine.ToString("F3"));
                        }
                        npcTargetBall = -1;
                        npcTargetPocket = -1;
                        npcShotType = 0;
                        npcCutAngle = 0f;
                        npcBestScore = 0f;
                        // Aim at center of rack (ball 8, the middle of the triangle)
                        Vector3 rackCenter = table.ballsP[8];
                        Vector3 cuePos = table.ballsP[0];
                        npcAimDir = (rackCenter - cuePos).normalized;
                        npcPower = MAX_POWER * 1.5f; // full power break
                        npcSpinValue = -0.05f; // mild draw
                        if (testMode) _RecordShotPre();
                        if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                        float baseDuration = testMode ? 0.3f : 0.5f;
                        npcChargeDuration = baseDuration + npcPower * (testMode ? 0.2f : 0.8f);
                        npcChargeElapsed = 0f;
                        table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                        npcState = NPC_CHARGING;
                        break;
                    }
                    _isFirstNpcShot = false;

                    _Log("[NPC] 检测到NPC回合,开始计算... open=" + table.isTableOpenLocal
                        + " pocketed=0x" + table.ballsPocketedLocal.ToString("X8")
                        + " teamId=" + table.teamIdLocal + " teamColor=" + table.teamColorLocal
                        + " npcGroup=" + npcGroupId);
                    _LogTableState("计算开始");
                    bool found = _FindBestShot();
                    if (found)
                    {
                        if (testMode) _RecordShotPre();
                        if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                        // Natural timing: harder shots = longer pullback, like a real player
                        float baseDuration = testMode ? 0.4f : 0.8f;
                        npcChargeDuration = baseDuration + npcPower * (testMode ? 0.3f : 1.2f);
                        npcChargeElapsed = 0f;
                        table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                        npcState = NPC_CHARGING;
                    }
                    else
                    {
                        _NpcFireSafetyShot();
                    }
                }
                break;

            case NPC_CHARGING:
                npcChargeElapsed += Time.deltaTime;
                table.desktopManager._NpcUpdateCharge(npcAimDir, npcChargeElapsed / npcChargeDuration);
                if (npcChargeElapsed >= npcChargeDuration)
                {
                    npcPower = table.desktopManager._NpcGetPower();
                    table.desktopManager._NpcFinishCharge();
                    npcTimer = testMode ? 0.2f : UnityEngine.Random.Range(0.5f, 2.0f);
                    npcState = NPC_DELAYING;
                }
                break;

            case NPC_DELAYING:
                npcTimer -= Time.deltaTime;
                if (npcTimer <= 0f)
                {
                    _NpcShoot();
                }
                break;

            case NPC_SHOOTING:
                table.desktopManager._NpcUpdateShot(Time.deltaTime);
                if (!table.desktopManager._NpcIsShooting())
                {
                    npcState = NPC_OBSERVING;
                }
                break;

            case NPC_OBSERVING:
                if (!table.isLocalSimulationRunning)
                {
                    // Wait one frame for _FlushBuffer() to sync teamIdLocal before NPC can fire again
                    npcFrameDelay = 1;

                    // Track repeat shots to prevent infinite loops
                    if (npcTargetBall == _lastShotBall && npcTargetPocket == _lastShotPocket)
                    {
                        _repeatCount++;
                    }
                    else
                    {
                        _lastShotBall = npcTargetBall;
                        _lastShotPocket = npcTargetPocket;
                        _repeatCount = 1;
                    }

                    // Log shot result
                    table._LogInfo("[NPC] 结果: foul=" + table.foulStateLocal + " pocketed=0x" + table.ballsPocketedLocal.ToString("X8") + " gameLive=" + table.gameLive
                        + " 撞击点=(" + table.firstHitPos.x.ToString("F3") + "," + table.firstHitPos.z.ToString("F3") + ")");
                    // Record shot result in test mode
                    if (testMode)
                    {
                        bool foul = table.foulStateLocal != 0;
                        _RecordShotPost(foul);

                        // Check if game is over via table state
                        if (!table.gameLive)
                        {
                            testGameCount++;
                            _corruptionResetCount = 0; // game ended normally, reset corruption counter
                            bool oneShot = testShotCount <= 1;
                            if (oneShot) testOneShotClearCount++;
                            table._LogInfo("[TEST] 第" + testGameCount + "/" + MAX_TEST_GAMES + "局结束 (winningTeam=" + table.winningTeamLocal + "), 共" + testShotCount + "杆" + (oneShot ? " ★一杆清台!" : ""));
                            testShotCount = 0;
                            if (testGameCount >= MAX_TEST_GAMES)
                            {
                                table._LogInfo("[TEST] 完成" + MAX_TEST_GAMES + "局! 一杆清台次数: " + testOneShotClearCount);
                                _StopTestMode();
                            }
                            else
                            {
                                _restartScheduled = true;
                                SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 1.0f);
                            }
                        }
                        else if (testShotCount >= MAX_TEST_SHOTS)
                        {
                            _Log("[TEST] 达到最大射击次数 " + MAX_TEST_SHOTS);
                            _StopTestMode();
                        }
                    }

                    // Sync desktop marker to body's current position before releasing NPC control
                    // so FixedUpdate doesn't snap the cue back to original grip position
                    CueController cue = table.activeCue;
                    if (cue != null)
                    {
                        cue.UpdateDesktopPosition();
                        cue._SetNpcControlled(false);
                    }
                    npcState = NPC_IDLE;
                    npcBallPlaced = false;

                    // In test mode, set short timer to auto-restart next shot
                    if (testMode)
                    {
                        npcTimer = 0.3f;
                    }
                }
                break;
        }
    }

    // ===================== SHOT SELECTION (走位+翻袋+K球+清台) =====================

    private void _InitPockets()
    {
        // 从ModelData读取cornerToSideOffset，保持与Inspector同步
        ModelData data = table.tableModels[table.tableModelLocal];
        if (data != null) cornerToSideOffset = data.cornerToSideOffset;
        _RecalcPockets();
    }

    private void _RecalcPockets()
    {

        // NPC pocket targets: offset from pocket center toward OPPOSITE side pocket
        Vector3 corner = table.k_vE;
        Vector3 side = table.k_vF;
        float innerR = 0.078f;

        // C0 (top-right +x,+z) → toward S5 (bottom 0,-z)
        npcPockets[0] = corner + (new Vector3(0, 0, -side.z) - corner).normalized * innerR;
        // C1 (bottom-right +x,-z) → toward S4 (top 0,+z)
        npcPockets[1] = new Vector3(corner.x, corner.y, -corner.z);
        npcPockets[1] += (new Vector3(0, 0, side.z) - npcPockets[1]).normalized * innerR;
        // C2 (top-left -x,+z) → toward S5 (bottom 0,-z)
        npcPockets[2] = new Vector3(-corner.x, corner.y, corner.z);
        npcPockets[2] += (new Vector3(0, 0, -side.z) - npcPockets[2]).normalized * innerR;
        // C3 (bottom-left -x,-z) → toward S4 (top 0,+z)
        npcPockets[3] = new Vector3(-corner.x, corner.y, -corner.z);
        npcPockets[3] += (new Vector3(0, 0, side.z) - npcPockets[3]).normalized * innerR;
        // S4 (top 0,+z) → toward center (0,0)
        npcPockets[4] = side + (Vector3.zero - side).normalized * 0.072f;
        // S5 (bottom 0,-z) → toward center (0,0)
        npcPockets[5] = new Vector3(side.x, side.y, -side.z);
        npcPockets[5] += (Vector3.zero - npcPockets[5]).normalized * 0.072f;

        // Save original positions before side offset
        for (int i = 0; i < 6; i++) npcPocketsOriginal[i] = npcPockets[i];

        // Corner T-points: offset toward midpoint of opposite side pocket + opposite corner pocket
        // Use npcPocketsOriginal to avoid chained offset
        Vector3 midC0 = (npcPocketsOriginal[5] + npcPocketsOriginal[3]) * 0.5f; // S5 + C3
        Vector3 midC1 = (npcPocketsOriginal[4] + npcPocketsOriginal[2]) * 0.5f; // S4 + C2
        Vector3 midC2 = (npcPocketsOriginal[5] + npcPocketsOriginal[0]) * 0.5f; // S5 + C0
        Vector3 midC3 = (npcPocketsOriginal[4] + npcPocketsOriginal[1]) * 0.5f; // S4 + C1
        npcPockets[0] = Vector3.Lerp(npcPocketsOriginal[0], midC0, cornerToSideOffset);
        npcPockets[1] = Vector3.Lerp(npcPocketsOriginal[1], midC1, cornerToSideOffset);
        npcPockets[2] = Vector3.Lerp(npcPocketsOriginal[2], midC2, cornerToSideOffset);
        npcPockets[3] = Vector3.Lerp(npcPocketsOriginal[3], midC3, cornerToSideOffset);
    }

    private bool _FindBestShot()
    {
        // === Repeat shot prevention: if same shot chosen 3+ times, force safety ===
        if (_repeatCount >= 3)
        {
            _Log("[NPC] 重复shot检测: 球" + _lastShotBall + "->袋" + _lastShotPocket + " 已重复" + _repeatCount + "次,强制安全球");
            _repeatCount = 0;
            _lastShotBall = -1;
            return false;
        }

        Vector3 cuePos = table.ballsP[0];
        uint targetBalls = _GetTargetBalls();
        _InitPockets();
        _VisualizeTPoints();
        _Log("[NPC] 袋口: 角=" + npcPockets[0].ToString("F3") + " 侧=" + npcPockets[4].ToString("F3"));

        int targetCount = 0;
        string targetList = "";
        for (int i = 1; i <= 15; i++)
        {
            if ((targetBalls & (1u << i)) != 0) { targetCount++; targetList += i + " "; }
        }
        _Log("[NPC] 目标球数=" + targetCount + " open=" + table.isTableOpenLocal + " 组=" + npcGroupId + " 球=[" + targetList.Trim() + "]");

        float bestScore = -1f;
        int bestBall = -1;
        int bestPocket = -1;
        Vector3 bestAimDir = Vector3.forward;
        float bestShotDist = 1f;
        float bestSpin = 0f;
        int bestShotType = 0; // 0=direct, 1=bank, 2=kick
        int bestKickCushion = -1; // first cushion for kick shot
        int bestKickCushion2 = -1; // second cushion for two-cushion kick

        // === PASS 1: Direct pocketing shots ===
        for (int b = 1; b <= 15; b++)
        {
            if ((targetBalls & (1u << b)) == 0) continue;
            if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
            Vector3 ballPos = table.ballsP[b];

            for (int p = 0; p < 6; p++)
            {
                Vector3 pocketPos = npcPockets[p];

                float ballToPocket = (pocketPos - ballPos).magnitude;
                if (ballToPocket < 0.05f || ballToPocket > 2.0f) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " 距离=" + ballToPocket.ToString("F3")); continue; }

                Vector3 t2pDir = (pocketPos - ballPos) / ballToPocket;
                Vector3 ghostBall = ballPos - t2pDir * BALL_DIAMETER;
                // Ghost ball must be roughly on the table — allow small tolerance for corner shots
                if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH + BALL_RADIUS * 0.5f
                    || Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT + BALL_RADIUS * 0.5f) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " ghost出界"); continue; }
                Vector3 cueToGhost = ghostBall - cuePos;
                float shotDist = cueToGhost.magnitude;
                if (shotDist < 0.05f || shotDist > 2.5f) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " shotDist=" + shotDist.ToString("F3")); continue; }

                Vector3 aimDir = cueToGhost / shotDist;
                float alignment = Vector3.Dot(aimDir, t2pDir);
                if (alignment < 0.05f) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " alignment=" + alignment.ToString("F3")); continue; } // max ~87° cut angle

                // Throw compensation: aim thin to counteract ball-ball friction (throw)
                // [TEMPORARILY DISABLED FOR TESTING]
                /*
                float cutAngleRad = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f));
                if (cutAngleRad > 0.05f)
                {
                    float sinCut = Mathf.Sin(cutAngleRad);
                    float throwOffset = 0.001f * sinCut * Mathf.Clamp01(cutAngleRad * cutAngleRad / 0.5f);
                    float halfW = table.k_TABLE_WIDTH;
                    float halfH = table.k_TABLE_HEIGHT;
                    bool nearCushion = ballPos.x > halfW - 0.06f || ballPos.x < -halfW + 0.06f
                                     || ballPos.z > halfH - 0.06f || ballPos.z < -halfH + 0.06f;
                    if (nearCushion && cutAngleRad > 0.4f)
                    {
                        throwOffset *= 2.5f;
                    }
                    Vector3 perp = new Vector3(-t2pDir.z, 0f, t2pDir.x);
                    if (Vector3.Dot(perp, aimDir) > 0f) perp = -perp;
                    ghostBall += perp * throwOffset;
                    cueToGhost = ghostBall - cuePos;
                    shotDist = cueToGhost.magnitude;
                    if (shotDist < 0.05f || shotDist > 2.5f) continue;
                    aimDir = cueToGhost / shotDist;
                    alignment = Vector3.Dot(aimDir, t2pDir);
                    if (alignment < 0.05f) continue;
                }
                */
                if (!_IsPathClear(cuePos, ghostBall, b)) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " 路径遮挡"); continue; }
                // Ghost ball position must not overlap any other ball
                {
                    bool ghostBlocked = false;
                    for (int g = 1; g <= 15; g++)
                    {
                        if (g == b) continue;
                        if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                        float gDistSqr = (table.ballsP[g] - ghostBall).sqrMagnitude;
                        if (gDistSqr < BALL_DIAMSQR) { ghostBlocked = true; break; }
                    }
                    if (ghostBlocked) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " ghost重叠"); continue; }
                }
                // Check cue ball path doesn't pass through target ball before reaching ghost ball
                {
                    Vector3 c2g = ghostBall - cuePos;
                    float c2gSqr = c2g.sqrMagnitude;
                    if (c2gSqr > 0.0001f)
                    {
                        float t = Vector3.Dot(ballPos - cuePos, c2g) / c2gSqr;
                        if (t > 0f && t < 1f)
                        {
                            Vector3 closest = cuePos + c2g * t;
                            float distSqr = (ballPos - closest).sqrMagnitude;
                            if (distSqr < BALL_DIAMSQR) { if (debug) _Log("[NPC] 跳过: 球" + b + "->袋" + p + " 母球穿目标球"); continue; }
                        }
                    }
                }
                // Check cue ball path doesn't cross cushion rail
                if (_IsPathCrossesCushion(cuePos, ghostBall))
                {
                    _Log("[NPC] 跳过: 球" + b + "->袋" + p + " cue路径穿库");
                    continue;
                }
                float cutAnglePre = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f));
                if (_IsBallToPocketBlocked(ballPos, pocketPos, b, cutAnglePre, table.pocketLocations[p]))
                {
                    _Log("[NPC] 跳过: 球" + b + "->袋" + p + " jaw碰撞/路径遮挡 切角=" + (cutAnglePre * Mathf.Rad2Deg).ToString("F1") + "°");
                    continue;
                }
                // Check target ball path to pocket doesn't cross cushion rail
                if (_IsPathCrossesCushion(ballPos, pocketPos))
                {
                    _Log("[NPC] 跳过: 球" + b + "->袋" + p + " 目标球路径穿库");
                    continue;
                }

                if (debug) _Log("[NPC] 球" + b + "->袋" + p + " 通过所有检查! alignment=" + alignment.ToString("F3") + " cut=" + (cutAnglePre * Mathf.Rad2Deg).ToString("F1") + "° dist=" + shotDist.ToString("F3"));
                // Pocketing score: alignment is dominant, shorter = better
                float pocketScore = alignment * 2.0f
                    + Mathf.Clamp01(1.0f - shotDist / 2.0f) * 0.3f
                    + Mathf.Clamp01(1.0f - ballToPocket / 1.5f) * 0.2f;

                // Extreme cut angle penalty: shots >70° are very hard to make
                float cutAngleDeg = cutAnglePre * Mathf.Rad2Deg;
                if (cutAngleDeg > 85f)
                {
                    pocketScore *= Mathf.Clamp01(1f - (cutAngleDeg - 70f) / 20f);
                }

                // Position play: only used as tiebreaker between equally easy shots, not for selection
                float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDir, t2pDir), -1f, 1f));
                float posBonus = _EvalPositionPlay(cuePos, aimDir, ballPos, t2pDir, cutAngle, targetBalls, b);
                float spinForShot = _posPlaySpin;
                // Selection: purely based on pocketing ease (alignment, distance, ball-to-pocket)
                float totalScore = pocketScore;
                // Tiny tiebreaker: if two shots are nearly equal pocketability, prefer the one with position
                if (posBonus > 0f)
                    totalScore += 0.01f;

                // Scratch risk: trace tangent line, penalize score if cue ball heads toward pocket
                if (cutAnglePre > 0.25f && ballToPocket < 1.0f)
                {
                    Vector3 tangent = new Vector3(-aimDir.z, 0f, aimDir.x);
                    Vector3 toTarget = ballPos - cuePos;
                    if (Vector3.Dot(tangent, toTarget) < 0f) tangent = -tangent;

                    bool scratchRisk = false;
                    float trRemaining = 2.0f;
                    Vector3 trStart = ghostBall;
                    Vector3 trDir = tangent;
                    for (int trBounce = 0; trBounce <= 2 && trRemaining > 0f; trBounce++)
                    {
                        float trNearestT = trRemaining;
                        float tw = table.k_TABLE_WIDTH;
                        float th = table.k_TABLE_HEIGHT;
                        if (Mathf.Abs(trDir.x) > 0.001f)
                        {
                            float t = (tw - BALL_RADIUS - trStart.x) / trDir.x;
                            if (t > 0.01f && t < trNearestT) trNearestT = t;
                            t = (-tw + BALL_RADIUS - trStart.x) / trDir.x;
                            if (t > 0.01f && t < trNearestT) trNearestT = t;
                        }
                        if (Mathf.Abs(trDir.z) > 0.001f)
                        {
                            float t = (th - BALL_RADIUS - trStart.z) / trDir.z;
                            if (t > 0.01f && t < trNearestT) trNearestT = t;
                            t = (-th + BALL_RADIUS - trStart.z) / trDir.z;
                            if (t > 0.01f && t < trNearestT) trNearestT = t;
                        }

                        int trSteps = Mathf.Max(1, (int)(trNearestT / (BALL_DIAMETER * 0.8f)));
                        for (int s = 1; s <= trSteps; s++)
                        {
                            Vector3 trCheck = trStart + trDir * (trNearestT * s / trSteps);
                            for (int sp = 0; sp < 6; sp++)
                            {
                                if ((trCheck - table.pocketLocations[sp]).magnitude < table.k_INNER_RADIUS_CORNER + BALL_RADIUS)
                                {
                                    scratchRisk = true;
                                    break;
                                }
                            }
                            if (scratchRisk) break;
                        }
                        if (scratchRisk) break;

                        // Find which cushion is hit first
                        int hitCushion = -1;
                        float hitT = trNearestT;
                        if (Mathf.Abs(trDir.x) > 0.001f)
                        {
                            float t = (tw - BALL_RADIUS - trStart.x) / trDir.x;
                            if (t > 0.01f && t < hitT) { hitT = t; hitCushion = 3; }
                            t = (-tw + BALL_RADIUS - trStart.x) / trDir.x;
                            if (t > 0.01f && t < hitT) { hitT = t; hitCushion = 2; }
                        }
                        if (Mathf.Abs(trDir.z) > 0.001f)
                        {
                            float t = (th - BALL_RADIUS - trStart.z) / trDir.z;
                            if (t > 0.01f && t < hitT) { hitT = t; hitCushion = 0; }
                            t = (-th + BALL_RADIUS - trStart.z) / trDir.z;
                            if (t > 0.01f && t < hitT) { hitT = t; hitCushion = 1; }
                        }
                        if (hitCushion < 0) break;
                        Vector3 hitPos = trStart + trDir * hitT;
                        if (hitCushion == 0 || hitCushion == 1) trDir.z = -trDir.z;
                        else trDir.x = -trDir.x;
                        trRemaining -= hitT;
                        trStart = hitPos;
                    }

                    if (scratchRisk)
                    {
                        totalScore *= 0.3f;
                    }
                }

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestBall = b;
                    bestPocket = p;
                    bestAimDir = aimDir;
                    bestShotDist = shotDist;
                    bestSpin = spinForShot;
                    bestShotType = 0;
                }
            }
        }

        // === PASS 2: Bank shots (翻袋) — only if no direct shot available ===
        if (bestScore < 0.2f)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];

                for (int p = 0; p < 6; p++)
                {
                    Vector3 pocketPos = npcPockets[p];
                    for (int cushion = 0; cushion < 4; cushion++)
                    {
                        // Save original reflected for bounce point calculation (geometry, no friction offset)
                        Vector3 reflectedOrig = _ReflectPocket(pocketPos, cushion);
                        // Friction offset: friction makes bounce steeper (closer to normal),
                        // so ball travels LESS along cushion after bounce (closer to normal).
                        // Ball lands TOWARD the normal, away from pocket → need steeper aim.
                        // Shift virtual pocket TOWARD ball travel direction.
                        Vector3 reflected = reflectedOrig;
                        Vector3 bankRawDir = (reflectedOrig - ballPos).normalized;
                        // Dynamic offset: ball→cushion distance reflects power needs;
                        // higher power → more friction → ball travels LESS along cushion
                        float ballToCushionDist = (reflectedOrig - ballPos).magnitude;
                        float baseFriction = 0.08f;
                        float frictionScale = Mathf.Clamp(ballToCushionDist * 0.12f, 0f, 0.08f);
                        float frictionOffset = baseFriction + frictionScale;
                        switch (cushion)
                        {
                            case 0:
                            case 1:
                                reflected.x += Mathf.Sign(bankRawDir.x) * frictionOffset;
                                break;
                            case 2:
                            case 3:
                                reflected.z += Mathf.Sign(bankRawDir.z) * frictionOffset;
                                break;
                        }
                        Vector3 bankDir = (reflected - ballPos).normalized;
                        // Cue must be on the OPPOSITE side of target from bank direction,
                        // so cue pushes target TOWARD cushion (in bankDir direction).
                        // cueToTarget points cue→target; if it aligns with bankDir (Dot>0),
                        // cue is behind target relative to cushion — would push target AWAY.
                        Vector3 cueToTarget = ballPos - cuePos;
                        if (Vector3.Dot(cueToTarget, bankDir) > 0f) continue;
                        Vector3 ghostBall = ballPos - bankDir * BALL_DIAMETER;
                        if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH - BALL_RADIUS
                            || Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT - BALL_RADIUS) continue;
                        Vector3 cueToGhost = ghostBall - cuePos;
                        float shotDist = cueToGhost.magnitude;
                        if (shotDist < 0.1f || shotDist > 2.0f) continue;

                        Vector3 aimDir = cueToGhost / shotDist;
                        float alignment = Vector3.Dot(aimDir, bankDir);
                        if (alignment < 0.5f) continue;
                        float cutAngleB = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDir, bankDir), -1f, 1f));
                        if (cutAngleB < 0.26f) continue;
                        if (cutAngleB > 2.79f) continue;
                        if (!_IsPathClear(cuePos, ghostBall, b)) continue;
                        // Check target ball doesn't block cue path to ghost
                        {
                            bool targetBlocks = false;
                            Vector3 c2g = ghostBall - cuePos;
                            float c2gLen = c2g.magnitude;
                            int c2gSteps = Mathf.Max(1, (int)(c2gLen / BALL_RADIUS));
                            for (int s = 1; s < c2gSteps; s++)
                            {
                                Vector3 checkPt = cuePos + c2g * ((float)s / c2gSteps);
                                if ((checkPt - ballPos).sqrMagnitude < BALL_DIAMSQR)
                                {
                                    targetBlocks = true;
                                    break;
                                }
                            }
                            if (targetBlocks)
                            {
                                _Log("[NPC] 跳过翻袋: 球" + b + "->袋" + p + " 目标球挡住ghost路径");
                                continue;
                            }
                        }
                        {
                            bool ghostBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                float gDistSqr = (table.ballsP[g] - ghostBall).sqrMagnitude;
                                if (gDistSqr < BALL_DIAMSQR) { ghostBlocked = true; break; }
                            }
                            if (ghostBlocked) continue;
                        }
                        if (_IsPathCrossesCushion(cuePos, ghostBall)) continue;

                        // === Bank shot: check target ball path to cushion and pocket ===
                        {
                            // Calculate bounce point using friction-adjusted reflected (matches actual aim direction)
                            Vector3 bouncePoint = _GetCushionBouncePoint(ballPos, reflected, cushion);
                            if (bouncePoint.x == float.MaxValue) continue;
                            // Bounce point must be on valid cushion segment (not in pocket opening)
                            if (!_IsCushionBouncePointValid(bouncePoint, cushion)) continue;

                            // Check path from target ball to cushion bounce point
                            bool targetPathBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                Vector3 gPos = table.ballsP[g];
                                Vector3 toBounce = bouncePoint - ballPos;
                                float bounceLen = toBounce.magnitude;
                                if (bounceLen < 0.001f) continue;
                                Vector3 bounceDir = toBounce / bounceLen;
                                Vector3 oc = gPos - ballPos;
                                float along = Vector3.Dot(oc, bounceDir);
                                if (along < BALL_RADIUS || along > bounceLen - BALL_RADIUS) continue;
                                Vector3 closest = ballPos + bounceDir * along;
                                float perpDist = (gPos - closest).sqrMagnitude;
                                if (perpDist < (BALL_DIAMETER + BALL_RADIUS) * (BALL_DIAMETER + BALL_RADIUS))
                                {
                                    targetPathBlocked = true;
                                    break;
                                }
                            }
                            if (targetPathBlocked)
                            {
                                _Log("[NPC] 跳过翻袋: 球" + b + "->袋" + p + " 目标球到库边路径遮挡");
                                continue;
                            }

                            // Check path from cushion bounce point to pocket
                            bool returnPathBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                Vector3 gPos = table.ballsP[g];
                                Vector3 toPocket = pocketPos - bouncePoint;
                                float pocketLen = toPocket.magnitude;
                                if (pocketLen < 0.001f) continue;
                                Vector3 pocketDir = toPocket / pocketLen;
                                Vector3 oc = gPos - bouncePoint;
                                float along = Vector3.Dot(oc, pocketDir);
                                if (along < BALL_RADIUS || along > pocketLen - BALL_RADIUS) continue;
                                Vector3 closest = bouncePoint + pocketDir * along;
                                float perpDist = (gPos - closest).sqrMagnitude;
                                if (perpDist < (BALL_DIAMETER + BALL_RADIUS) * (BALL_DIAMETER + BALL_RADIUS))
                                {
                                    returnPathBlocked = true;
                                    break;
                                }
                            }
                            if (returnPathBlocked)
                            {
                                _Log("[NPC] 跳过翻袋: 球" + b + "->袋" + p + " 库边到袋口路径遮挡");
                                continue;
                            }

                            // === Trajectory verification: does ball path actually reach pocket? ===
                            {
                                Vector3 pocketCenter = table.pocketLocations[p];
                                Vector3 bounceToPocket = (pocketPos - bouncePoint).normalized;
                                float projT = Vector3.Dot(pocketCenter - bouncePoint, bounceToPocket);
                                float minDist = float.MaxValue;
                                if (projT > 0f)
                                {
                                    Vector3 closest = bouncePoint + bounceToPocket * projT;
                                    minDist = (pocketCenter - closest).magnitude;
                                }
                                float pocketRadius = table.k_INNER_RADIUS_CORNER;
                                if (Mathf.Abs(pocketCenter.x) < 0.1f && Mathf.Abs(pocketCenter.z) > 0.5f)
                                    pocketRadius = table.k_INNER_RADIUS_SIDE;
                                if (minDist > pocketRadius)
                                {
                                    _Log("[NPC] 跳过翻袋: 球" + b + "->袋" + p
                                        + " 轨迹偏差=" + (minDist * 100f).ToString("F1") + "cm > 袋半径"
                                        + (pocketRadius * 100f).ToString("F1") + "cm");
                                    continue;
                                }
                            }
                        }

                        float baseScore = alignment * 1.0f + Mathf.Clamp01(1.0f - shotDist / 2.0f) * 0.3f;
                        // Position play for bank shots — tiebreaker only
                        float bankCutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDir, bankDir), -1f, 1f));
                        float bankPosBonus = _EvalPositionPlay(cuePos, aimDir, ballPos, bankDir, bankCutAngle, targetBalls, b);
                        float bankSpinForShot = _posPlaySpin;
                        float score = baseScore;
                        if (bankPosBonus > 0f)
                            score += 0.01f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestBall = b;
                            bestPocket = p;
                            bestAimDir = aimDir;
                            bestShotDist = shotDist;
                            bestSpin = bankSpinForShot;
                            bestShotType = 1;
                        }
                    }
                }
            }
        }

        // === PASS 2b: Two-cushion bank shots (两库翻袋) — ball hits two cushions before pocket ===
        if (bestScore < 0.5f)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];

                for (int p = 0; p < 6; p++)
                {
                    Vector3 pocketPos = npcPockets[p];
                    // Try all ordered pairs of cushions: ball→c1→c2→pocket
                    for (int c1 = 0; c1 < 4; c1++)
                    {
                        for (int c2 = 0; c2 < 4; c2++)
                        {
                            if (c1 == c2) continue;
                            // Reflect pocket across c2, then across c1
                            Vector3 temp = _ReflectPocket(pocketPos, c2);
                            Vector3 reflected2 = _ReflectPocket(temp, c1);

                            Vector3 bankDir = (reflected2 - ballPos).normalized;
                            // Cue must push target TOWARD cushion (in bankDir direction)
                            Vector3 cueToTarget2 = ballPos - cuePos;
                            if (Vector3.Dot(cueToTarget2, bankDir) > 0f) continue;
                            Vector3 ghostBall = ballPos - bankDir * BALL_DIAMETER;
                            if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH - BALL_RADIUS
                                || Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT - BALL_RADIUS) continue;
                            Vector3 cueToGhost = ghostBall - cuePos;
                            float shotDist = cueToGhost.magnitude;
                            if (shotDist < 0.1f || shotDist > 2.5f) continue;

                            Vector3 aimDir = cueToGhost / shotDist;
                            float alignment = Vector3.Dot(aimDir, bankDir);
                            if (alignment < 0.4f) continue;
                            float cutAngleB = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDir, bankDir), -1f, 1f));
                            if (cutAngleB < 0.26f || cutAngleB > 2.79f) continue;
                            if (!_IsPathClear(cuePos, ghostBall, b)) continue;
                            if (_IsPathCrossesCushion(cuePos, ghostBall)) continue;

                            // Calculate bounce point on first cushion
                            Vector3 reflected1 = _ReflectPocket(pocketPos, c2);
                            Vector3 reflected2 = _ReflectPocket(reflected1, c1);
                            Vector3 bounce1 = _GetCushionBouncePoint(ballPos, reflected2, c1);
                            if (bounce1.x == float.MaxValue) continue;
                            if (!_IsCushionBouncePointValid(bounce1, c1)) continue;

                            // Calculate bounce point on second cushion from the double-reflected path
                            Vector3 bounce2 = _GetCushionBouncePoint(bounce1, reflected1, c2);
                            if (bounce2.x == float.MaxValue) continue;
                            if (!_IsCushionBouncePointValid(bounce2, c2)) continue;

                            // Check path: ball→bounce1 not blocked
                            bool pathBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                Vector3 gPos = table.ballsP[g];
                                Vector3 toB1 = bounce1 - ballPos;
                                float b1Len = toB1.magnitude;
                                if (b1Len < 0.001f) continue;
                                Vector3 b1Dir = toB1 / b1Len;
                                Vector3 oc = gPos - ballPos;
                                float along = Vector3.Dot(oc, b1Dir);
                                if (along < BALL_RADIUS || along > b1Len - BALL_RADIUS) continue;
                                Vector3 closest = ballPos + b1Dir * along;
                                if ((gPos - closest).sqrMagnitude < (BALL_DIAMETER + BALL_RADIUS) * (BALL_DIAMETER + BALL_RADIUS))
                                { pathBlocked = true; break; }
                            }
                            if (pathBlocked) continue;

                            // Check path: bounce1→bounce2 not blocked
                            pathBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                Vector3 gPos = table.ballsP[g];
                                Vector3 toB2 = bounce2 - bounce1;
                                float b2Len = toB2.magnitude;
                                if (b2Len < 0.001f) continue;
                                Vector3 b2Dir = toB2 / b2Len;
                                Vector3 oc = gPos - bounce1;
                                float along = Vector3.Dot(oc, b2Dir);
                                if (along < BALL_RADIUS || along > b2Len - BALL_RADIUS) continue;
                                Vector3 closest = bounce1 + b2Dir * along;
                                if ((gPos - closest).sqrMagnitude < (BALL_DIAMETER + BALL_RADIUS) * (BALL_DIAMETER + BALL_RADIUS))
                                { pathBlocked = true; break; }
                            }
                            if (pathBlocked) continue;

                            // Check path: bounce2→pocket not blocked
                            pathBlocked = false;
                            for (int g = 1; g <= 15; g++)
                            {
                                if (g == b) continue;
                                if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                                Vector3 gPos = table.ballsP[g];
                                Vector3 toP = pocketPos - bounce2;
                                float pLen = toP.magnitude;
                                if (pLen < 0.001f) continue;
                                Vector3 pDir = toP / pLen;
                                Vector3 oc = gPos - bounce2;
                                float along = Vector3.Dot(oc, pDir);
                                if (along < BALL_RADIUS || along > pLen - BALL_RADIUS) continue;
                                Vector3 closest = bounce2 + pDir * along;
                                if ((gPos - closest).sqrMagnitude < (BALL_DIAMETER + BALL_RADIUS) * (BALL_DIAMETER + BALL_RADIUS))
                                { pathBlocked = true; break; }
                            }
                            if (pathBlocked) continue;

                            // === Trajectory verification: does ball path actually reach pocket? ===
                            {
                                Vector3 pocketCenter = table.pocketLocations[p];
                                Vector3 bounce2ToPocket = (pocketPos - bounce2).normalized;
                                float projT = Vector3.Dot(pocketCenter - bounce2, bounce2ToPocket);
                                float minDist = float.MaxValue;
                                if (projT > 0f)
                                {
                                    Vector3 closest = bounce2 + bounce2ToPocket * projT;
                                    minDist = (pocketCenter - closest).magnitude;
                                }
                                float pocketRadius = table.k_INNER_RADIUS_CORNER;
                                if (Mathf.Abs(pocketCenter.x) < 0.1f && Mathf.Abs(pocketCenter.z) > 0.5f)
                                    pocketRadius = table.k_INNER_RADIUS_SIDE;
                                if (minDist > pocketRadius)
                                {
                                    _Log("[NPC] 跳过两库翻袋: 球" + b + "->袋" + p
                                        + " 库" + c1 + "->库" + c2
                                        + " 轨迹偏差=" + (minDist * 100f).ToString("F1") + "cm > 袋半径"
                                        + (pocketRadius * 100f).ToString("F1") + "cm");
                                    continue;
                                }
                            }

                            float totalDist = shotDist + (bounce1 - ballPos).magnitude + (bounce2 - bounce1).magnitude + (pocketPos - bounce2).magnitude;
                            if (totalDist > 5.0f) continue;

                            float baseScore = alignment * 0.8f + Mathf.Clamp01(1.0f - totalDist / 5.0f) * 0.3f;
                            float score = baseScore;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestBall = b;
                                bestPocket = p;
                                bestAimDir = aimDir;
                                bestShotDist = shotDist;
                                bestSpin = 0f;
                                bestShotType = 1;
                                _Log("[NPC] 两库翻袋: 球" + b + "->袋" + p + " 库" + c1 + "->库" + c2 + " 分=" + score.ToString("F2"));
                            }
                        }
                    }
                }
            }
        }

        // === PASS 2.5: Thin cut (轻碰) — gentle touch when ball is visible but not pocketable ===
        // Priority: if we can see the target ball, try a thin cut before kick shots
        if (bestScore < 0.5f)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];

                // Check if cue ball has clear path to target ball (can "see" it)
                if (!_IsPathClear(cuePos, ballPos, b)) continue;

                // Calculate ghost ball for thin cut: clip the edge at maximum cut angle
                Vector3 cueToBall = ballPos - cuePos;
                float cueToBallDist = cueToBall.magnitude;
                if (cueToBallDist < 0.05f || cueToBallDist > 2.0f) continue;
                Vector3 cueToBallDir = cueToBall / cueToBallDist;

                // Perpendicular to cue→ball line (in horizontal plane) — try both sides
                Vector3 perpA = new Vector3(-cueToBallDir.z, 0f, cueToBallDir.x);
                Vector3 perpB = new Vector3(cueToBallDir.z, 0f, -cueToBallDir.x);

                // Try both sides, pick the one that works
                Vector3 ghostBall = Vector3.zero;
                bool foundGhost = false;
                for (int side = 0; side < 2; side++)
                {
                    Vector3 perp = side == 0 ? perpA : perpB;
                    // Ghost ball offset to the side: edge contact at 90° cut angle (thinnest possible)
                    Vector3 ghost = ballPos + perp * BALL_DIAMETER;
                    if (Mathf.Abs(ghost.x) > table.k_TABLE_WIDTH - BALL_RADIUS
                        || Mathf.Abs(ghost.z) > table.k_TABLE_HEIGHT - BALL_RADIUS) continue;
                    if (!_IsPathClear(cuePos, ghost, b)) continue;
                    bool blocked = false;
                    for (int g = 1; g <= 15; g++)
                    {
                        if (g == b) continue;
                        if ((table.ballsPocketedLocal & (1u << g)) != 0) continue;
                        if ((table.ballsP[g] - ghost).sqrMagnitude < BALL_DIAMSQR) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    ghostBall = ghost;
                    foundGhost = true;
                    break;
                }
                if (!foundGhost) continue;

                // Score as safety: how safe is the cue ball after thin contact?
                // Thin cut = cue ball barely deflects, continues mostly forward
                float safetyScore = 0f;

                // Determine opponent balls (group we are NOT playing)
                uint thinOppBalls = 0u;
                if (npcGroupId == 0) thinOppBalls = ~table.ballsPocketedLocal & 0xFE00u & ~0x2u;
                else if (npcGroupId == 1) thinOppBalls = ~table.ballsPocketedLocal & 0x1FCu & ~0x2u;

                // 1. Distance from opponent balls (farther = safer)
                float minOppDist = float.MaxValue;
                for (int ob = 1; ob <= 15; ob++)
                {
                    if ((thinOppBalls & (1u << ob)) == 0) continue;
                    if ((table.ballsPocketedLocal & (1u << ob)) != 0) continue;
                    float d = (ghostBall - table.ballsP[ob]).magnitude;
                    if (d < minOppDist) minOppDist = d;
                }
                safetyScore += Mathf.Clamp01(minOppDist / 1.5f) * 0.6f;

                // 2. Bonus if cue ball ends up near cushion (harder for opponent)
                float minCushionDist = Mathf.Min(
                    Mathf.Abs(ghostBall.x - table.k_TABLE_WIDTH),
                    Mathf.Abs(ghostBall.x + table.k_TABLE_WIDTH),
                    Mathf.Abs(ghostBall.z - table.k_TABLE_HEIGHT),
                    Mathf.Abs(ghostBall.z + table.k_TABLE_HEIGHT));
                if (minCushionDist < 0.10f) safetyScore += 0.3f;

                // 3. Penalty if cue ball is close to opponent's pocket
                for (int p = 0; p < 6; p++)
                {
                    float dToPocket = (ghostBall - npcPockets[p]).magnitude;
                    if (dToPocket < 0.15f) safetyScore -= 0.2f;
                }

                _Log("[NPC] 轻碰: 球" + b + " dist=" + cueToBallDist.ToString("F2") + " safety=" + safetyScore.ToString("F2"));

                if (safetyScore > bestScore)
                {
                    bestScore = safetyScore;
                    bestBall = b;
                    bestPocket = -1;
                    bestAimDir = cueToBallDir; // aim along cue→ball line for thin contact
                    bestShotDist = cueToBallDist;
                    bestSpin = 0f;
                    bestShotType = 3; // thin cut (轻碰)
                }
            }
        }

        // === PASS 3: Kick shots (K球) — cue ball bounces off cushion first ===
        if (bestScore < 0.5f)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];
                // Try kick shots for all target balls when no direct/bank shot found

                for (int cushion = 0; cushion < 4; cushion++)
                {
                    Vector3 reflectedCue = _ReflectPoint(cuePos, cushion);
                    Vector3 kickDir = (ballPos - reflectedCue).normalized;
                    Vector3 cushionPoint = _GetCushionPoint(cuePos, ballPos, cushion);
                    if (cushionPoint.x == float.MaxValue)
                    {
                        _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 无交点");
                        continue;
                    }

                    float dist1 = (cushionPoint - cuePos).magnitude;
                    float dist2 = (ballPos - cushionPoint).magnitude;
                    if (dist1 + dist2 > 2.5f)
                    {
                        _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 距离过远=" + (dist1 + dist2).ToString("F2"));
                        continue;
                    }

                    Vector3 aimDir = (cushionPoint - cuePos).normalized;
                    // Bounce point must be on valid cushion segment (not in pocket opening)
                    if (!_IsCushionBouncePointValid(cushionPoint, cushion)) continue;
                    // Check ball collision only (skip bounds check — cushion point is on the boundary)
                    if (!_IsPathClearBallsOnly(cuePos, cushionPoint, -1))
                    {
                        _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 母球到库边路径遮挡");
                        continue;
                    }
                    if (!_IsPathClearBallsOnly(cushionPoint, ballPos, b))
                    {
                        _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 库边到目标路径遮挡");
                        continue;
                    }

                    float score = 0.4f + Mathf.Clamp01(1.0f - (dist1 + dist2) / 2.5f) * 0.3f;
                    _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 通过! dist=" + (dist1 + dist2).ToString("F2") + " score=" + score.ToString("F2"));
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestBall = b;
                        bestPocket = -1;
                        bestAimDir = aimDir;
                        bestShotDist = dist1 + dist2;
                        bestSpin = 0f;
                        bestShotType = 2;
                        bestKickCushion = cushion;
                    }
                }
            }
        }

        // === PASS 3b: Two-cushion kick shots (两库勾库) — cue ball hits two cushions before target ===
        if (bestScore < 0.5f)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];

                for (int c1 = 0; c1 < 4; c1++)
                {
                    for (int c2 = 0; c2 < 4; c2++)
                    {
                        if (c1 == c2) continue;
                        // Reflect ball across c2, then across c1 so the cue path matches the full two-cushion geometry
                        Vector3 reflected1 = _ReflectPoint(ballPos, c2);
                        Vector3 reflected2 = _ReflectPoint(reflected1, c1);

                        Vector3 kickDir = (reflected2 - cuePos).normalized;
                        // Find where cue path hits first cushion (c1)
                        Vector3 bounce1 = _GetCushionPoint(cuePos, reflected2, c1);
                        if (bounce1.x == float.MaxValue) continue;
                        if (!_IsCushionBouncePointValid(bounce1, c1)) continue;

                        // From bounce1, find where it hits second cushion (c2) on the reflected path
                        Vector3 bounce2 = _GetCushionPoint(bounce1, reflected1, c2);
                        if (bounce2.x == float.MaxValue) continue;
                        if (!_IsCushionBouncePointValid(bounce2, c2)) continue;

                        float dist1 = (bounce1 - cuePos).magnitude;
                        float dist2 = (bounce2 - bounce1).magnitude;
                        float dist3 = (ballPos - bounce2).magnitude;
                        float totalDist = dist1 + dist2 + dist3;
                        if (totalDist > 3.5f) continue;

                        Vector3 aimDir = (bounce1 - cuePos).normalized;
                        // Check path: cue→bounce1
                        if (!_IsPathClearBallsOnly(cuePos, bounce1, -1)) continue;
                        // Check path: bounce1→bounce2
                        if (!_IsPathClearBallsOnly(bounce1, bounce2, -1)) continue;
                        // Check path: bounce2→ball (exclude target ball)
                        if (!_IsPathClearBallsOnly(bounce2, ballPos, b)) continue;

                        float score = 0.35f + Mathf.Clamp01(1.0f - totalDist / 3.5f) * 0.25f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestBall = b;
                            bestPocket = -1;
                            bestAimDir = aimDir;
                            bestShotDist = totalDist;
                            bestSpin = 0f;
                            bestShotType = 2;
                            bestKickCushion = c1;
                            bestKickCushion2 = c2;
                            _Log("[NPC] 两库勾库: 球" + b + " 库" + c1 + "->库" + c2 + " dist=" + totalDist.ToString("F2") + " score=" + score.ToString("F2"));
                        }
                    }
                }
            }
        }

        // 8-ball is now included in targetBalls by _GetTargetBalls() when group is cleared
        // No separate check needed — PASS 1/2/3 handles it like any other ball

        if (bestBall < 0)
        {
            _Log("[NPC] 无进球路线(直接+翻袋+K球均无)");
            return false;
        }

        // === Calculate power (physics-based) — runs BEFORE scratch prevention so adjustments apply ===
        Vector3 ballPos2 = table.ballsP[bestBall];
        float ballToPocketDist = bestPocket >= 0 ? (npcPockets[bestPocket] - ballPos2).magnitude : 0.5f;
        float cutAngleFinal = 0f;
        if (bestPocket >= 0)
        {
            Vector3 t2pDir2 = (npcPockets[bestPocket] - ballPos2).normalized;
            cutAngleFinal = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2pDir2), -1f, 1f));
        }
        float cosAngle = Mathf.Cos(cutAngleFinal);
        if (cosAngle < 0.5f) cosAngle = 0.5f;
        float effectiveDist = bestShotDist + ballToPocketDist / cosAngle;
        if (bestShotType == 2) effectiveDist *= 1.3f;
        if (bestShotType == 1 && bestShotDist > 1.5f) effectiveDist *= 1.4f; // two-cushion bank: longer path
        float needVel = Mathf.Sqrt(3.92f * effectiveDist) * 0.85f;
        float power = Mathf.Pow(needVel / 4.0f, 1.0f / 1.4f) * 0.5f;
        power = Mathf.Clamp(power, MIN_POWER, MAX_POWER);

        // === Thin cut power: gentle touch, just kiss the ball ===
        if (bestShotType == 3)
        {
            power = MIN_POWER;
            _Log("[NPC] 轻碰减力: 力=" + power.ToString("F2"));
        }

        // === Kick shot power cap: softer than direct shots ===
        if (bestShotType == 2)
        {
            float kickMaxPower = Mathf.Clamp(0.18f + effectiveDist * 0.06f, 0.22f, 0.32f);
            if (power > kickMaxPower)
            {
                _Log("[NPC] 勾库减力: 原力=" + power.ToString("F2") + " -> " + kickMaxPower.ToString("F2"));
                power = kickMaxPower;
            }
        }

        // === Bank shot power cap: dynamic based on total travel distance ===
        if (bestShotType == 1)
        {
            // Ball travels: ball→cushion→pocket. Longer total distance needs more power.
            // Base 0.22 for short banks, up to 0.38 for long ones.
            float bankMaxPower = Mathf.Clamp(0.20f + effectiveDist * 0.08f, 0.25f, 0.38f);
            if (power > bankMaxPower)
            {
                _Log("[NPC] 翻袋减力: 原力=" + power.ToString("F2") + " -> " + bankMaxPower.ToString("F2") + " (dist=" + effectiveDist.ToString("F2") + ")");
                power = bankMaxPower;
            }
        }

        // === Scratch prevention: straight shot near pocket → force draw (direct shots only) ===
        if (bestPocket >= 0 && bestShotType != 1)
        {
            Vector3 bPos = table.ballsP[bestBall];
            Vector3 t2p = (npcPockets[bestPocket] - bPos).normalized;
            float finalCut = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2p), -1f, 1f));
            Vector3 ghostCheck = bPos - t2p * BALL_DIAMETER;
            float ghostToPocket = (npcPockets[bestPocket] - ghostCheck).magnitude;
            float ballToPkt = (npcPockets[bestPocket] - bPos).magnitude;
            float cueToBallDist = (bPos - cuePos).magnitude;
            // Minimum power: consider BOTH cue→target AND target→pocket distances
            float totalDist = cueToBallDist + ballToPkt;
            float minReachPower = Mathf.Pow(totalDist * 1.5f / 4.0f, 1.0f / 1.4f) * 0.5f;
            // Straight shot: stop shot (定杆) — light backspin to cancel forward roll from friction
            if (finalCut < 0.30f)
            {
                float drawDist = Mathf.Clamp01(cueToBallDist / 1.5f);
                bestSpin = Mathf.Lerp(-0.15f, -0.3f, drawDist);
                // Only reduce power for stop shot when target ball is CLOSE to pocket
                // Skip reduction if power is already at minimum — short-distance shots need it
                if (power > MIN_POWER + 0.02f)
                {
                    if (ballToPkt < 0.30f && finalCut < 0.10f)
                    {
                        power *= 0.95f;
                    }
                    else if (ballToPkt < 0.50f)
                    {
                        power *= 0.95f;
                    }
                }
                // Don't reduce power when target is far from pocket — need momentum
                power = Mathf.Max(power, minReachPower);
                _Log("[NPC] 定杆: cut=" + (finalCut * Mathf.Rad2Deg).ToString("F0") + "° dist=" + cueToBallDist.ToString("F2") + " spin=" + bestSpin.ToString("F2") + " 力度→" + power.ToString("F2"));
            }

            // === Cut angle scratch prevention: trace tangent trajectory with cushion bounces ===
            if (finalCut > 0.25f && ballToPkt < 1.0f)
            {
                Vector3 tangent = new Vector3(-bestAimDir.z, 0f, bestAimDir.x);
                Vector3 toTarget = table.ballsP[bestBall] - cuePos;
                if (Vector3.Dot(tangent, toTarget) < 0f) tangent = -tangent;

                // Trace with up to 2 cushion bounces (same as foul prediction)
                bool scratchDetected = false;
                int scratchPocket = -1;
                float scratchDist = float.MaxValue;
                Vector3 traceStart = ghostCheck;
                Vector3 traceDir = tangent;
                float remainingDist = 2.0f; // total trace distance
                for (int bounce = 0; bounce <= 2 && remainingDist > 0f; bounce++)
                {
                    float nearestT = remainingDist;
                    int hitCushion = -1;
                    float tw = table.k_TABLE_WIDTH;
                    float th = table.k_TABLE_HEIGHT;
                    if (Mathf.Abs(traceDir.x) > 0.001f)
                    {
                        float t = (tw - BALL_RADIUS - traceStart.x) / traceDir.x;
                        if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 3; }
                        t = (-tw + BALL_RADIUS - traceStart.x) / traceDir.x;
                        if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 2; }
                    }
                    if (Mathf.Abs(traceDir.z) > 0.001f)
                    {
                        float t = (th - BALL_RADIUS - traceStart.z) / traceDir.z;
                        if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 0; }
                        t = (-th + BALL_RADIUS - traceStart.z) / traceDir.z;
                        if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 1; }
                    }

                    Vector3 segEnd = traceStart + traceDir * nearestT;
                    int stepsInSeg = Mathf.Max(1, (int)(nearestT / (BALL_DIAMETER * 0.8f)));
                    for (int s = 1; s <= stepsInSeg; s++)
                    {
                        Vector3 checkPos = traceStart + traceDir * (nearestT * s / stepsInSeg);
                        for (int sp = 0; sp < 6; sp++)
                        {
                            float d = (checkPos - table.pocketLocations[sp]).magnitude;
                            if (d < table.k_INNER_RADIUS_CORNER + BALL_RADIUS && d < scratchDist)
                            {
                                scratchDetected = true;
                                scratchPocket = sp;
                                scratchDist = d;
                            }
                        }
                    }
                    if (scratchDetected) break;

                    if (hitCushion < 0) break;
                    if (hitCushion == 0 || hitCushion == 1) traceDir.z = -traceDir.z;
                    else traceDir.x = -traceDir.x;
                    remainingDist -= nearestT;
                    traceStart = segEnd;
                }
                if (scratchDetected)
                {
                    bestSpin = -0.6f;
                    power *= 0.70f;
                    _Log("[NPC] 防摔袋: cut=" + (finalCut * Mathf.Rad2Deg).ToString("F0")
                        + "° 母球切线近袋" + scratchPocket + " 距=" + (scratchDist * 100f).ToString("F1")
                        + "cm → spin=-0.6 力度×0.70");
                }
            }

            // === Straight shot scratch prevention: cue ball follows target into pocket ===
            if (finalCut < 0.25f && bestBall >= 0 && bestPocket >= 0)
            {
                Vector3 bPos2 = table.ballsP[bestBall];
                // On straight shot, cue ball continues along same line after contact
                // Check if any pocket is along the cue→target→pocket line
                for (int sp = 0; sp < 6; sp++)
                {
                    Vector3 pktCenter = table.pocketLocations[sp];
                    Vector3 cueToPkt = pktCenter - cuePos;
                    float cueToPktDist = cueToPkt.magnitude;
                    if (cueToPktDist < 0.1f) continue;
                    Vector3 cueToPktDir = cueToPkt / cueToPktDist;
                    // Check if pocket is roughly along the shot line (same direction as aim)
                    float alignment = Vector3.Dot(cueToPktDir, bestAimDir);
                    if (alignment < 0.95f) continue; // not straight enough
                    // Check if target ball is between cue and pocket
                    float cueToBallDist2 = (bPos2 - cuePos).magnitude;
                    if (cueToBallDist2 > cueToPktDist) continue; // pocket is behind cue
                    // Straight shot into pocket — cue ball will follow!
                    // Dynamic correction based on ball-to-pocket distance
                    float bToPkt = (pktCenter - bPos2).magnitude;
                    float dynSpin, dynPowerMul;
                    if (bToPkt < 0.03f)
                    {
                        // Ultra close: ball almost in pocket, cue ball has almost no room to slow down
                        dynSpin = -0.90f;
                        dynPowerMul = 0.35f;
                    }
                    else if (bToPkt < 0.08f)
                    {
                        // Close: need very strong draw
                        dynSpin = -0.80f;
                        dynPowerMul = 0.45f;
                    }
                    else
                    {
                        // Normal: moderate draw
                        dynSpin = -0.65f;
                        dynPowerMul = 0.60f;
                    }
                    bestSpin = dynSpin;
                    power *= dynPowerMul;
                    _Log("[NPC] 直线球防摔袋: 球" + bestBall + "->袋" + sp
                        + " 切角=" + (finalCut * Mathf.Rad2Deg).ToString("F1")
                        + "° 球袋距=" + (bToPkt * 100f).ToString("F1") + "cm"
                        + " → spin=" + dynSpin.ToString("F2") + " 力度×" + dynPowerMul.ToString("F2"));
                    break;
                }
            }

            // === Trajectory verification ===
            Vector3 pocketCenter = table.pocketLocations[bestPocket];
            float minDistToPocket = float.MaxValue;
            Vector3 ballVelDir = t2p;
            float projT = Vector3.Dot(pocketCenter - bPos, ballVelDir);
            if (projT > 0f)
            {
                Vector3 closestOnTrajectory = bPos + ballVelDir * projT;
                minDistToPocket = (pocketCenter - closestOnTrajectory).magnitude;
            }
            float pocketRadius = table.k_INNER_RADIUS_CORNER;
            if (Mathf.Abs(pocketCenter.x) < 0.1f && Mathf.Abs(pocketCenter.z) > 0.5f)
                pocketRadius = table.k_INNER_RADIUS_SIDE;
            _Log("[NPC] 轨迹验证: 球" + bestBall + " 最近袋中心距离=" + (minDistToPocket * 100f).ToString("F1")
                + "cm 入袋半径=" + (pocketRadius * 100f).ToString("F1") + "cm "
                + (minDistToPocket < pocketRadius ? "OK" : "WARNING:可能不入袋"));
        }

        // === Bank shot trajectory verification ===
        if (bestPocket >= 0 && bestShotType == 1)
        {
            Vector3 bPos = table.ballsP[bestBall];
            Vector3 pp = npcPockets[bestPocket];
            // Find which cushion the target ball bounces off
            for (int c = 0; c < 4; c++)
            {
                Vector3 reflected = _ReflectPocket(pp, c);
                // Apply friction offset: shift toward ball travel direction
                Vector3 bankRaw = (reflected - bPos).normalized;
                float ballToCushionDist = (reflected - bPos).magnitude;
                float baseFriction = 0.08f;
                float frictionScale = Mathf.Clamp(ballToCushionDist * 0.12f, 0f, 0.08f);
                float frictionOff = baseFriction + frictionScale;
                switch (c)
                {
                    case 0:
                    case 1:
                        reflected.x += Mathf.Sign(bankRaw.x) * frictionOff;
                        break;
                    case 2:
                    case 3:
                        reflected.z += Mathf.Sign(bankRaw.z) * frictionOff;
                        break;
                }
                Vector3 bouncePt = _GetCushionBouncePoint(bPos, reflected, c);
                if (bouncePt.x == float.MaxValue) continue;
                if (!_IsCushionBouncePointValid(bouncePt, c)) continue;

                // Verify path: bounce→pocket direction
                Vector3 bounceToPocket = (pp - bouncePt).normalized;
                Vector3 pocketCenter = table.pocketLocations[bestPocket];
                float projT = Vector3.Dot(pocketCenter - bouncePt, bounceToPocket);
                float minDist = float.MaxValue;
                if (projT > 0f)
                {
                    Vector3 closest = bouncePt + bounceToPocket * projT;
                    minDist = (pocketCenter - closest).magnitude;
                }
                float pocketRadius = table.k_INNER_RADIUS_CORNER;
                if (Mathf.Abs(pocketCenter.x) < 0.1f && Mathf.Abs(pocketCenter.z) > 0.5f)
                    pocketRadius = table.k_INNER_RADIUS_SIDE;
                _Log("[NPC] 翻袋轨迹验证: 球" + bestBall + "->库" + c + "->袋" + bestPocket
                    + " 最近袋中心距离=" + (minDist * 100f).ToString("F1") + "cm "
                    + (minDist < pocketRadius ? "OK" : "WARNING:可能不入袋"));
                break;
            }
        }

        npcAimDir = bestAimDir;
        npcPower = power;
        npcSpinValue = bestSpin;
        npcTargetBall = bestBall;
        npcTargetPocket = bestPocket;
        npcShotType = bestShotType;
        npcBestScore = bestScore;
        npcKickCushion = bestKickCushion;
        npcKickCushion2 = bestKickCushion2;
        // Calculate first impact point
        if (bestShotType == 2 && bestKickCushion >= 0)
        {
            // Kick shot: first impact is cushion bounce point
            if (bestKickCushion2 >= 0)
            {
                Vector3 reflected1 = _ReflectPoint(table.ballsP[bestBall], bestKickCushion2);
                Vector3 reflected2 = _ReflectPoint(reflected1, bestKickCushion);
                npcFirstImpactPos = _GetCushionPoint(cuePos, reflected2, bestKickCushion);
            }
            else
            {
                npcFirstImpactPos = _GetCushionPoint(cuePos, table.ballsP[bestBall], bestKickCushion);
            }
        }
        else if (bestBall >= 0)
        {
            // Direct/bank shot: first impact is ghost ball position
            npcFirstImpactPos = cuePos + bestAimDir * bestShotDist;
        }
        else npcFirstImpactPos = cuePos;
        // Calculate cut angle for logging
        if (bestPocket >= 0 && bestBall >= 0)
        {
            Vector3 t2pLog = (npcPockets[bestPocket] - table.ballsP[bestBall]).normalized;
            npcCutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2pLog), -1f, 1f)) * Mathf.Rad2Deg;
        }
        else npcCutAngle = 0f;

        string shotTypeStr = bestShotType == 0 ? "直接" : (bestShotType == 1 ? "翻袋" : (bestShotType == 2 ? "K球" : "轻碰"));
        string spinType = bestSpin < -0.05f ? "低杆" : (bestSpin > 0.05f ? "高杆" : "定杆");
        Vector3 ghostBallLog = cuePos + bestAimDir * bestShotDist;
        _Log("[NPC] " + shotTypeStr + ": 球" + bestBall + "->袋" + bestPocket
            + " 力=" + power.ToString("F2") + " 旋=" + bestSpin.ToString("F2") + "(" + spinType + ")"
            + " 距=" + bestShotDist.ToString("F2") + " 分=" + bestScore.ToString("F2")
            + " ghost=(" + ghostBallLog.x.ToString("F3") + "," + ghostBallLog.z.ToString("F3") + ")");
        if (bestPocket >= 0)
        {
            Vector3 bp = table.ballsP[bestBall];
            Vector3 pp = npcPockets[bestPocket];
            Vector3 t2p = (pp - bp).normalized;
            float debugCut = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2p), -1f, 1f)) * Mathf.Rad2Deg;
            _Log("[NPC] 瞄准: aimDir=(" + bestAimDir.x.ToString("F3") + "," + bestAimDir.z.ToString("F3")
                + ") t2pDir=(" + t2p.x.ToString("F3") + "," + t2p.z.ToString("F3")
                + ") 切角=" + debugCut.ToString("F1") + "°"
                + " 袋口=(" + pp.x.ToString("F2") + "," + pp.z.ToString("F2") + ")"
                + " 目标球=(" + bp.x.ToString("F2") + "," + bp.z.ToString("F2") + ")");

            // === Foul prediction: trace cue ball trajectory with cushion bounces ===
            Vector3 ghostForScratch = bp - t2p * BALL_DIAMETER;
            Vector3 cueAfterHit = ghostForScratch;
            Vector3 tangent = new Vector3(-bestAimDir.z, 0f, bestAimDir.x);
            Vector3 cueFinalDir = tangent;
            if (bestSpin < -0.3f) cueFinalDir = -bestAimDir;
            else if (bestSpin > 0.3f) cueFinalDir = bestAimDir;

            // Trace trajectory with up to 2 cushion bounces
            bool scratchRisk = false;
            int scratchPocket = -1;
            float scratchDist = float.MaxValue;
            Vector3 traceStart = cueAfterHit;
            Vector3 traceDir = cueFinalDir;
            float remainingDist = BALL_DIAMETER * 10f; // total trace distance
            for (int bounce = 0; bounce <= 2 && remainingDist > 0f; bounce++)
            {
                // Find nearest cushion intersection
                float nearestT = remainingDist;
                int hitCushion = -1;
                float tw = table.k_TABLE_WIDTH;
                float th = table.k_TABLE_HEIGHT;
                // Right cushion (x = +tw)
                if (Mathf.Abs(traceDir.x) > 0.001f)
                {
                    float t = (tw - BALL_RADIUS - traceStart.x) / traceDir.x;
                    if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 3; }
                }
                // Left cushion (x = -tw)
                if (Mathf.Abs(traceDir.x) > 0.001f)
                {
                    float t = (-tw + BALL_RADIUS - traceStart.x) / traceDir.x;
                    if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 2; }
                }
                // Top cushion (z = +th)
                if (Mathf.Abs(traceDir.z) > 0.001f)
                {
                    float t = (th - BALL_RADIUS - traceStart.z) / traceDir.z;
                    if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 0; }
                }
                // Bottom cushion (z = -th)
                if (Mathf.Abs(traceDir.z) > 0.001f)
                {
                    float t = (-th + BALL_RADIUS - traceStart.z) / traceDir.z;
                    if (t > 0.01f && t < nearestT) { nearestT = t; hitCushion = 1; }
                }

                // Check pockets along this segment
                Vector3 segEnd = traceStart + traceDir * nearestT;
                int stepsInSeg = Mathf.Max(1, (int)(nearestT / (BALL_DIAMETER * 0.8f)));
                for (int s = 1; s <= stepsInSeg; s++)
                {
                    Vector3 checkPos = traceStart + traceDir * (nearestT * s / stepsInSeg);
                    for (int sp = 0; sp < 6; sp++)
                    {
                        float d = (checkPos - table.pocketLocations[sp]).magnitude;
                        if (d < table.k_INNER_RADIUS_CORNER + BALL_RADIUS && d < scratchDist)
                        {
                            scratchRisk = true;
                            scratchPocket = sp;
                            scratchDist = d;
                        }
                    }
                }
                if (scratchRisk) break;

                if (hitCushion < 0) break; // no cushion hit, segment goes to end

                // Bounce off cushion
                if (hitCushion == 0 || hitCushion == 1) traceDir.z = -traceDir.z;
                else traceDir.x = -traceDir.x;
                remainingDist -= nearestT;
                traceStart = segEnd;
            }
            // === Unified foul correction: scratch risk + roadblock in one pass ===
            bool roadblockDetected = false;
            float roadblockPerpDist = 0f;
            int roadblockBall = -1;
            float maxPathLen = BALL_DIAMETER * 8f;
            for (int ob = 1; ob <= 15; ob++)
            {
                if (ob == bestBall) continue;
                if ((table.ballsPocketedLocal & (1u << ob)) != 0) continue;
                Vector3 obPos = table.ballsP[ob];
                Vector3 cuePathDir = cueFinalDir;
                float proj = Vector3.Dot(obPos - cueAfterHit, cuePathDir);
                if (proj > 0 && proj < maxPathLen)
                {
                    Vector3 closestPt = cueAfterHit + cuePathDir * proj;
                    float perpDist = (obPos - closestPt).magnitude;
                    if (perpDist < BALL_DIAMETER)
                    {
                        roadblockDetected = true;
                        roadblockPerpDist = perpDist;
                        roadblockBall = ob;
                    }
                }
            }

            if (scratchRisk || roadblockDetected)
            {
                float finalSpin = bestSpin;
                float powerMul = 1.0f;

                if (scratchRisk && roadblockDetected)
                {
                    // Both triggered: use the most aggressive single correction
                    finalSpin = -0.8f;
                    powerMul = 0.55f;
                    _Log("[NPC] 犯规预测: 母球近袋" + scratchPocket
                        + " 距=" + (scratchDist * 100f).ToString("F1") + "cm"
                        + " + 路障球" + roadblockBall
                        + " 距=" + (roadblockPerpDist * 100f).ToString("F1") + "cm"
                        + " → spin=" + finalSpin.ToString("F2") + " 力度×" + powerMul.ToString("F2") + " (合并修正)");
                }
                else if (scratchRisk)
                {
                    if (scratchDist < 0.12f)
                    {
                        finalSpin = -0.7f;
                        powerMul = 0.60f;
                    }
                    else
                    {
                        finalSpin = Mathf.Min(bestSpin, -0.5f);
                        powerMul = 0.8f;
                    }
                    _Log("[NPC] 犯规预测: 母球可能落袋! 近袋" + scratchPocket
                        + " 距=" + (scratchDist * 100f).ToString("F1") + "cm"
                        + " → spin=" + finalSpin.ToString("F2") + " 力度×" + powerMul.ToString("F2"));
                }
                else // roadblock only
                {
                    finalSpin = Mathf.Min(bestSpin, -0.7f);
                    powerMul = 0.75f;
                    _Log("[NPC] 犯规预测: 母球路径可能碰到球" + roadblockBall
                        + " 距=" + (roadblockPerpDist * 100f).ToString("F1") + "cm"
                        + " → spin=" + finalSpin.ToString("F2") + " 力度×" + powerMul.ToString("F2"));
                }

                bestSpin = finalSpin;
                npcPower *= powerMul;
            }
        }

        // === Gizmo visualization ===
        _VisualizeShot(cuePos, bestBall, bestPocket, bestAimDir, bestShotDist, bestShotType);

        return true;
    }

    // === Gizmo visualization for NPC shot calculation ===
#if NPC_GIZMOS
    public GameObject gizmoLinePrefab; // Assign empty GO with LineRenderer in Inspector
    private GameObject[] _gizmosPool = new GameObject[32];
    private int _gizmosCount;

    private void _ClearGizmos()
    {
        for (int i = 0; i < _gizmosCount; i++)
        {
            if (_gizmosPool[i] != null) Destroy(_gizmosPool[i]);
        }
        _gizmosCount = 0;
    }

    private GameObject _MakeLineGO(Vector3 from, Vector3 to, Color color, float width = 0.003f)
    {
        if (_gizmosCount >= _gizmosPool.Length || gizmoLinePrefab == null) return null;
        GameObject go = VRCInstantiate(gizmoLinePrefab);
        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
        }
        go.SetActive(true);
        _gizmosPool[_gizmosCount++] = go;
        return go;
    }

    private void _DrawLine(Vector3 from, Vector3 to, Color color, float width = 0.003f)
    {
        if ((to - from).magnitude < 0.001f) return;
        _MakeLineGO(from, to, color, width);
    }

    private void _DrawDot(Vector3 pos, float radius, Color color)
    {
        float s = radius;
        _MakeLineGO(pos + Vector3.right * s, pos - Vector3.right * s, color, 0.004f);
        _MakeLineGO(pos + Vector3.forward * s, pos - Vector3.forward * s, color, 0.004f);
        _MakeLineGO(pos + Vector3.up * s, pos - Vector3.up * s, color, 0.004f);
    }

    private void _VisualizeShot(Vector3 cuePos, int bestBall, int bestPocket, Vector3 aimDir, float shotDist, int shotType)
    {
        if (gizmoLinePrefab == null) return;
        _ClearGizmos();

        // Ball center height in world space: tableSurface.position.y (ballsP.y = 0)
        float y = table.tableSurface != null ? table.tableSurface.position.y : 0.03f;

        // All positions are table-local; convert to world for LineRenderer
        Vector3 ghostBall = cuePos + aimDir * shotDist;
        _DrawDot(_L2W(WithY(cuePos, y)), 0.012f, Color.blue);

        if (bestPocket >= 0)
        {
            Vector3 ballPos = table.ballsP[bestBall];
            Vector3 pocketPos = npcPockets[bestPocket];

            if (shotType == 1)
            {
                // Bank shot: try single-cushion first, then two-cushion
                bool drawn = false;
                // Single cushion
                for (int c = 0; c < 4; c++)
                {
                    Vector3 reflected = _ReflectPocket(pocketPos, c);
                    // Apply friction offset: shift toward ball travel direction
                    Vector3 bankRaw = (reflected - ballPos).normalized;
                    float ballToCushionDist = (reflected - ballPos).magnitude;
                    float baseFriction = 0.08f;
                    float frictionScale = Mathf.Clamp(ballToCushionDist * 0.12f, 0f, 0.08f);
                    float frictionOff = baseFriction + frictionScale;
                    switch (c)
                    {
                        case 0:
                        case 1:
                            reflected.x += Mathf.Sign(bankRaw.x) * frictionOff;
                            break;
                        case 2:
                        case 3:
                            reflected.z += Mathf.Sign(bankRaw.z) * frictionOff;
                            break;
                    }
                    Vector3 bp = _GetCushionBouncePoint(ballPos, reflected, c);
                    if (bp.x != float.MaxValue && _IsCushionBouncePointValid(bp, c))
                    {
                        _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(bp, y)), Color.red, 0.004f);
                        _DrawLine(_L2W(WithY(bp, y)), _L2W(WithY(pocketPos, y)), Color.yellow, 0.004f);
                        _DrawDot(_L2W(WithY(bp, y)), 0.012f, Color.red);
                        drawn = true;
                        break;
                    }
                }
                // Two-cushion
                if (!drawn)
                {
                    for (int c1 = 0; c1 < 4 && !drawn; c1++)
                    {
                        for (int c2 = 0; c2 < 4 && !drawn; c2++)
                        {
                            if (c1 == c2) continue;
                            Vector3 reflected1 = _ReflectPocket(pocketPos, c2);
                            Vector3 reflected2 = _ReflectPocket(reflected1, c1);
                            Vector3 b1 = _GetCushionBouncePoint(ballPos, reflected2, c1);
                            if (b1.x == float.MaxValue || !_IsCushionBouncePointValid(b1, c1)) continue;
                            Vector3 b2 = _GetCushionBouncePoint(b1, reflected1, c2);
                            if (b2.x == float.MaxValue || !_IsCushionBouncePointValid(b2, c2)) continue;
                            _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(b1, y)), Color.red, 0.004f);
                            _DrawLine(_L2W(WithY(b1, y)), _L2W(WithY(b2, y)), Color.red, 0.004f);
                            _DrawLine(_L2W(WithY(b2, y)), _L2W(WithY(pocketPos, y)), Color.yellow, 0.004f);
                            _DrawDot(_L2W(WithY(b1, y)), 0.012f, Color.red);
                            _DrawDot(_L2W(WithY(b2, y)), 0.012f, Color.red);
                            drawn = true;
                        }
                    }
                }
                if (!drawn)
                {
                    _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(pocketPos, y)), Color.red, 0.004f);
                }
            }
            else
            {
                // Direct shot: ball → pocket
                _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(pocketPos, y)), Color.red, 0.004f);
            }

            _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(ghostBall, y)), Color.green);
            _DrawDot(_L2W(WithY(ghostBall, y)), 0.010f, Color.yellow);
            _DrawLine(_L2W(WithY(ghostBall, y)), _L2W(WithY(ballPos, y)), Color.white);
        }
        else if (shotType == 2 && bestBall >= 0)
        {
            // ===== KICK SHOT (勾库/K球) — cue ball bounces off cushion before hitting target =====
            Vector3 ballPos = table.ballsP[bestBall];
            Vector3 kickGhost = Vector3.zero;

            if (npcKickCushion2 >= 0)
            {
                // Two-cushion kick: use the same double-reflection geometry as selection
                Vector3 reflected1 = _ReflectPoint(ballPos, npcKickCushion2);
                Vector3 reflected2 = _ReflectPoint(reflected1, npcKickCushion);
                Vector3 bounce1 = _GetCushionPoint(cuePos, reflected2, npcKickCushion);
                Vector3 bounce2 = (bounce1.x != float.MaxValue)
                    ? _GetCushionPoint(bounce1, reflected1, npcKickCushion2)
                    : new Vector3(float.MaxValue, 0, 0);
                if (bounce1.x != float.MaxValue && bounce2.x != float.MaxValue)
                {
                    _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(bounce1, y)), Color.cyan, 0.005f);
                    _DrawLine(_L2W(WithY(bounce1, y)), _L2W(WithY(bounce2, y)), Color.cyan, 0.005f);
                    _DrawLine(_L2W(WithY(bounce2, y)), _L2W(WithY(ballPos, y)), Color.white, 0.004f);
                    _DrawDot(_L2W(WithY(bounce1, y)), 0.014f, Color.cyan);
                    _DrawDot(_L2W(WithY(bounce2, y)), 0.014f, Color.cyan);
                    kickGhost = bounce1;
                    _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(kickGhost, y)), Color.green);
                    _DrawDot(_L2W(WithY(kickGhost, y)), 0.010f, Color.yellow);
                    return;
                }
            }

            if (npcKickCushion >= 0)
            {
                // Single cushion kick: use saved cushion
                Vector3 cushionPoint = _GetCushionPoint(cuePos, ballPos, npcKickCushion);
                if (cushionPoint.x != float.MaxValue)
                {
                    _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(cushionPoint, y)), Color.cyan, 0.005f);
                    _DrawLine(_L2W(WithY(cushionPoint, y)), _L2W(WithY(ballPos, y)), Color.white, 0.004f);
                    _DrawDot(_L2W(WithY(cushionPoint, y)), 0.014f, Color.cyan);
                    kickGhost = cushionPoint;
                    _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(kickGhost, y)), Color.green);
                    _DrawDot(_L2W(WithY(kickGhost, y)), 0.010f, Color.yellow);
                    return;
                }
            }

            // Fallback: direct line
            _DrawLine(_L2W(WithY(cuePos, y)), _L2W(WithY(ballPos, y)), Color.white);
        }

        for (int i = 0; i < 6; i++)
        {
            // Original T-point (green)
            _DrawDot(_L2W(WithY(npcPocketsOriginal[i], y)), 0.008f, Color.green);
            // Offset T-point (cyan / magenta if selected)
            Color pc = (i == bestPocket) ? Color.magenta : Color.cyan;
            float size = (i == bestPocket) ? 0.015f : 0.010f;
            _DrawDot(_L2W(WithY(npcPockets[i], y)), size, pc);
        }
    }

    private void _VisualizeTPoints()
    {
        if (gizmoLinePrefab == null) return;
        float y = table.tableSurface != null ? table.tableSurface.position.y : 0.03f;
        for (int i = 0; i < 6; i++)
        {
            _DrawDot(_L2W(WithY(npcPocketsOriginal[i], y)), 0.008f, Color.green);
            _DrawDot(_L2W(WithY(npcPockets[i], y)), 0.010f, Color.cyan);
        }
    }
#else
    private void _VisualizeShot(Vector3 cuePos, int bestBall, int bestPocket, Vector3 aimDir, float shotDist, int shotType) { }
    private void _VisualizeTPoints() { }
#endif

    private Vector3 WithY(Vector3 v, float y) { return new Vector3(v.x, y, v.z); }

    // Convert table-local position to world position for LineRenderer (UseWorldSpace)
    private Vector3 _L2W(Vector3 localPos)
    {
        return table.transform.TransformPoint(localPos);
    }

    // Position play: predict cue ball endpoint and score next-shot availability
    private float _EvalPositionPlay(Vector3 cuePos, Vector3 aimDir, Vector3 ballPos, Vector3 t2pDir, float cutAngle, uint targetBalls, int excludeBall)
    {
        float bestNextScore = -1f;
        float bestSpinChoice = 0f;

        float sinCut = Mathf.Sin(cutAngle);
        float cosCut = Mathf.Cos(cutAngle);
        Vector3 perpDir = new Vector3(-aimDir.z, 0f, aimDir.x); // perpendicular in table plane

        float shotDist = (ballPos - cuePos).magnitude;
        bool isShort = shotDist < 0.3f;
        bool isStraight = Mathf.Abs(cutAngle) < 0.15f;

        // Check if target ball is near a pocket (high scratch risk for straight shots)
        bool targetNearPocket = false;
        for (int p = 0; p < 6; p++)
        {
            if ((npcPockets[p] - ballPos).magnitude < 0.35f) { targetNearPocket = true; break; }
        }

        // Build spin candidates
        int spinCount;
        float[] spins;
        if (isShort && isStraight)
        {
            if (targetNearPocket)
            {
                // Straight shot near pocket: very strong draw to prevent follow-scratch
                spins = new float[] { -0.6f, -0.4f, -0.2f };
                spinCount = 3;
            }
            else
            {
                // Short straight: strong draw, stun, mild follow
                spins = new float[] { -0.65f, -0.4f, 0f, 0.3f };
                spinCount = 4;
            }
        }
        else if (isShort)
        {
            // Short cut: draw, stun, follow
            spins = new float[] { -0.3f, 0f, 0.4f };
            spinCount = 3;
        }
        else
        {
            // Long shot: stun, light follow
            spins = new float[] { 0f, 0.2f };
            spinCount = 2;
        }

        for (int si = 0; si < spinCount; si++)
        {
            float spin = spins[si];
            Vector3 cueEndpoint;

            if (Mathf.Abs(cutAngle) < 0.1f)
            {
                // Near-straight shot: follow goes forward, draw comes back, stun stops
                // Conservative estimates to avoid over-scoring follow near pockets
                float travelDist = spin > 0f ? 0.15f : (spin < 0f ? -0.15f : 0.02f);
                cueEndpoint = ballPos + aimDir * travelDist;
            }
            else
            {
                // Cut shot: cue ball deflects ~90° from contact line (stun)
                Vector3 deflectDir = (aimDir - t2pDir * Vector3.Dot(aimDir, t2pDir)).normalized;
                float deflectDist = sinCut * 0.5f;
                float forwardComp = spin * 0.4f;
                cueEndpoint = ballPos + deflectDir * deflectDist + aimDir * forwardComp;
            }

            // Clamp to table bounds
            cueEndpoint.x = Mathf.Clamp(cueEndpoint.x, -table.k_TABLE_WIDTH + 0.05f, table.k_TABLE_WIDTH - 0.05f);
            cueEndpoint.z = Mathf.Clamp(cueEndpoint.z, -table.k_TABLE_HEIGHT + 0.05f, table.k_TABLE_HEIGHT - 0.05f);

            // === Scratch risk: if cue ball ends up in any pocket, reject ===
            float scratchPenalty = 0f;
            for (int p = 0; p < 6; p++)
            {
                float distToPocket = (cueEndpoint - table.pocketLocations[p]).magnitude;
                if (distToPocket < table.k_INNER_RADIUS_CORNER + BALL_RADIUS * 3f)
                {
                    scratchPenalty = 100f; // effectively reject this spin
                    break;
                }
            }

            // === Cue ball travel distance: less movement = more control = better ===
            float cueTravel = (cueEndpoint - ballPos).magnitude;

            float nextScore = _ScoreNextShot(cueEndpoint, targetBalls, excludeBall);
            nextScore -= scratchPenalty;
            // Penalize long cue ball travel: 5cm=0% penalty, 40cm=40% penalty
            float travelPenalty = Mathf.Clamp01(cueTravel / 0.4f) * 0.4f;
            nextScore *= (1f - travelPenalty);
            if (nextScore > bestNextScore)
            {
                bestNextScore = nextScore;
                bestSpinChoice = spin;
            }
        }

        _posPlaySpin = bestSpinChoice;
        return bestNextScore;
    }

    // Score how good the next shot would be from a given cue position
    // Higher = easier to pocket + good position for the shot after
    private float _ScoreNextShot(Vector3 futurePos, uint targetBalls, int excludeBall)
    {
        float best = 0f;
        int viableCount = 0; // how many makeable shots exist from this position

        // Check if future position is near any pocket (scratch risk)
        bool futureNearPocket = false;
        for (int p = 0; p < 6; p++)
        {
            if ((futurePos - table.pocketLocations[p]).magnitude < table.k_INNER_RADIUS_CORNER + BALL_RADIUS * 3f)
            { futureNearPocket = true; break; }
        }

        for (int b = 1; b <= 15; b++)
        {
            if (b == excludeBall) continue;
            if ((targetBalls & (1u << b)) == 0) continue;
            if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
            Vector3 ballPos = table.ballsP[b];

            for (int p = 0; p < 6; p++)
            {
                float ballToPocket = (npcPockets[p] - ballPos).magnitude;
                if (ballToPocket > 1.5f) continue;
                Vector3 t2pDir = (npcPockets[p] - ballPos).normalized;
                Vector3 ghostBall = ballPos - t2pDir * BALL_DIAMETER;

                // Ghost ball must be on table
                if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH - BALL_RADIUS
                    || Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT - BALL_RADIUS) continue;

                Vector3 toGhost = ghostBall - futurePos;
                float dist = toGhost.magnitude;
                if (dist < 0.08f || dist > 2.0f) continue;

                // Path must be clear
                if (!_IsPathClear(futurePos, ghostBall, b)) continue;

                // Target ball path to pocket must not cross cushion
                if (_IsPathCrossesCushion(ballPos, npcPockets[p])) continue;

                float alignment = Vector3.Dot(toGhost / dist, t2pDir);
                if (alignment < 0.3f) continue;

                // Calculate cut angle for this hypothetical next shot
                Vector3 aimDirNext = toGhost / dist;
                float cutAngleNext = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDirNext, t2pDir), -1f, 1f));

                // Score components:
                // 1. Alignment (higher = straighter shot = easier)
                float alignScore = alignment * 2.0f;
                // 2. Distance (closer = easier, sweet spot 0.15-0.4m)
                float distScore = Mathf.Clamp01(1.0f - dist / 1.5f) * 0.5f;
                // 3. Cut angle penalty (big cuts are hard — exponential penalty)
                float cutPenalty = cutAngleNext * cutAngleNext * 2.0f;
                // 4. Short straight bonus (easy shots worth more for running)
                float easyBonus = (cutAngleNext < 0.15f && dist < 0.4f) ? 0.5f : 0f;
                // 5. Scratch risk penalty: if future position is near pocket, heavy penalty
                float scratchPenalty = futureNearPocket ? 1.5f : 0f;

                float score = alignScore + distScore - cutPenalty + easyBonus - scratchPenalty;
                if (score > best) best = score;
                viableCount++;
            }
        }

        // Bonus for having multiple viable shots (position is flexible)
        if (viableCount >= 3) best += 0.3f;
        else if (viableCount >= 2) best += 0.15f;

        return best;
    }

    // Reflect pocket position across a cushion for bank shot calculation
    // Target ball travels toward reflected pocket, bounces off cushion, goes into real pocket
    private Vector3 _ReflectPocket(Vector3 pocketPos, int cushion)
    {
        switch (cushion)
        {
            case 0: return new Vector3(pocketPos.x, 0f, 2f * table.k_TABLE_HEIGHT - pocketPos.z); // top (z = +k_H)
            case 1: return new Vector3(pocketPos.x, 0f, -2f * table.k_TABLE_HEIGHT - pocketPos.z); // bottom (z = -k_H)
            case 2: return new Vector3(-2f * table.k_TABLE_WIDTH - pocketPos.x, 0f, pocketPos.z); // left (x = -k_W)
            case 3: return new Vector3(2f * table.k_TABLE_WIDTH - pocketPos.x, 0f, pocketPos.z); // right (x = +k_W)
            default: return pocketPos;
        }
    }

    // Reflect a point across a cushion
    // Cushion friction reduces reflection angle ~15% vs geometric mirror.
    // Compensate by pushing the virtual reflected point further from the cushion.
    private const float CUSHION_FRICTION_COMP = 0.10f;

    private Vector3 _ReflectPoint(Vector3 point, int cushion)
    {
        switch (cushion)
        {
            case 0: // top (z = +k_TABLE_HEIGHT)
            {
                float z = 2f * table.k_TABLE_HEIGHT - point.z;
                z += (z - point.z) * CUSHION_FRICTION_COMP;
                return new Vector3(point.x, 0f, z);
            }
            case 1: // bottom (z = -k_TABLE_HEIGHT)
            {
                float z = -2f * table.k_TABLE_HEIGHT - point.z;
                z += (z - point.z) * CUSHION_FRICTION_COMP;
                return new Vector3(point.x, 0f, z);
            }
            case 2: // left (x = -k_TABLE_WIDTH)
            {
                float x = -2f * table.k_TABLE_WIDTH - point.x;
                x += (x - point.x) * CUSHION_FRICTION_COMP;
                return new Vector3(x, 0f, point.z);
            }
            case 3: // right (x = +k_TABLE_WIDTH)
            {
                float x = 2f * table.k_TABLE_WIDTH - point.x;
                x += (x - point.x) * CUSHION_FRICTION_COMP;
                return new Vector3(x, 0f, point.z);
            }
            default: return point;
        }
    }

    // Calculate where cue ball hits the cushion on its way to target (for kick shots)
    private Vector3 _GetCushionPoint(Vector3 cuePos, Vector3 target, int cushion)
    {
        Vector3 reflected = _ReflectPoint(cuePos, cushion);
        Vector3 dir = (target - reflected).normalized;
        float t = float.MaxValue;
        Vector3 result = new Vector3(float.MaxValue, 0f, 0f);

        switch (cushion)
        {
            case 0: // top cushion (z = +TABLE_HEIGHT)
                if (Mathf.Abs(dir.z) > 0.001f) { t = (table.k_TABLE_HEIGHT - reflected.z) / dir.z; }
                break;
            case 1: // bottom cushion (z = -TABLE_HEIGHT)
                if (Mathf.Abs(dir.z) > 0.001f) { t = (-table.k_TABLE_HEIGHT - reflected.z) / dir.z; }
                break;
            case 2: // left cushion (x = -TABLE_WIDTH)
                if (Mathf.Abs(dir.x) > 0.001f) { t = (-table.k_TABLE_WIDTH - reflected.x) / dir.x; }
                break;
            case 3: // right cushion (x = +TABLE_WIDTH)
                if (Mathf.Abs(dir.x) > 0.001f) { t = (table.k_TABLE_WIDTH - reflected.x) / dir.x; }
                break;
        }

        if (t > 0f && t < 10f)
        {
            result = reflected + dir * t;
            result.y = 0f;
        }
        return result;
    }

    // Calculate where target ball hits the cushion on its way to reflected pocket (for bank shots)
    private Vector3 _GetCushionBouncePoint(Vector3 ballPos, Vector3 reflectedPocket, int cushion)
    {
        Vector3 dir = (reflectedPocket - ballPos).normalized;
        float t = float.MaxValue;
        Vector3 result = new Vector3(float.MaxValue, 0f, 0f);

        switch (cushion)
        {
            case 0: // top cushion (z = +TABLE_HEIGHT)
                if (Mathf.Abs(dir.z) > 0.001f) { t = (table.k_TABLE_HEIGHT - ballPos.z) / dir.z; }
                break;
            case 1: // bottom cushion (z = -TABLE_HEIGHT)
                if (Mathf.Abs(dir.z) > 0.001f) { t = (-table.k_TABLE_HEIGHT - ballPos.z) / dir.z; }
                break;
            case 2: // left cushion (x = -TABLE_WIDTH)
                if (Mathf.Abs(dir.x) > 0.001f) { t = (-table.k_TABLE_WIDTH - ballPos.x) / dir.x; }
                break;
            case 3: // right cushion (x = +TABLE_WIDTH)
                if (Mathf.Abs(dir.x) > 0.001f) { t = (table.k_TABLE_WIDTH - ballPos.x) / dir.x; }
                break;
        }

        if (t > 0f && t < 10f)
        {
            result = ballPos + dir * t;
            result.y = 0f;
        }
        return result;
    }

    private uint _GetTargetBalls()
    {
        uint pocketed = table.ballsPocketedLocal;
        uint remaining = ~pocketed & 0xFFFEu & ~0x2u; // exclude cue(0) and 8-ball(1)

        if (table.isTableOpenLocal)
        {
            npcGroupId = -1;
            return remaining;
        }

        // Always recalculate from current teamIdLocal — test mode NPC plays both teams
        npcGroupId = (int)(table.teamIdLocal ^ table.teamColorLocal);
        uint groupMask = ((uint)npcGroupId == 0) ? 0x1FCu : 0xFE00u;
        uint result = remaining & groupMask;

        // When all group balls cleared, always include 8-ball if it's on the table
        // Don't trust pocketed flag — just check physical position
        if (result == 0)
        {
            Vector3 b1 = table.ballsP[1];
            bool ball1OnTable = Mathf.Abs(b1.x) < table.k_TABLE_WIDTH && Mathf.Abs(b1.z) < table.k_TABLE_HEIGHT;
            if (ball1OnTable)
            {
                result |= 0x2u; // add 8-ball (ball 1)
                _Log("[NPC] 组球清完,加入8-ball  ball1pos=" + table.ballsP[1].ToString("F3") + " pocketed=0x" + pocketed.ToString("X8"));
            }
        }

        return result;
    }

    private bool _IsAllBallsOnRightHalf()
    {
        for (int i = 2; i <= 15; i++)
        {
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            if (table.ballsP[i].x <= 0f) return false;
        }
        return true;
    }

    private bool _IsPathClear(Vector3 from, Vector3 to, int excludeBall)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.001f) return true;
        Vector3 ndir = dir / dist;

        for (int i = 1; i <= 15; i++)
        {
            if (i == excludeBall) continue;
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            Vector3 oc = table.ballsP[i] - from;
            float dot = Vector3.Dot(oc, ndir);
            if (dot < 0f || dot > dist) continue;
            Vector3 closest = from + ndir * dot;
            float perpDist = (table.ballsP[i] - closest).sqrMagnitude;
            if (perpDist < PATH_CLEARANCE * PATH_CLEARANCE) return false;
        }
        // Check table boundaries: cue ball path must stay within cushion rails
        if (!_IsPathInBounds(from, to)) return false;
        return true;
    }

    // Like _IsPathClear but skips bounds check — used for kick shots where endpoint is on cushion
    private bool _IsPathClearBallsOnly(Vector3 from, Vector3 to, int excludeBall)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.001f) return true;
        Vector3 ndir = dir / dist;

        for (int i = 1; i <= 15; i++)
        {
            if (i == excludeBall) continue;
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            Vector3 oc = table.ballsP[i] - from;
            float dot = Vector3.Dot(oc, ndir);
            if (dot < 0f || dot > dist) continue;
            Vector3 closest = from + ndir * dot;
            float perpDist = (table.ballsP[i] - closest).sqrMagnitude;
            if (perpDist < PATH_CLEARANCE * PATH_CLEARANCE) return false;
        }
        return true;
    }

    // Check if a line segment stays within table boundaries
    private bool _IsPathInBounds(Vector3 from, Vector3 to)
    {
        float maxX = table.k_TABLE_WIDTH - BALL_RADIUS;
        float maxZ = table.k_TABLE_HEIGHT - BALL_RADIUS;
        // Check 4 sample points including endpoint (ghost ball must be on table)
        for (int s = 1; s <= 4; s++)
        {
            float t = s * 0.25f;
            Vector3 p = from + (to - from) * t;
            if (Mathf.Abs(p.x) > maxX || Mathf.Abs(p.z) > maxZ) return false;
        }
        return true;
    }

    // Check if path from cue ball to ghost ball crosses any table cushion
    private bool _IsPathCrossesCushion(Vector3 from, Vector3 to)
    {
        // Cushion boundaries (actual rail positions, not pocket positions)
        float tw = table.k_TABLE_WIDTH;   // half table width
        float th = table.k_TABLE_HEIGHT;  // half table height
        float sw = 0.08f;                 // approximate pocket opening half-width

        // 4 cushion segments (excluding pocket openings)
        // Top edge (z = +th): from left side pocket to right side pocket
        if (_SegCrossSeg(from, to, new Vector3(-tw + sw, 0, th), new Vector3(tw - sw, 0, th))) return true;
        // Bottom edge (z = -th)
        if (_SegCrossSeg(from, to, new Vector3(-tw + sw, 0, -th), new Vector3(tw - sw, 0, -th))) return true;
        // Right edge (x = +tw): from bottom corner to top corner
        if (_SegCrossSeg(from, to, new Vector3(tw, 0, -th + sw), new Vector3(tw, 0, th - sw))) return true;
        // Left edge (x = -tw)
        if (_SegCrossSeg(from, to, new Vector3(-tw, 0, -th + sw), new Vector3(-tw, 0, th - sw))) return true;

        return false;
    }

    // Check if ball's path to pocket crosses the pocket jaw (cushion near opening)
    // This is the REAL reason balls rattle: path clips the jaw on the way in
    // pocketCenter = actual pocket center position (NOT the T-point)
    // cutAngle = angle between cue->target and target->pocket (radians)
    private bool _BallApproachBadAngle(Vector3 ballPos, Vector3 pocketTPoint, Vector3 pocketCenter, float cutAngle)
    {
        // Opening direction: from pocket center toward table center (inward)
        Vector3 openDir = (Vector3.zero - pocketCenter).normalized;

        // Approach angle check: ball must approach from a direction that can enter the pocket
        // Use T-point (where ball is actually aimed) not pocket center — for corner pockets
        // the T-point is offset 35% toward side pocket, which changes the approach angle
        Vector3 approachDir = (pocketTPoint - ballPos).normalized;
        float alignWithPocket = -Vector3.Dot(approachDir, openDir);

        // Cushion-parallel exception: ball near cushion traveling parallel can slide into pocket
        float halfW = table.k_TABLE_WIDTH;
        float halfH = table.k_TABLE_HEIGHT;
        float nearEdge = BALL_RADIUS * 3f; // within ~8.6cm of cushion
        if (ballPos.x > halfW - nearEdge || ballPos.x < -halfW + nearEdge
            || ballPos.z > halfH - nearEdge || ballPos.z < -halfH + nearEdge)
        {
            Vector3 cushionNormal = Vector3.zero;
            if (Mathf.Abs(ballPos.x) > halfW - nearEdge)
                cushionNormal = new Vector3(ballPos.x > 0 ? 1f : -1f, 0, 0);
            else
                cushionNormal = new Vector3(0, 0, ballPos.z > 0 ? 1f : -1f);
            float perpComponent = Mathf.Abs(Vector3.Dot(approachDir, cushionNormal));
            if (perpComponent < 0.3f) return false;
        }

        // Only reject if ball is clearly approaching from wrong direction (align < 0.2)
        if (alignWithPocket < 0.2f) return true;

        return false;
    }

    // Check if a cushion bounce point is within the valid cushion segment (between two pocket openings)
    private bool _IsCushionBouncePointValid(Vector3 point, int cushion)
    {
        float tw = table.k_TABLE_WIDTH;
        float th = table.k_TABLE_HEIGHT;
        float sw = 0.08f; // pocket opening half-width
        switch (cushion)
        {
            case 0: // top (z = +th)
                return point.x > -tw + sw && point.x < tw - sw;
            case 1: // bottom (z = -th)
                return point.x > -tw + sw && point.x < tw - sw;
            case 2: // left (x = -tw)
                return point.z > -th + sw && point.z < th - sw;
            case 3: // right (x = +tw)
                return point.z > -th + sw && point.z < th - sw;
        }
        return false;
    }

    // Line segment intersection test (2D in XZ plane)
    private bool _SegCrossSeg(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
    {
        Vector2 d1 = new Vector2(a2.x - a1.x, a2.z - a1.z);
        Vector2 d2 = new Vector2(b2.x - b1.x, b2.z - b1.z);
        float cross = d1.x * d2.y - d1.y * d2.x;
        if (Mathf.Abs(cross) < 1e-6f) return false;
        Vector2 d3 = new Vector2(b1.x - a1.x, b1.z - a1.z);
        float t = (d3.x * d2.y - d3.y * d2.x) / cross;
        float u = (d3.x * d1.y - d3.y * d1.x) / cross;
        return t > 0.01f && t < 0.99f && u > 0.01f && u < 0.99f;
    }

    // pocketCenter = actual pocket center (for jaw check); pocketPos = T-point aim target (for path)
    private bool _IsBallToPocketBlocked(Vector3 ballPos, Vector3 pocketPos, int excludeBall, float cutAngle, Vector3 pocketCenter)
    {

        Vector3 dir = pocketPos - ballPos;
        float len = dir.magnitude;
        if (len < 0.001f) return false;
        Vector3 ndir = dir / len;

        if (_BallApproachBadAngle(ballPos, pocketPos, pocketCenter, cutAngle)) return true;

        // Clearance: two ball radii (center-to-center must not overlap)
        float clearSqr = BALL_DIAMETER * BALL_DIAMETER;
        for (int i = 1; i <= 15; i++)
        {
            if (i == excludeBall) continue;
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            Vector3 oc = table.ballsP[i] - ballPos;
            float along = Vector3.Dot(oc, ndir);
            if (along < 0f || along > len) continue;
            Vector3 closest = ballPos + ndir * along;
            float perpDist = (table.ballsP[i] - closest).sqrMagnitude;
            if (perpDist < clearSqr) return true;
        }
        return false;
    }

    private void _NpcFireSafetyShot()
    {
        Vector3 cuePos = table.ballsP[0];
        uint targetBalls = _GetTargetBalls();
        if (targetBalls == 0) targetBalls = ~table.ballsPocketedLocal & 0xFFFEu;

        // Opponent balls: the group we are NOT playing
        uint opponentBalls = 0u;
        if (npcGroupId == 0) opponentBalls = ~table.ballsPocketedLocal & 0xFE00u & ~0x2u;
        else if (npcGroupId == 1) opponentBalls = ~table.ballsPocketedLocal & 0x1FCu & ~0x2u;

        // Evaluate each target ball: predict cue ball endpoint, score how safe it is
        float bestSafetyScore = float.MinValue;
        int bestBall = -1;
        Vector3 bestCueEndpoint = cuePos;

        for (int b = 1; b <= 15; b++)
        {
            if ((targetBalls & (1u << b)) == 0) continue;
            if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
            if (!_IsPathClear(cuePos, table.ballsP[b], b)) continue;

            Vector3 ballPos = table.ballsP[b];
            Vector3 aimDir = (ballPos - cuePos).normalized;
            float dist = (ballPos - cuePos).magnitude;

            // Predict cue ball endpoint after contact (tangent line)
            Vector3 ballToPocket = Vector3.zero;
            float minBPdist = float.MaxValue;
            for (int p = 0; p < 6; p++)
            {
                float d = (npcPockets[p] - ballPos).magnitude;
                if (d < minBPdist) { minBPdist = d; ballToPocket = (npcPockets[p] - ballPos).normalized; }
            }
            float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(-aimDir, ballToPocket), -1f, 1f));
            Vector3 tangent = new Vector3(-aimDir.z, 0f, aimDir.x);
            if (Vector3.Dot(tangent, ballToPocket) < 0f) tangent = -tangent;
            Vector3 cueEndpoint = ballPos + tangent * dist * Mathf.Clamp01(1f - cutAngle / 1.57f);

            // Safety score: minimize opponent's opportunities
            // 1. Distance to nearest opponent ball (farther = better)
            float minOppDist = float.MaxValue;
            for (int ob = 1; ob <= 15; ob++)
            {
                if ((opponentBalls & (1u << ob)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << ob)) != 0) continue;
                float d = (cueEndpoint - table.ballsP[ob]).magnitude;
                if (d < minOppDist) minOppDist = d;
            }
            // 2. Count opponent balls with clear path to pocket from cue endpoint
            int oppOpenCount = 0;
            for (int ob = 1; ob <= 15; ob++)
            {
                if ((opponentBalls & (1u << ob)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << ob)) != 0) continue;
                for (int p = 0; p < 6; p++)
                {
                    if (_IsPathClear(cueEndpoint, table.ballsP[ob], ob)
                        && _IsPathClear(table.ballsP[ob], npcPockets[p], ob))
                    { oppOpenCount++; break; }
                }
            }

            float score = minOppDist * 2.0f - oppOpenCount * 0.5f;
            if (score > bestSafetyScore)
            {
                bestSafetyScore = score;
                bestBall = b;
                bestCueEndpoint = cueEndpoint;
            }
        }

        // Fallback: if no clear-path ball found, try cushion bounce or closest (after 3 failures)
        if (bestBall < 0)
        {
            // If ball positions are corrupted (all outside table), don't attempt safety shot
            // — it would just cause more corruption. Let _NpcTick handle the reset.
            bool anyBallOutOfBounds = false;
            for (int i = 2; i <= 15; i++)
            {
                if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
                Vector3 p = table.ballsP[i];
                if (Mathf.Abs(p.x) > table.k_TABLE_WIDTH + 0.2f
                    || Mathf.Abs(p.z) > table.k_TABLE_HEIGHT + 0.2f
                    || Mathf.Abs(p.y) > 2.0f)
                {
                    anyBallOutOfBounds = true;
                    break;
                }
            }
            if (anyBallOutOfBounds)
            {
                _Log("[NPC] 球位置腐败,跳过安全球");
                return; // let _NpcTick handle the reset on next frame
            }

            _safetyFailCount++;
            _Log("[NPC] 无法击球 (第" + _safetyFailCount + "次)");
            if (_safetyFailCount >= 3)
            {
                _safetyFailCount = 0;
                // 1st try: closest ball with clear path
                float bestDist = float.MaxValue;
                for (int b = 1; b <= 15; b++)
                {
                    if ((targetBalls & (1u << b)) == 0) continue;
                    if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                    if (!_IsPathClear(cuePos, table.ballsP[b], b)) continue;
                    float d = (table.ballsP[b] - cuePos).sqrMagnitude;
                    if (d < bestDist) { bestDist = d; bestBall = b; }
                }
                // 2nd try: cushion bounce (勾库) to hit own ball — avoids foul
                if (bestBall < 0)
                {
                    float bestKickScore = float.MinValue;
                    for (int b = 1; b <= 15; b++)
                    {
                        if ((targetBalls & (1u << b)) == 0) continue;
                        if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                        Vector3 ballPos = table.ballsP[b];
                        for (int cushion = 0; cushion < 4; cushion++)
                        {
                            Vector3 cushionPoint = _GetCushionPoint(cuePos, ballPos, cushion);
                            if (cushionPoint.x == float.MaxValue) continue;
                            if (!_IsPathClear(cuePos, cushionPoint, -1)) continue;
                            if (!_IsPathClear(cushionPoint, ballPos, b)) continue;
                            float totalDist = (cushionPoint - cuePos).magnitude + (ballPos - cushionPoint).magnitude;
                            if (totalDist > 2.5f) continue;
                            float score = 1.0f - totalDist / 2.5f;
                            if (score > bestKickScore)
                            {
                                bestKickScore = score;
                                bestBall = b;
                                bestCueEndpoint = cushionPoint;
                            }
                        }
                    }
                    if (bestBall >= 0)
                    {
                        Vector3 kickAim = (bestCueEndpoint - cuePos).normalized;
                        float kickDist = (bestCueEndpoint - cuePos).magnitude;
                        npcAimDir = kickAim;
                        npcPower = Mathf.Clamp(kickDist * 1.5f / 4.0f, MIN_POWER, 0.25f);
                        npcSpinValue = 0f;
                        npcTargetBall = bestBall;
                        npcTargetPocket = -1;
                        npcShotType = 2; // 勾库
                        npcCutAngle = 0f;
                        npcBestScore = 0f;
                        _Log("[NPC] 强制勾库安全球: 击球" + bestBall + " 距=" + kickDist.ToString("F2") + " 力=" + npcPower.ToString("F2"));
                        npcChargeDuration = testMode ? 0.5f : 1.0f;
                        npcChargeElapsed = 0f;
                        if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                        table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                        if (testMode) _RecordShotPre();
                        npcState = NPC_CHARGING;
                        return;
                    }
                }
                // 3rd try: closest ball even if blocked (absolute last resort)
                if (bestBall < 0)
                {
                    bestDist = float.MaxValue;
                    for (int b = 1; b <= 15; b++)
                    {
                        if ((targetBalls & (1u << b)) == 0) continue;
                        if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                        float d = (table.ballsP[b] - cuePos).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; bestBall = b; }
                    }
                }
                if (bestBall >= 0)
                {
                    Vector3 fBallPos = table.ballsP[bestBall];
                    Vector3 forcedAim = (fBallPos - cuePos).normalized;
                    float forcedDist = (fBallPos - cuePos).magnitude;
                    // Offset aim off-center to reduce accidental pocketing
                    Vector3 fPerp = new Vector3(-forcedAim.z, 0f, forcedAim.x);
                    Vector3 forcedSafeAim = (fBallPos + fPerp * BALL_RADIUS * 0.4f - cuePos).normalized;
                    npcAimDir = forcedSafeAim;
                    npcPower = Mathf.Clamp(forcedDist * 1.5f / 4.0f, MIN_POWER, 0.30f);
                    npcSpinValue = 0f;
                    npcTargetBall = bestBall;
                    npcTargetPocket = -1;
                    npcShotType = 0; // 安全球(直打)
                    npcCutAngle = 0f;
                    npcBestScore = 0f;
                    _Log("[NPC] 强制安全球(遮挡): 击球" + bestBall + " 距=" + forcedDist.ToString("F2") + " 力=" + npcPower.ToString("F2"));
                    npcChargeDuration = testMode ? 0.5f : 1.0f;
                    npcChargeElapsed = 0f;
                    if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                    table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                    if (testMode) _RecordShotPre();
                    npcState = NPC_CHARGING;
                    return;
                }
            }
            npcTimer = 1f;
            return;
        }
        _safetyFailCount = 0;

        Vector3 kBallPos = table.ballsP[bestBall];
        Vector3 aim = (kBallPos - cuePos).normalized;
        float shotDist = (kBallPos - cuePos).magnitude;
        // Offset aim slightly off-center to avoid accidentally pocketing the target ball
        Vector3 kPerp = new Vector3(-aim.z, 0f, aim.x);
        Vector3 safeAim = (kBallPos + kPerp * BALL_RADIUS * 0.4f - cuePos).normalized;
        float minVel = shotDist * 1.5f;
        float minPower = Mathf.Pow(minVel / 4.0f, 1.0f / 1.4f) * 0.5f;

        npcAimDir = safeAim;
        npcPower = Mathf.Clamp(minPower, MIN_POWER, 0.35f);
        npcSpinValue = 0f;
        npcTargetBall = bestBall;
        npcTargetPocket = -1;
        npcShotType = 0; // 安全球
        npcCutAngle = 0f;
        npcBestScore = 0f;

        _Log("[NPC] 安全球: 击球" + bestBall + " 距=" + shotDist.ToString("F2")
            + " 力=" + npcPower.ToString("F2") + " 母球预测=("
            + bestCueEndpoint.x.ToString("F2") + "," + bestCueEndpoint.z.ToString("F2") + ")");
        npcChargeDuration = testMode ? 0.5f : 1.0f;
        npcChargeElapsed = 0f;
        if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
        table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
        if (testMode) _RecordShotPre();
        npcState = NPC_CHARGING;
    }

    private int BitCount(uint v)
    {
        v = v - ((v >> 1) & 0x55555555u);
        v = (v & 0x33333333u) + ((v >> 2) & 0x33333333u);
        return (int)(((v + (v >> 4)) & 0x0F0F0F0Fu) * 0x01010101u >> 24);
    }

    private void _LogTableState(string tag)
    {
        Vector3 cue = table.ballsP[0];
        string s = "[NPC] " + tag + " cue=(" + cue.x.ToString("F2") + "," + cue.z.ToString("F2") + ") balls:";
        for (int i = 1; i <= 15; i++)
        {
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            Vector3 p = table.ballsP[i];
            s += " " + i + "=(" + p.x.ToString("F2") + "," + p.z.ToString("F2") + ")";
        }
        _Log(s);
    }

    // Ball-in-hand: find optimal cue ball placement
    private void _NpcPlaceCueBall()
    {
        uint targetBalls = _GetTargetBalls();
        if (targetBalls == 0) return;

        _InitPockets();

        float bestScore = float.MaxValue;
        Vector3 bestPos = Vector3.zero;
        float minClearanceSqr = BALL_DIAMETER * BALL_DIAMETER * 1.5f;
        float ghostOffset = BALL_DIAMETER * 3.5f;

        // Break shot: constrain ghost ball positions to kitchen area (left quarter)
        bool isBreakShot = _isFirstNpcShot;
        float kitchenLine = -table.k_TABLE_WIDTH * 0.5f;

        // Pass 1: strict constraints (clear path, no overlap, enough distance)
        for (int b = 1; b <= 15; b++)
        {
            if ((targetBalls & (1u << b)) == 0) continue;
            Vector3 ballPos = table.ballsP[b];

            for (int p = 0; p < 6; p++)
            {
                Vector3 pocketPos = npcPockets[p];
                if (_IsBallToPocketBlocked(ballPos, pocketPos, b, 0f, table.pocketLocations[p])) continue;

                Vector3 t2pDir = (pocketPos - ballPos).normalized;
                Vector3 ghostBall = ballPos - t2pDir * ghostOffset;

                // Must be on the table
                if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH - 0.05f) continue;
                if (Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT - 0.05f) continue;

                // Break shot: ghost ball must be in kitchen area (left quarter)
                if (isBreakShot && ghostBall.x > kitchenLine) continue;

                bool tooClose = false;
                for (int i = 1; i <= 15; i++)
                {
                    if (i == b) continue;
                    if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
                    if ((table.ballsP[i] - ghostBall).sqrMagnitude < minClearanceSqr)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                if (!_IsPathClear(ghostBall, ballPos, b)) continue;

                float shotDist = (ghostBall - table.ballsP[0]).magnitude;
                // Alignment bonus: prefer positions along the target→pocket line (straighter shot)
                Vector3 cueToGhost = (ghostBall - table.ballsP[0]).normalized;
                float alignment = Vector3.Dot(cueToGhost, t2pDir);
                float score = shotDist - alignment * 0.3f; // alignment reduces score (better)
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPos = ghostBall;
                }
            }
        }

        // Pass 2: relaxed — only require no overlap with other balls
        if (bestScore >= float.MaxValue)
        {
            _Log("[NPC] 自由球: 严格约束无解,放宽约束重试");
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                Vector3 ballPos = table.ballsP[b];

                for (int p = 0; p < 6; p++)
                {
                    Vector3 pocketPos = npcPockets[p];
                    Vector3 t2pDir = (pocketPos - ballPos).normalized;
                    Vector3 ghostBall = ballPos - t2pDir * ghostOffset;

                    if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH - 0.05f) continue;
                    if (Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT - 0.05f) continue;

                    // Break shot: ghost ball must be in kitchen area (left quarter)
                    if (isBreakShot && ghostBall.x > kitchenLine) continue;

                    bool tooClose = false;
                    for (int i = 1; i <= 15; i++)
                    {
                        if (i == b) continue;
                        if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
                        if ((table.ballsP[i] - ghostBall).sqrMagnitude < minClearanceSqr)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    float shotDist = (ghostBall - table.ballsP[0]).magnitude;
                    Vector3 cueToGhost2 = (ghostBall - table.ballsP[0]).normalized;
                    float alignment2 = Vector3.Dot(cueToGhost2, t2pDir);
                    float score2 = shotDist - alignment2 * 0.3f;
                    if (score2 < bestScore)
                    {
                        bestScore = score2;
                        bestPos = ghostBall;
                    }
                }
            }
        }

        // Pass 3: absolute fallback — place near center, away from all balls
        if (bestScore >= float.MaxValue)
        {
            _Log("[NPC] 自由球: 所有约束无解,放置台面中心");
            bestPos = new Vector3(0f, 0f, 0f);
            // Shift if center is occupied
            for (int i = 1; i <= 15; i++)
            {
                if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
                if ((table.ballsP[i] - bestPos).sqrMagnitude < minClearanceSqr)
                {
                    bestPos = bestPos + new Vector3(0.15f, 0f, 0f);
                    break;
                }
            }
            bestScore = 0f;
        }

        table.ballsP[0] = bestPos;
        table._TriggerPlaceBall(0);
        _Log("[NPC] 自由球摆放: (" + bestPos.x.ToString("F2") + ", " + bestPos.z.ToString("F2") + ") 距离=" + bestScore.ToString("F2"));
    }


    private void _NpcShoot()
    {
        float vel = Mathf.Pow(npcPower * 2.0f, 1.4f) * 4.0f;
        _Log("[NPC] 击球: 球" + npcTargetBall + " 力=" + npcPower.ToString("F2") + " 速=" + vel.ToString("F1") + "m/s"
            + " 方向=(" + npcAimDir.x.ToString("F3") + "," + npcAimDir.z.ToString("F3") + ")");
        table.desktopManager._NpcFire(npcAimDir, npcPower);
        npcState = NPC_SHOOTING;
    }

    public void _NpcStop()
    {
        npcState = NPC_IDLE;
        npcBallPlaced = false;
        npcFrameDelay = 0;
        _lastShotBall = -1;
        _lastShotPocket = -1;
        _repeatCount = 0;
        _safetyFailCount = 0;
        if (table.activeCue != null)
        {
            table.activeCue._SetNpcControlled(false);
        }
        if (table.desktopManager != null)
        {
            table.desktopManager._NpcFinishCharge();
        }
    }
}
