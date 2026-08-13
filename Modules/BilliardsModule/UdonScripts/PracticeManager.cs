#define EIJIS_SNOOKER15REDS
#define EIJIS_10BALL
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
    private int npcShotType; // 0=直接 1=翻袋 2=K球 3=轻碰 4=组合
    private float npcCutAngle; // 切角(度)
    private float npcBestScore; // 选球分数
    private int npcKickCushion = -1; // 勾库时实际选择的库边 (0=top,1=bottom,2=left,3=right)
    private int npcKickCushion2 = -1; // 两库勾库的第二个库边
    private int npcComboIntermediateBall = -1; // 组合球第一颗被母球撞到的中介球
    private bool npcHasDirectCandidate; // 本轮是否出现过直球候选
    private Vector3 npcFirstImpactPos = Vector3.zero; // 预测的母球第一次撞击点

    // Shared scratch cache for NPC candidate evaluation.
    // Primitive-only so it stays Udon-safe and can be reused across passes.
    private int npcEvalBall = -1;
    private int npcEvalPocket = -1;
    private int npcEvalShotType = 0;
    private int npcEvalKickCushion = -1;
    private int npcEvalKickCushion2 = -1;
    private int npcEvalComboIntermediateBall = -1;
    private Vector3 npcEvalAimDir = Vector3.forward;
    private Vector3 npcEvalFirstImpactPos = Vector3.zero;
    private float npcEvalShotDist = 0f;
    private float npcEvalScore = -1f;
    private float npcEvalSpin = 0f;
    private float npcEvalCutAngle = 0f;

    private int npcGroupId = -1; // cached NPC group: 0=solids, 1=stripes, -1=uninitialized
    private int npcFrameDelay; // frames to wait after sim ends before NPC can fire (fixes AI-vs-AI timing bug)

    // --- Repeat shot detection (prevent infinite loops) ---
    private int _lastShotBall = -1;
    private int _lastShotPocket = -1;
    private int _repeatCount = 0;
    private int _consecutiveMissCount = 0; // consecutive shots without pocketing (breaks A/B ping-pong)
    private uint _consecutiveMissMask = 0u; // pocketed mask from last check
    private int _lastFailedShotBall = -1;
    private int _lastFailedShotPocket = -1;
    private int _lastFailedShotType = -1;
    private uint _lastFailedPocketedMask = 0u;

    // --- Safety shot fail counter (break "无法击球" loop) ---
    private int _safetyFailCount = 0;
    private int _safetyFallbackCount = 0; // ultimate fallback retry counter

    // --- Corruption reset counter (prevent infinite corruption loop) ---
    private int _corruptionResetCount = 0;
    private int _corruptionSkipCount = 0; // non-test mode corruption skip counter
    private const int MAX_CORRUPTION_RESETS = 5;
    private const int MAX_CORRUPTION_SKIPS = 10; // force safety shot after this many skips

    // --- Failed break detection: all balls on right half → full power break ---
    private bool _isFirstNpcShot = true; // reset when game restarts
    private bool _gameWasLive = false; // track gameLive edge for reset
    private int _breakWaitCount = 0; // prevent infinite break-detection wait
    private bool _restartScheduled = false; // prevent duplicate auto-restart in test mode
    private bool _testGameEndHandled = false; // latch one test-mode game-end accounting pass
    private bool _testGameRunning = false; // only count ends after a game actually reached gameLive

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
    private int[] testShotType = new int[MAX_TEST_SHOTS]; // 0=直接 1=翻袋 2=K球 3=轻碰 4=组合
    private float[] testCutAngle = new float[MAX_TEST_SHOTS]; // 切角(度)
    private bool[] testIsFreeBall = new bool[MAX_TEST_SHOTS]; // 自由球
    private bool[] testTableOpen = new bool[MAX_TEST_SHOTS]; // 台面开放
    private int[] testNpcGroup = new int[MAX_TEST_SHOTS]; // NPC组别 -1=无 0=全色 1=花色
    private float[] testBestScore = new float[MAX_TEST_SHOTS]; // 选球分数
    private float[] testFirstImpactX = new float[MAX_TEST_SHOTS]; // 母球第一次撞击点X
    private float[] testFirstImpactZ = new float[MAX_TEST_SHOTS]; // 母球第一次撞击点Z
    private string[] testJudgement = new string[MAX_TEST_SHOTS]; // 日记判断
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

        if (table.npcEnabledLocal && (table.is8Ball || table.is9Ball || table.is10Ball || table.isSnooker) && (table.isPracticeMode || testMode) && (table.isOrangeTeamFull || testMode))
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
        _isFirstNpcShot = true;
        _gameWasLive = false;
        _safetyFallbackCount = 0;
        _corruptionSkipCount = 0;
        _breakWaitCount = 0;
        _consecutiveMissCount = 0;
        _consecutiveMissMask = 0u;
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
        _restartScheduled = false;
        _testGameEndHandled = false;
        _testGameRunning = false;
        _lastFailedShotBall = -1;
        _lastFailedShotPocket = -1;
        _lastFailedShotType = -1;
        _lastFailedPocketedMask = 0u;
        if (testReportText != null) testReportText.gameObject.SetActive(false);
        table._LogInfo("[TEST] 测试模式已启用, 自动开始游戏... 共" + MAX_TEST_GAMES + "局");
        // Auto-open lobby, join team, and start game through the same guarded state machine used for restarts.
        _AutoRestartGame();
    }

    public void _AutoJoinAndStart()
    {
        _AutoRestartGame();
    }

    public void _AutoTryStart()
    {
        if (!testMode) return;
        if (table.gameLive) return; // already started

        bool lobbyReady = table.lobbyOpen || table.gameStateLocal == 1 || table.networkingManager.gameStateSynced == 1;
        if (!lobbyReady || table.localPlayerId == -1)
        {
            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.3f);
            return;
        }

        table._LogInfo("[TEST] 自动开局重试: stateLocal=" + table.gameStateLocal
            + " stateSynced=" + table.networkingManager.gameStateSynced
            + " lobby=" + table.lobbyOpen
            + " player=" + table.localPlayerId);
        table._TriggerGameStart(); // PlayButton retry
        SendCustomEventDelayedSeconds(nameof(_AutoTryStart), 0.5f);
    }

    public void _AutoRestartGame()
    {
        if (!testMode) return;

        if (table.gameLive)
        {
            return;
        }

        int stateLocal = table.gameStateLocal;
        byte stateSynced = table.networkingManager.gameStateSynced;
        bool endedState = stateLocal == 3 || stateSynced == 3;
        bool lobbyReady = table.lobbyOpen || stateLocal == 1 || stateSynced == 1;

        // Game end arrives asynchronously. If the ended state is still visible together
        // with the old lobby flag, wait one tick for onRemoteGameEnded() to clear locals.
        if (endedState && table.lobbyOpen)
        {
            table._LogInfo("[TEST] 等待上一局结束清理: stateLocal=" + stateLocal + " stateSynced=" + stateSynced + " lobby=" + table.lobbyOpen);
            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.2f);
            return;
        }

        if (!lobbyReady)
        {
            table._LogInfo("[TEST] 自动开大厅: stateLocal=" + stateLocal + " stateSynced=" + stateSynced + " lobby=" + table.lobbyOpen);
            table._TriggerLobbyOpen(); // StartButton
            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.5f);
            return;
        }

        if (table.localPlayerId == -1)
        {
            table._LogInfo("[TEST] 自动入队: stateLocal=" + stateLocal + " stateSynced=" + stateSynced + " lobby=" + table.lobbyOpen);
            table._TriggerJoinTeam(0); // join orange team
            SendCustomEventDelayedSeconds(nameof(_AutoRestartGame), 0.4f);
            return;
        }

        table._LogInfo("[TEST] 自动开局: stateLocal=" + stateLocal
            + " stateSynced=" + stateSynced
            + " lobby=" + table.lobbyOpen
            + " player=" + table.localPlayerId);
        table._TriggerGameStart(); // PlayButton
        SendCustomEventDelayedSeconds(nameof(_AutoTryStart), 0.5f);
    }

    public void _OnTableGameEnded(uint winningTeam)
    {
        _FinishTestGame(" (winningTeam=" + winningTeam + ")");
    }

    private void _FinishTestGame(string suffix)
    {
        if (!testMode || !_testGameRunning || _testGameEndHandled) return;

        _testGameRunning = false;
        _testGameEndHandled = true;
        _corruptionResetCount = 0; // game ended normally, reset corruption counter
        testGameCount++;
        bool oneShot = testShotCount <= 1;
        if (oneShot) testOneShotClearCount++;
        table._LogInfo("[TEST] 第" + testGameCount + "/" + MAX_TEST_GAMES + "局结束" + suffix + ", 共" + testShotCount + "杆" + (oneShot ? " ★一杆清台!" : ""));
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

    private bool _ShouldSkipFailedCandidate(int ball, int pocket, int shotType)
    {
        return _lastFailedShotBall == ball
            && _lastFailedShotPocket == pocket
            && _lastFailedShotType == shotType
            && _lastFailedPocketedMask == table.ballsPocketedLocal;
    }

    public void _StopTestMode()
    {
        testMode = false;
        _restartScheduled = false;
        _testGameEndHandled = false;
        _testGameRunning = false;
        _lastFailedShotBall = -1;
        _lastFailedShotPocket = -1;
        _lastFailedShotType = -1;
        _lastFailedPocketedMask = 0u;
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
        testJudgement[testShotCount] = "";
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
        string typeStr = _GetShotTypeName(testShotType[idx]);
        testJudgement[idx] = _BuildShotJudgement(idx, pBefore, pAfter, foul, pocketedStr);
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
            + " 判断=" + testJudgement[idx]
            + " 撞击=(" + testFirstImpactX[idx].ToString("F3") + "," + testFirstImpactZ[idx].ToString("F3") + ")"
            + targetStatus
            + "\n  before=[" + testSnapBefore[idx] + "]"
            + "\n  after=[" + testSnapAfter[idx] + "]");
    }

    private string _GetShotTypeName(int shotType)
    {
        if (shotType == 0) return "直接";
        if (shotType == 1) return "翻袋";
        if (shotType == 2) return "K球";
        if (shotType == 3) return "轻碰";
        if (shotType == 4) return "组合";
        return "未知";
    }

    private string _BuildShotJudgement(int idx, uint pBefore, uint pAfter, bool foul, string pocketedStr)
    {
        bool targetPocketed = false;
        if (testTargetBall[idx] >= 0 && testTargetBall[idx] <= 15)
        {
            targetPocketed = ((pAfter & (1u << testTargetBall[idx])) != 0) && ((pBefore & (1u << testTargetBall[idx])) == 0);
        }

        if (targetPocketed) return "目标球进袋";
        if (foul) return "犯规";
        if (testShotType[idx] == 1)
        {
            if (testCutAngle[idx] >= 80f) return "翻袋高薄切，疑似袋角/摩擦补偿不足";
            if (testPower[idx] < 0.28f) return "翻袋力度偏小";
            return "翻袋未进，可能是容差或摩擦补偿不足";
        }
        if (testShotType[idx] == 0)
        {
            if (testCutAngle[idx] >= 90f)
            {
                if (Mathf.Abs(testSpin[idx]) >= 0.20f) return "薄切+旋转，疑似投掷效应影响";
                return "高薄切，疑似袋角/jaw 或容差过小";
            }
            if (testPower[idx] < 0.30f) return "力度偏小";
            if (pocketedStr.Length == 0) return "直接球未进，可能是瞄准或碰撞偏移";
            return "直接球结果异常";
        }
        if (testShotType[idx] == 2) return "勾库未进，可能库边补偿不足";
        if (testShotType[idx] == 4) return "组合球未进，可能连传角度不足";
        return "轻碰未进或被迫安全球";
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
            string typeStr = _GetShotTypeName(testShotType[i]);
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
                + " judge=" + testJudgement[i]
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
                _FinishTestGame("");
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
            _testGameEndHandled = false;
            _testGameRunning = true;
            npcGroupId = -1;
            _lastShotBall = -1;
            _lastShotPocket = -1;
            _repeatCount = 0;
            _lastFailedShotBall = -1;
            _lastFailedShotPocket = -1;
            _lastFailedShotType = -1;
            _lastFailedPocketedMask = 0u;
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
                if (testMode && _testGameRunning && !table.gameLive && !_restartScheduled)
                {
                    _Log("[NPC] 游戏已结束(Idle检测),停止NPC");
                    _FinishTestGame("");
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
                            _corruptionSkipCount++;
                            if (_corruptionSkipCount >= MAX_CORRUPTION_SKIPS)
                            {
                                _Log("[NPC] 球位置腐败跳过" + _corruptionSkipCount + "次(已达上限), 触发游戏重置...");
                                _corruptionSkipCount = 0;
                                table._TriggerGameReset();
                                npcTimer = 1.0f;
                            }
                            else
                            {
                                _Log("[NPC] 检测到球位置腐败,跳过本回合(第" + _corruptionSkipCount + "/" + MAX_CORRUPTION_SKIPS + "次)");
                                _safetyFailCount = 0;
                                npcTimer = 1.0f;
                            }
                        }
                        break;
                    }
                }

                // Non-test mode: verify break has been taken before NPC acts
                // (prevents NPC from stealing break during table re-initialization)
                if (!testMode && _isFirstNpcShot)
                {
                    // Break is taken if:
                    // 1. Foul committed or ball-in-hand awarded → break definitely happened
                    // 2. Any ball pocketed → break happened
                    // 3. >5 balls scattered from rack center → break happened
                    bool breakTaken = (table.foulStateLocal != 0 || table.isReposition);
                    if (!breakTaken)
                    {
                        int scatteredCount = 0;
                        Vector3 rackCenter = _GetRackCenter();
                        for (int i = 2; i <= 15; i++)
                        {
                            if ((table.ballsPocketedLocal & (1u << i)) != 0) { breakTaken = true; break; }
                            if ((table.ballsP[i] - rackCenter).sqrMagnitude > 0.04f) scatteredCount++;
                        }
                        if (!breakTaken) breakTaken = (scatteredCount >= 6);
                    }
                    if (!breakTaken)
                    {
                        _breakWaitCount++;
                        // After ~15s of waiting (30 ticks × 0.5s), assume the break actually happened
                        // and proceed — prevents infinite wait if detection logic is wrong
                        if (_breakWaitCount > 30)
                        {
                            _Log("[NPC] 开球检测等待超时(" + _breakWaitCount + "次), 强制继续");
                            _breakWaitCount = 0;
                        }
                        else
                        {
                            npcTimer = 0.5f;
                            break;
                        }
                    }
                    else
                    {
                        _breakWaitCount = 0;
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
                        table.ballsP[0] = new Vector3(kitchenLine, table.ballsP[0].y, 0f);
                        table._TriggerPlaceBall(0);
                        _Log("[NPC] 开球:白球移至开球线中点 x=" + kitchenLine.ToString("F3"));
                        npcTargetBall = -1;
                        npcTargetPocket = -1;
                        npcShotType = 0;
                        npcCutAngle = 0f;
                        npcBestScore = 0f;
                        // Aim at the geometric center of the rack to avoid side-cut break scratches.
                        Vector3 rackCenter = _GetRackCenter();
                        Vector3 cuePos = table.ballsP[0];
                        npcAimDir = (rackCenter - cuePos).normalized;
                        npcPower = MAX_POWER;
                        npcSpinValue = 0f;
                        _Log("[NPC] 开球:瞄球堆中心=" + rackCenter.ToString("F3") + " dir=(" + npcAimDir.x.ToString("F3") + "," + npcAimDir.z.ToString("F3") + ") 力=" + npcPower.ToString("F2"));
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

                    // Track consecutive failures (any shot, any pocket) to break A/B ping-pong loops
                    uint prevPocketed = _consecutiveMissMask;
                    _consecutiveMissMask = table.ballsPocketedLocal;
                    if (_consecutiveMissMask == prevPocketed)
                    {
                        _consecutiveMissCount++;
                        if (_consecutiveMissCount >= 6)
                        {
                            _Log("[NPC] 连续未进球" + _consecutiveMissCount + "次, 重置重复计数器");
                            _repeatCount = 0;
                            _lastShotBall = -1;
                            _lastShotPocket = -1;
                            _consecutiveMissCount = 0;
                        }
                    }
                    else
                    {
                        _consecutiveMissCount = 0;
                    }

                    // Remember failed attack candidates so the next search can avoid looping on the same miss.
                    if (npcTargetPocket >= 0 && npcTargetBall >= 0)
                    {
                        uint targetMask = 1u << npcTargetBall;
                        if ((table.ballsPocketedLocal & targetMask) == 0u)
                        {
                            _lastFailedShotBall = npcTargetBall;
                            _lastFailedShotPocket = npcTargetPocket;
                            _lastFailedShotType = npcShotType;
                            _lastFailedPocketedMask = table.ballsPocketedLocal;
                            _Log("[NPC] 记住失败候选: 球" + npcTargetBall + "->袋" + npcTargetPocket + " type=" + npcShotType);
                        }
                        else
                        {
                            _lastFailedShotBall = -1;
                            _lastFailedShotPocket = -1;
                            _lastFailedShotType = -1;
                            _lastFailedPocketedMask = 0u;
                        }
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
                            _FinishTestGame(" (winningTeam=" + table.winningTeamLocal + ")");
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
        npcHasDirectCandidate = false;
        npcComboIntermediateBall = -1;
        npcEvalComboIntermediateBall = -1;
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
        int bestShotType = 0; // 0=direct, 1=bank, 2=kick, 3=thin cut, 4=combo
        int bestKickCushion = -1; // first cushion for kick shot
        int bestKickCushion2 = -1; // second cushion for two-cushion kick
        int bestComboIntermediateBall = -1;
        bool hasBankCandidate = false;
        bool hasDirectCandidate = false;
        float bestDirectScore = -1f;
        int bestDirectBall = -1;
        int bestDirectPocket = -1;
        Vector3 bestDirectAimDir = Vector3.forward;
        float bestDirectShotDist = 1f;
        float bestDirectSpin = 0f;
        int bestDirectKickCushion = -1;
        int bestDirectKickCushion2 = -1;
        Vector3 bestDirectFirstImpactPos = Vector3.zero;
        float bestDirectCutAngle = 0f;

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

                // Throw compensation: AdvancedPhysics HandleCollision6 models
                // ball-ball friction (J_tangential), causing throw ~atan(μ) ≈ 1.7-2.9°.
                // OB is thrown toward CB's incoming direction (thicker hit).
                // We compensate by shifting ghost ball TOWARD aimDir side (thinner aim).
                float cutAngleRad = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f));
                if (cutAngleRad > 0.05f)
                {
                    float sinCut = Mathf.Sin(cutAngleRad);
                    float throwOffset = 0.0025f * sinCut * Mathf.Clamp01(cutAngleRad * cutAngleRad / 0.5f);
                    float halfW = table.k_TABLE_WIDTH;
                    float halfH = table.k_TABLE_HEIGHT;
                    bool nearCushion = ballPos.x > halfW - 0.06f || ballPos.x < -halfW + 0.06f
                                     || ballPos.z > halfH - 0.06f || ballPos.z < -halfH + 0.06f;
                    if (nearCushion && cutAngleRad > 0.4f)
                    {
                        throwOffset *= 2.0f;
                    }
                    // Perp must point TOWARD aimDir side of t2p (makes cut thinner to offset throw)
                    Vector3 perp = new Vector3(-t2pDir.z, 0f, t2pDir.x);
                    if (Vector3.Dot(perp, aimDir) < 0f) perp = -perp;
                    ghostBall += perp * throwOffset;
                    cueToGhost = ghostBall - cuePos;
                    shotDist = cueToGhost.magnitude;
                    if (shotDist < 0.05f || shotDist > 2.5f) continue;
                    aimDir = cueToGhost / shotDist;
                    alignment = Vector3.Dot(aimDir, t2pDir);
                    if (alignment < 0.05f) continue;
                }
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

                // 9-ball/10-ball: must hit LOWEST numbered ball first
                // Bonus for targeting the lowest ball, heavy penalty for skipping it
                if (table.is9Ball || table.is10Ball)
                {
                    int lowestBall = table.findLowestUnpocketedBall(table.ballsPocketedLocal);
                    if (lowestBall > 0)
                    {
                        if (b == lowestBall)
                        {
                            // Strong bonus for targeting the legal lowest ball
                            pocketScore += 0.4f;
                        }
                        else
                        {
                            // Heavy penalty: hitting a higher ball first is a foul
                            // Still allow combo evaluation (PASS 2.2) to succeed
                            pocketScore *= 0.15f;
                        }
                    }
                }

                // Snooker: prefer higher-value colors during color turn (black=7 > yellow=2)
                if (table.isSnooker && table.colorTurnLocal && b < table.sixredsnooker_ballpoints.Length)
                {
                    int colorValue = table.sixredsnooker_ballpoints[b];
                    // Scale bonus: 2-7 points → 0.05-0.20 bonus (subtle but decisive for tie-breaking)
                    pocketScore += (colorValue - 1) * 0.03f;
                }

                // Progressive cut angle penalty: large-angle shots are unreliable and risk rattle
                // Starts at 30° (mild) and increases to heavy penalty at 90°
                float cutAngleDeg = cutAnglePre * Mathf.Rad2Deg;
                if (cutAngleDeg > 30f)
                {
                    float angleFactor = Mathf.Clamp01((cutAngleDeg - 30f) / 60f); // 30°→0%, 90°→100%
                    pocketScore *= Mathf.Lerp(1f, 0.2f, angleFactor);
                }

                // Position play: only used as tiebreaker between equally easy shots, not for selection
                float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f));
                float posBonus = _EvalPositionPlay(cuePos, aimDir, ballPos, t2pDir, cutAngle, targetBalls, b);
                float spinForShot = _posPlaySpin;
                // Selection: purely based on pocketing ease (alignment, distance, ball-to-pocket)
                float totalScore = pocketScore;
                // Tiny tiebreaker: if two shots are nearly equal pocketability, prefer the one with position
                if (posBonus > 0f)
                    totalScore += 0.01f;

                // Scratch risk: predict cue ball post-contact trajectory
                // Check both tangent-line scratch (cut shots) AND follow-scratch (straight shots)
                // Progressive penalty based on how close cue ball passes to pocket
                float minScratchDist = float.MaxValue;
                float scratchThreshold = table.k_INNER_RADIUS_CORNER + BALL_RADIUS;

                // Check 1: Follow/Draw scratch — for near-straight shots,
                // cue ball follows aimDir (top spin) or reverses (draw) into pocket
                if (cutAnglePre < 0.26f) // ~15° cut — cue ball stays near shot line
                {
                    // Forward check: cue ball follows aimDir (top/follow)
                    for (int s = 0; s <= 15; s++)
                    {
                        Vector3 check = ghostBall + aimDir * (2.0f * s / 15);
                        for (int sp = 0; sp < 6; sp++)
                        {
                            float d = (check - table.pocketLocations[sp]).magnitude;
                            if (d < scratchThreshold && d < minScratchDist)
                                minScratchDist = d;
                        }
                    }
                    // Reverse check: cue ball draws back along -aimDir
                    for (int s = 0; s <= 15; s++)
                    {
                        Vector3 check = ghostBall - aimDir * (2.0f * s / 15);
                        for (int sp = 0; sp < 6; sp++)
                        {
                            float d = (check - table.pocketLocations[sp]).magnitude;
                            if (d < scratchThreshold && d < minScratchDist)
                                minScratchDist = d;
                        }
                    }
                }

                // Check 2: Tangent-line scratch — cue ball departs along tangent after cut
                {
                    Vector3 tangent = new Vector3(-aimDir.z, 0f, aimDir.x);
                    Vector3 toTarget = ballPos - cuePos;
                    if (Vector3.Dot(tangent, toTarget) < 0f) tangent = -tangent;

                    float trRemaining = 2.5f;
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
                        for (int s = 0; s <= trSteps; s++)
                        {
                            Vector3 trCheck = trStart + trDir * (trNearestT * s / trSteps);
                            for (int sp = 0; sp < 6; sp++)
                            {
                                float d = (trCheck - table.pocketLocations[sp]).magnitude;
                                if (d < scratchThreshold && d < minScratchDist)
                                    minScratchDist = d;
                            }
                        }

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
                }

                // Progressive penalty: closer scratch = heavier penalty
                if (minScratchDist < scratchThreshold)
                {
                    // Map 0→scratchThreshold: very close = nearly reject (0.02x), far = mild (0.7x)
                    float scratchSeverity = 1.0f - Mathf.Clamp01(minScratchDist / scratchThreshold);
                    float penaltyMul = Mathf.Lerp(0.02f, 0.7f, scratchSeverity);
                    totalScore *= penaltyMul;
                }

            if (_ShouldSkipFailedCandidate(b, p, 0))
            {
                if (debug) _Log("[NPC] 跳过重复失败直接球: 球" + b + "->袋" + p);
                continue;
            }

            npcEvalBall = b;

                npcEvalPocket = p;
                npcEvalShotType = 0;
                npcEvalKickCushion = -1;
                npcEvalKickCushion2 = -1;
                npcEvalAimDir = aimDir;
                npcEvalFirstImpactPos = ghostBall;
                npcEvalShotDist = shotDist;
                npcEvalSpin = spinForShot;
                npcEvalCutAngle = cutAngle;
                npcEvalScore = totalScore;
                npcHasDirectCandidate = true;
                hasDirectCandidate = true;

                if (npcEvalScore > bestScore)
                {
                    bestScore = npcEvalScore;
                    bestBall = npcEvalBall;
                    bestPocket = npcEvalPocket;
                    bestAimDir = npcEvalAimDir;
                    bestShotDist = npcEvalShotDist;
                    bestSpin = npcEvalSpin;
                    bestShotType = npcEvalShotType;
                    npcFirstImpactPos = npcEvalFirstImpactPos;
                    npcCutAngle = npcEvalCutAngle;
                }
                if (npcEvalScore > bestDirectScore)
                {
                    bestDirectScore = npcEvalScore;
                    bestDirectBall = npcEvalBall;
                    bestDirectPocket = npcEvalPocket;
                    bestDirectAimDir = npcEvalAimDir;
                    bestDirectShotDist = npcEvalShotDist;
                    bestDirectSpin = npcEvalSpin;
                    bestDirectKickCushion = npcEvalKickCushion;
                    bestDirectKickCushion2 = npcEvalKickCushion2;
                    bestDirectFirstImpactPos = npcEvalFirstImpactPos;
                    bestDirectCutAngle = npcEvalCutAngle;
                    hasDirectCandidate = true;
                }
            }
        }

        // === PASS 2: Bank shots (翻袋) — consider when direct shot is difficult or low confidence ===
        float directDifficulty = 1.0f - bestDirectScore; // Estimate direct shot difficulty (0=easy, 1=hard)
        if ((!hasDirectCandidate || directDifficulty > 0.4f) && bestScore < 0.35f)
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
                        float cutAngleB = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, bankDir), -1f, 1f));
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
                            npcEvalFirstImpactPos = bouncePoint;
                        }

                        if (_ShouldSkipFailedCandidate(b, p, 1))
                        {
                            _Log("[NPC] 跳过重复失败翻袋: 球" + b + "->袋" + p + " 库" + cushion);
                            continue;
                        }

                        float baseScore = alignment * 1.0f + Mathf.Clamp01(1.0f - shotDist / 2.0f) * 0.3f;
                        // Position play for bank shots — tiebreaker only
                        float bankCutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, bankDir), -1f, 1f));
                        float bankPosBonus = _EvalPositionPlay(cuePos, aimDir, ballPos, bankDir, bankCutAngle, targetBalls, b);
                        float bankSpinForShot = _posPlaySpin;
                        float score = baseScore;
                        if (bankPosBonus > 0f)
                            score += 0.01f;
                        npcEvalBall = b;
                        npcEvalPocket = p;
                        npcEvalShotType = 1;
                        npcEvalKickCushion = cushion;
                        npcEvalKickCushion2 = -1;
                        npcEvalAimDir = aimDir;
                        npcEvalShotDist = shotDist;
                        npcEvalSpin = bankSpinForShot;
                        npcEvalCutAngle = bankCutAngle;
                        npcEvalScore = score;

                        if (npcEvalScore > bestScore)
                        {
                            bestScore = npcEvalScore;
                            bestBall = npcEvalBall;
                            bestPocket = npcEvalPocket;
                            bestAimDir = npcEvalAimDir;
                            bestShotDist = npcEvalShotDist;
                            bestSpin = npcEvalSpin;
                            bestShotType = npcEvalShotType;
                            bestKickCushion = npcEvalKickCushion;
                            bestKickCushion2 = npcEvalKickCushion2;
                            npcFirstImpactPos = npcEvalFirstImpactPos;
                            npcCutAngle = npcEvalCutAngle;
                            hasBankCandidate = true;
                        }
                    }
                }
            }
        }

        // === PASS 2b: Two-cushion bank shots (两库翻袋) — ball hits two cushions before pocket ===
        if (!hasDirectCandidate && bestScore < 0.5f)
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
                            // Reflect pocket across c2, then across c1.
                            Vector3 reflected1 = _ReflectPocket(pocketPos, c2);
                            Vector3 bankReflected2 = _ReflectPocket(reflected1, c1);

                            Vector3 bankDir = (bankReflected2 - ballPos).normalized;
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
                            float cutAngleB = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, bankDir), -1f, 1f));
                            if (cutAngleB < 0.26f || cutAngleB > 2.79f) continue;
                            if (!_IsPathClear(cuePos, ghostBall, b)) continue;
                            if (_IsPathCrossesCushion(cuePos, ghostBall)) continue;

                            // Calculate bounce point on first cushion from the same reflected route.
                            Vector3 bounce1 = _GetCushionBouncePoint(ballPos, bankReflected2, c1);
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
                            npcEvalBall = b;
                            npcEvalPocket = p;
                            npcEvalShotType = 1;
                            npcEvalKickCushion = c1;
                            npcEvalKickCushion2 = c2;
                            npcEvalAimDir = aimDir;
                            npcEvalFirstImpactPos = bounce1;
                            npcEvalShotDist = shotDist;
                            npcEvalSpin = 0f;
                            npcEvalCutAngle = 0f;
                            npcEvalScore = score;

                        if (npcEvalScore > bestScore)
                        {
                            bestScore = npcEvalScore;
                            bestBall = npcEvalBall;
                            bestPocket = npcEvalPocket;
                            bestAimDir = npcEvalAimDir;
                            bestShotDist = npcEvalShotDist;
                            bestSpin = npcEvalSpin;
                            bestShotType = npcEvalShotType;
                            bestKickCushion = npcEvalKickCushion;
                            bestKickCushion2 = npcEvalKickCushion2;
                            npcFirstImpactPos = npcEvalFirstImpactPos;
                            npcCutAngle = npcEvalCutAngle;
                            hasBankCandidate = true;
                            _Log("[NPC] 两库翻袋: 球" + b + "->袋" + p + " 库" + c1 + "->库" + c2 + " 分=" + score.ToString("F2"));
                        }

                        }
                    }
                }
            }
        }

        // === PASS 2.2: Combo shots (组合球) — cue -> intermediate ball -> target ball -> pocket ===
        // 9-ball/10-ball: combos are essential — must hit lowest ball first, can pocket any ball
        bool tryCombo = _TryEvaluateComboShot(cuePos, targetBalls, debug);
        if (table.is9Ball || table.is10Ball)
        {
            // Always evaluate combos in 9/10-ball (lowest-ball-first rule makes them primary)
            if (tryCombo && npcEvalScore > bestScore)
            {
                bestScore = npcEvalScore;
                bestBall = npcEvalBall;
                bestPocket = npcEvalPocket;
                bestAimDir = npcEvalAimDir;
                bestShotDist = npcEvalShotDist;
                bestSpin = npcEvalSpin;
                bestShotType = npcEvalShotType;
                bestKickCushion = npcEvalKickCushion;
                bestKickCushion2 = npcEvalKickCushion2;
                bestComboIntermediateBall = npcEvalComboIntermediateBall;
                npcFirstImpactPos = npcEvalFirstImpactPos;
                npcCutAngle = npcEvalCutAngle;
            }
        }
        else if (!hasDirectCandidate && bestScore < 0.2f && tryCombo)
        {
            if (npcEvalScore > bestScore)
            {
                bestScore = npcEvalScore;
                bestBall = npcEvalBall;
                bestPocket = npcEvalPocket;
                bestAimDir = npcEvalAimDir;
                bestShotDist = npcEvalShotDist;
                bestSpin = npcEvalSpin;
                bestShotType = npcEvalShotType;
                bestKickCushion = npcEvalKickCushion;
                bestKickCushion2 = npcEvalKickCushion2;
                bestComboIntermediateBall = npcEvalComboIntermediateBall;
                npcFirstImpactPos = npcEvalFirstImpactPos;
                npcCutAngle = npcEvalCutAngle;
            }
        }

        // === PASS 2.5: Thin cut (轻碰) — gentle touch when ball is visible but not pocketable ===
        // Priority: if we can see the target ball, try a thin cut before kick shots
        if (!hasDirectCandidate && bestScore < 0.2f && !hasBankCandidate)
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

                // Determine opponent balls (mode-dependent)
                uint thinOppBalls = 0u;
                if (table.is9Ball || table.is10Ball)
                {
                    thinOppBalls = 0u;
                }
                else if (table.isSnooker)
                {
                    bool tSnkRed = table.sixRedCheckIfRedOnTable(table.ballsPocketedLocal, false);
                    if (table.colorTurnLocal)
                        thinOppBalls = ~table.ballsPocketedLocal & table.SNOOKER_REDS_MASK;
                    else if (tSnkRed)
                        thinOppBalls = ~table.ballsPocketedLocal & 0x1AEu;
                }
                else if (npcGroupId == 0) thinOppBalls = ~table.ballsPocketedLocal & 0xFE00u & ~0x2u;
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
                bool thinNearPocket = false;
                for (int p = 0; p < 6; p++)
                {
                    float dToPocket = (ghostBall - npcPockets[p]).magnitude;
                    if (dToPocket < 0.18f)
                    {
                        thinNearPocket = true;
                        safetyScore -= 0.3f;
                    }
                }
                if (thinNearPocket)
                {
                    if (debug) _Log("[NPC] 跳过: 轻碰 球" + b + " ghost近袋口");
                    continue;
                }
                safetyScore = Mathf.Min(safetyScore, 0.18f);
                if (safetyScore <= 0f) continue;

                _Log("[NPC] 轻碰: 球" + b + " dist=" + cueToBallDist.ToString("F2") + " safety=" + safetyScore.ToString("F2"));

                npcEvalBall = b;
                npcEvalPocket = -1;
                npcEvalShotType = 3;
                npcEvalKickCushion = -1;
                npcEvalKickCushion2 = -1;
                npcEvalAimDir = cueToBallDir;
                npcEvalFirstImpactPos = ghostBall;
                npcEvalShotDist = cueToBallDist;
                npcEvalSpin = 0f;
                npcEvalCutAngle = 0f;
                npcEvalScore = safetyScore;

                if (npcEvalScore > bestScore)
                {
                    bestScore = npcEvalScore;
                    bestBall = npcEvalBall;
                    bestPocket = npcEvalPocket;
                    bestAimDir = npcEvalAimDir; // aim along cue→ball line for thin contact
                    bestShotDist = npcEvalShotDist;
                    bestSpin = npcEvalSpin;
                    bestShotType = npcEvalShotType; // thin cut (轻碰)
                    bestKickCushion = npcEvalKickCushion;
                    bestKickCushion2 = npcEvalKickCushion2;
                    npcFirstImpactPos = npcEvalFirstImpactPos;
                    npcCutAngle = npcEvalCutAngle;
                }
            }
        }

        // === PASS 3: Kick shots (K球) — only if no thin-cut safety shot exists ===
        // Direct-contact safety shots should outrank kick shots when both are available.
        if (!hasDirectCandidate && bestScore < 0.25f && bestShotType != 3)
        {
            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];
                // Try kick shots for all target balls when no direct/bank shot found

                for (int cushion = 0; cushion < 4; cushion++)
                {
                    Vector3 cushionPoint = _GetCushionPoint(cuePos, ballPos, cushion);
                    if (cushionPoint.x == float.MaxValue)
                    {
                        _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 无交点");
                        continue;
                    }

                    float dist1 = (cushionPoint - cuePos).magnitude;
                    float dist2 = (ballPos - cushionPoint).magnitude;
                    if (dist1 + dist2 > 2.0f)
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

                    float score = 0.35f + Mathf.Clamp01(1.0f - (dist1 + dist2) / 2.0f) * 0.25f;
                    if (score < 0.45f) continue;
                    _Log("[NPC] 勾库: 球" + b + "->库" + cushion + " 通过! dist=" + (dist1 + dist2).ToString("F2") + " score=" + score.ToString("F2"));
                    npcEvalBall = b;
                    npcEvalPocket = -1;
                    npcEvalShotType = 2;
                    npcEvalKickCushion = cushion;
                    npcEvalKickCushion2 = -1;
                    npcEvalAimDir = aimDir;
                    npcEvalFirstImpactPos = cushionPoint;
                    npcEvalShotDist = dist1 + dist2;
                    npcEvalSpin = 0f;
                    npcEvalCutAngle = 0f;
                    npcEvalScore = score;

                    if (npcEvalScore > bestScore)
                    {
                        bestScore = npcEvalScore;
                        bestBall = npcEvalBall;
                        bestPocket = npcEvalPocket;
                        bestAimDir = npcEvalAimDir;
                        bestShotDist = npcEvalShotDist;
                        bestSpin = npcEvalSpin;
                        bestShotType = npcEvalShotType;
                        bestKickCushion = npcEvalKickCushion;
                        bestKickCushion2 = npcEvalKickCushion2;
                        npcFirstImpactPos = npcEvalFirstImpactPos;
                        npcCutAngle = npcEvalCutAngle;
                    }
                }
            }
        }

        // === PASS 3b: Two-cushion kick shots (两库勾库) — cue ball hits two cushions before target ===
        if (!hasDirectCandidate && bestScore < 0.25f)
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
                        Vector3 bank2Reflected2 = _ReflectPoint(reflected1, c1);

                        // Find where cue path hits first cushion (c1)
                        Vector3 bounce1 = _GetCushionPoint(cuePos, bank2Reflected2, c1);
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
                        npcEvalBall = b;
                        npcEvalPocket = -1;
                        npcEvalShotType = 2;
                        npcEvalKickCushion = c1;
                        npcEvalKickCushion2 = c2;
                        npcEvalAimDir = aimDir;
                        npcEvalFirstImpactPos = bounce1;
                        npcEvalShotDist = totalDist;
                        npcEvalSpin = 0f;
                        npcEvalCutAngle = 0f;
                        npcEvalScore = score;

                        if (npcEvalScore > bestScore)
                        {
                            bestScore = npcEvalScore;
                            bestBall = npcEvalBall;
                            bestPocket = npcEvalPocket;
                            bestAimDir = npcEvalAimDir;
                            bestShotDist = npcEvalShotDist;
                            bestSpin = npcEvalSpin;
                            bestShotType = npcEvalShotType;
                            bestKickCushion = npcEvalKickCushion;
                            bestKickCushion2 = npcEvalKickCushion2;
                            npcFirstImpactPos = npcEvalFirstImpactPos;
                            npcCutAngle = npcEvalCutAngle;
                            _Log("[NPC] 两库勾库: 球" + b + " 库" + c1 + "->库" + c2 + " dist=" + totalDist.ToString("F2") + " score=" + score.ToString("F2"));
                        }
                    }
                }
            }
        }

        // 8-ball is now included in targetBalls by _GetTargetBalls() when group is cleared
        // No separate check needed — PASS 1/2/3 handles it like any other ball

        // Final-eight fallback: if only the black ball remains, do not downgrade into a safety shot.
        // Final 8-ball: always evaluate with relaxed criteria to pick the best direct shot.
        // A direct 8-ball attempt is always the correct choice — never kick or bank the black.
        if (targetBalls == 0x2u)
        {
            Vector3 ballPos = table.ballsP[1];
            float relaxedBest = float.MinValue;
            int relaxedPocket = -1;
            Vector3 relaxedAimDir = Vector3.forward;
            float relaxedShotDist = 0f;
            float relaxedSpin = 0f;
            float relaxedCutAngle = 0f;
            Vector3 relaxedImpact = Vector3.zero;

            for (int p = 0; p < 6; p++)
            {
                Vector3 pocketPos = npcPockets[p];
                float ballToPocket = (pocketPos - ballPos).magnitude;
                if (ballToPocket < 0.05f || ballToPocket > 2.0f) continue;

                Vector3 t2pDir = (pocketPos - ballPos).normalized;
                Vector3 ghostBall = ballPos - t2pDir * BALL_DIAMETER;
                if (Mathf.Abs(ghostBall.x) > table.k_TABLE_WIDTH + BALL_RADIUS * 0.5f
                    || Mathf.Abs(ghostBall.z) > table.k_TABLE_HEIGHT + BALL_RADIUS * 0.5f) continue;

                Vector3 cueToGhost = ghostBall - cuePos;
                float shotDist = cueToGhost.magnitude;
                if (shotDist < 0.05f || shotDist > 2.5f) continue;

                Vector3 aimDir = cueToGhost / shotDist;
                float alignment = Vector3.Dot(aimDir, t2pDir);
                if (alignment < -0.15f) continue;

                Vector3 c2g = ghostBall - cuePos;
                if (!_IsPathClear(cuePos, ghostBall, 1)) continue;
                if (_IsPathCrossesCushion(cuePos, ghostBall)) continue;
                if (_IsBallToPocketBlocked(ballPos, pocketPos, 1, Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f)), table.pocketLocations[p])) continue;

                float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, t2pDir), -1f, 1f));
                float score = alignment * 2.0f
                    + Mathf.Clamp01(1.0f - shotDist / 2.0f) * 0.25f
                    + Mathf.Clamp01(1.0f - ballToPocket / 1.5f) * 0.15f;

                if (score > relaxedBest)
                {
                    relaxedBest = score;
                    relaxedPocket = p;
                    relaxedAimDir = aimDir;
                    relaxedShotDist = shotDist;
                    relaxedSpin = 0f;
                    relaxedCutAngle = cutAngle;
                    relaxedImpact = ghostBall;
                }
            }

            if (relaxedPocket >= 0)
            {
                bestScore = Mathf.Max(bestScore, 0.05f);
                bestBall = 1;
                bestPocket = relaxedPocket;
                bestAimDir = relaxedAimDir;
                bestShotDist = relaxedShotDist;
                bestSpin = relaxedSpin;
                bestShotType = 0;
                bestKickCushion = -1;
                bestKickCushion2 = -1;
                npcFirstImpactPos = relaxedImpact;
                npcCutAngle = relaxedCutAngle;
                _Log("[NPC] 终局8-ball宽松尝试: 袋" + relaxedPocket + " dist=" + relaxedShotDist.ToString("F2") + " score=" + relaxedBest.ToString("F2"));
            }
        }

        // Direct shot with cut angle < 80° → always pick it, regardless of bank/kick score.
        // Only fall back to alternatives when no reasonable direct shot exists or no alternative found.
        if (hasDirectCandidate && (bestDirectCutAngle < 80f * Mathf.Deg2Rad || bestBall < 0))
        {
            if (bestBall >= 0)
                _Log("[NPC] 直接球优先: 切角=" + (bestDirectCutAngle * Mathf.Rad2Deg).ToString("F1") + "° 分=" + bestDirectScore.ToString("F3") + " (忽略替代分=" + bestScore.ToString("F3") + ")");
            else
                _Log("[NPC] 直接球(无替代): 切角=" + (bestDirectCutAngle * Mathf.Rad2Deg).ToString("F1") + "° 分=" + bestDirectScore.ToString("F3"));
            bestScore = bestDirectScore;
            bestBall = bestDirectBall;
            bestPocket = bestDirectPocket;
            bestAimDir = bestDirectAimDir;
            bestShotDist = bestDirectShotDist;
            bestSpin = bestDirectSpin;
            bestShotType = 0;
            bestKickCushion = bestDirectKickCushion;
            bestKickCushion2 = bestDirectKickCushion2;
            bestComboIntermediateBall = -1;
            npcFirstImpactPos = bestDirectFirstImpactPos;
            npcCutAngle = bestDirectCutAngle;
        }
        else if (hasDirectCandidate)
        {
            _Log("[NPC] 直接球切角过大(" + (bestDirectCutAngle * Mathf.Rad2Deg).ToString("F1") + "°), 选用替代方案 替代分=" + bestScore.ToString("F3"));
        }

        if (bestBall < 0)
        {
            _Log("[NPC] 无进球路线(直接+翻袋+K球均无)");
            return false;
        }

        // === Calculate power (physics-based) — runs BEFORE scratch prevention so adjustments apply ===
        Vector3 ballPos2 = table.ballsP[bestBall];
        float ballToPocketDist = bestPocket >= 0 ? (npcPockets[bestPocket] - ballPos2).magnitude : 0.5f;
        float cutAngleFinal = 0f;
        if (bestShotType != 4)
        {
            if (bestPocket >= 0)
            {
                Vector3 t2pLog = (npcPockets[bestPocket] - table.ballsP[bestBall]).normalized;
                npcCutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2pLog), -1f, 1f)) * Mathf.Rad2Deg;
                cutAngleFinal = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bestAimDir, t2pLog), -1f, 1f));
            }
            else
            {
                npcCutAngle = 0f;
                cutAngleFinal = 0f;
            }
        }
        else
        {
            cutAngleFinal = Mathf.Clamp(npcCutAngle * Mathf.Deg2Rad, 0f, Mathf.PI * 0.5f);
        }

        float cosAngle = Mathf.Cos(cutAngleFinal);
        if (cosAngle < 0.5f) cosAngle = 0.5f;
        float effectiveDist = bestShotDist + ballToPocketDist / cosAngle;
        if (bestShotType == 2)
        {
            effectiveDist *= bestKickCushion2 >= 0 ? 1.45f : 1.3f;
        }
        if (bestShotType == 1)
        {
            if (bestKickCushion2 >= 0) effectiveDist *= 1.55f;
            else if (bestShotDist > 1.5f) effectiveDist *= 1.4f;
        }
        if (bestShotType == 4)
        {
            if (bestComboIntermediateBall >= 0)
            {
                Vector3 comboMidPos = table.ballsP[bestComboIntermediateBall];
                effectiveDist += (cuePos - comboMidPos).magnitude * 0.35f;
            }
            effectiveDist *= 1.12f;
            _Log("[NPC] 组合加力: dist=" + effectiveDist.ToString("F2"));
        }
        float needVel = Mathf.Sqrt(3.92f * effectiveDist) * 0.85f;
        float power = Mathf.Pow(needVel / 4.0f, 1.0f / 1.4f) * 0.5f;
        power = Mathf.Clamp(power, MIN_POWER, MAX_POWER);
        if (bestShotType == 3)
        {
            power = MIN_POWER;
            _Log("[NPC] 轻碰减力: 力=" + power.ToString("F2"));
        }
        if (bestShotType == 2)
        {
            float kickMaxPower = Mathf.Clamp(0.18f + effectiveDist * 0.05f, 0.22f, bestKickCushion2 >= 0 ? 0.26f : 0.30f);
            if (power > kickMaxPower)
            {
                _Log("[NPC] 勾库减力: 原力=" + power.ToString("F2") + " -> " + kickMaxPower.ToString("F2")
                    + (bestKickCushion2 >= 0 ? " (两库保守)" : ""));
                power = kickMaxPower;
            }
        }
        if (bestShotType == 1)
        {
            float bankMaxPower = bestKickCushion2 >= 0
                ? Mathf.Clamp(0.20f + effectiveDist * 0.06f, 0.24f, 0.34f)
                : Mathf.Clamp(0.20f + effectiveDist * 0.08f, 0.25f, 0.38f);
            if (power > bankMaxPower)
            {
                _Log("[NPC] 翻袋减力: 原力=" + power.ToString("F2") + " -> " + bankMaxPower.ToString("F2") + " (dist=" + effectiveDist.ToString("F2")
                    + (bestKickCushion2 >= 0 ? ", 两库保守" : "") + ")");
                power = bankMaxPower;
            }
        }
        // 直球中力：保持中等力度，靠轻低杆防跟进（不增力）
        else if (bestShotType == 0 && npcCutAngle < 5.0f) // 非常直的球（切角小于5度）
        {
            // 不增力，依赖轻低杆防止母球跟进
            // 力度保持base水平，clamp到合理范围
            power = Mathf.Clamp(power, MIN_POWER, 0.30f);
            _Log("[NPC] 直球中力: 力=" + power.ToString("F2") + " (切角=" + npcCutAngle.ToString("F1") + "°)");
        }
        npcPower = power;
        npcAimDir = bestAimDir;
        npcTargetBall = bestBall;
        npcTargetPocket = bestPocket;
        npcShotType = bestShotType;
        npcSpinValue = bestSpin;
        npcKickCushion = bestKickCushion;
        npcKickCushion2 = bestKickCushion2;
        npcComboIntermediateBall = bestShotType == 4 ? bestComboIntermediateBall : -1;
        npcBestScore = bestScore;

        string shotTypeStr = _GetShotTypeName(bestShotType);
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
                if (bestShotType == 4 && ob == npcComboIntermediateBall) continue;
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
                // Reject shot if cue ball is heading almost directly into pocket center —
                // spin/power adjustments won't save it at this distance.
                if (scratchRisk && scratchDist < 0.04f)
                {
                    _Log("[NPC] 犯规预测: 母球必定落袋! 近袋" + scratchPocket
                        + " 距=" + (scratchDist * 100f).ToString("F1") + "cm → 拒绝此球,换安全球");
                    npcHasDirectCandidate = false;
                    _NpcFireSafetyShot();
                    return true;
                }

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
                    // Improved scratch risk handling: less aggressive for distant scratches
                    float scratchFactor = Mathf.InverseLerp(0.08f, 0.02f, scratchDist); // 0-1 range (closer = higher value)
                    if (scratchFactor > 0.5f) // Only significantly reduce power for close scratches
                    {
                        powerMul = Mathf.Lerp(1.0f, 0.7f, (scratchFactor - 0.5f) * 2f);
                        finalSpin = Mathf.Min(bestSpin, -0.3f + scratchFactor * -0.4f);
                    }
                    else
                    {
                        // Distant scratch - minimal impact
                        powerMul = Mathf.Lerp(1.0f, 0.9f, scratchFactor * 2f); // 1.0 to 0.9 as dist decreases
                        finalSpin = bestSpin;
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
                // Bank shot: draw the selected cushion route first, then fall back to discovery.
                bool drawn = false;
                if (npcKickCushion >= 0)
                {
                    if (npcKickCushion2 >= 0)
                    {
                        Vector3 reflected1 = _ReflectPocket(pocketPos, npcKickCushion2);
                        Vector3 drawReflected2 = _ReflectPocket(reflected1, npcKickCushion);
                        Vector3 b1 = _GetCushionBouncePoint(ballPos, drawReflected2, npcKickCushion);
                        Vector3 b2 = (b1.x != float.MaxValue) ? _GetCushionBouncePoint(b1, reflected1, npcKickCushion2) : new Vector3(float.MaxValue, 0f, 0f);
                        if (b1.x != float.MaxValue && b2.x != float.MaxValue)
                        {
                            _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(b1, y)), Color.red, 0.004f);
                            _DrawLine(_L2W(WithY(b1, y)), _L2W(WithY(b2, y)), Color.red, 0.004f);
                            _DrawLine(_L2W(WithY(b2, y)), _L2W(WithY(pocketPos, y)), Color.yellow, 0.004f);
                            _DrawDot(_L2W(WithY(b1, y)), 0.012f, Color.red);
                            _DrawDot(_L2W(WithY(b2, y)), 0.012f, Color.red);
                            drawn = true;
                        }
                    }
                    else
                    {
                        Vector3 reflected = _ReflectPocket(pocketPos, npcKickCushion);
                        Vector3 bankRaw = (reflected - ballPos).normalized;
                        float ballToCushionDist = (reflected - ballPos).magnitude;
                        float frictionOff = 0.08f + Mathf.Clamp(ballToCushionDist * 0.12f, 0f, 0.08f);
                        if (npcKickCushion == 0 || npcKickCushion == 1) reflected.x += Mathf.Sign(bankRaw.x) * frictionOff;
                        else reflected.z += Mathf.Sign(bankRaw.z) * frictionOff;
                        Vector3 bp = _GetCushionBouncePoint(ballPos, reflected, npcKickCushion);
                        if (bp.x != float.MaxValue && _IsCushionBouncePointValid(bp, npcKickCushion))
                        {
                            _DrawLine(_L2W(WithY(ballPos, y)), _L2W(WithY(bp, y)), Color.red, 0.004f);
                            _DrawLine(_L2W(WithY(bp, y)), _L2W(WithY(pocketPos, y)), Color.yellow, 0.004f);
                            _DrawDot(_L2W(WithY(bp, y)), 0.012f, Color.red);
                            drawn = true;
                        }
                    }
                }
                // Single cushion fallback
                for (int c = 0; c < 4 && !drawn; c++)
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
                            Vector3 drawReflected2 = _ReflectPocket(reflected1, c1);
                            Vector3 b1 = _GetCushionBouncePoint(ballPos, drawReflected2, c1);
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
                // Two-cushion kick: draw from the same first impact point selected for firing.
                Vector3 reflected1 = _ReflectPoint(ballPos, npcKickCushion2);
                Vector3 bounce1 = npcFirstImpactPos;
                if (bounce1.x == float.MaxValue || !_IsCushionBouncePointValid(bounce1, npcKickCushion))
                {
                    Vector3 cueReflected2 = _ReflectPoint(reflected1, npcKickCushion);
                    bounce1 = _GetCushionPoint(cuePos, cueReflected2, npcKickCushion);
                }
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
                // Single cushion kick: use the selected first impact point when available.
                Vector3 cushionPoint = npcFirstImpactPos;
                if (cushionPoint.x == float.MaxValue || !_IsCushionBouncePointValid(cushionPoint, npcKickCushion))
                {
                    cushionPoint = _GetCushionPoint(cuePos, ballPos, npcKickCushion);
                }
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
                // Straight shot near pocket: light draw to prevent follow-scratch
                spins = new float[] { -0.15f, -0.05f, 0f };
                spinCount = 3;
            }
            else
            {
                // Short straight: light draw, stun, light follow
                spins = new float[] { -0.12f, 0f, 0.15f };
                spinCount = 3;
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
                float cutAngleNext = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDirNext, t2pDir), -1f, 1f));

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

    private bool _IsBallOnTable(int ballId)
    {
        Vector3 p = table.ballsP[ballId];
        return Mathf.Abs(p.x) < table.k_TABLE_WIDTH && Mathf.Abs(p.z) < table.k_TABLE_HEIGHT && Mathf.Abs(p.y) <= 1.0f;
    }

    private uint _GetTargetBalls()
    {
        uint pocketed = table.ballsPocketedLocal;

        // === 9-Ball / 10-Ball: all unpocketed balls are shared targets ===
        if (table.is9Ball || table.is10Ball)
        {
            npcGroupId = -1;
            int maxBall = table.is10Ball ? 10 : 9;
            uint allValid = 0u;
            for (int i = 1; i <= maxBall; i++)
            {
                uint mask = 1u << i;
                if ((pocketed & mask) == 0u)
                    allValid |= mask;
            }
            return allValid;
        }

        // === Snooker (6-red / 15-red): red/color alternation ===
        if (table.isSnooker)
        {
            npcGroupId = -1;
            uint pocketedLocal = table.ballsPocketedLocal;
            bool redOnTable = table.sixRedCheckIfRedOnTable(pocketedLocal, false);
            int nextColor = table.sixRedFindLowestUnpocketedColor(pocketedLocal);
            uint objective = table.sixRedGetObjective(table.colorTurnLocal, redOnTable, nextColor, false, false);

            // Free ball: add first-hit ball to objective set
            if (table.foulStateLocal == 5)
            {
                objective |= 1u << table.firstHit;
            }

            return objective;
        }

        // === 8-Ball (original logic) ===
        uint remaining = ~pocketed & 0xFFFEu & ~0x2u; // exclude cue(0) and 8-ball(1)

        // If only the 8-ball remains physically on the table, always target it
        bool onlyEightOnTable = _IsBallOnTable(1);
        if (onlyEightOnTable)
        {
            for (int i = 2; i <= 15; i++)
            {
                if (_IsBallOnTable(i))
                {
                    onlyEightOnTable = false;
                    break;
                }
            }
        }
        if (onlyEightOnTable)
        {
            npcGroupId = (int)(table.teamIdLocal ^ table.teamColorLocal);
            _Log("[NPC] 仅剩8-ball,强制加入目标球 ball1pos=" + table.ballsP[1].ToString("F3") + " pocketed=0x" + pocketed.ToString("X8"));
            return 0x2u;
        }

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
        if (result == 0 && _IsBallOnTable(1))
        {
            result |= 0x2u; // add 8-ball (ball 1)
            _Log("[NPC] 组球清完,加入8-ball  ball1pos=" + table.ballsP[1].ToString("F3") + " pocketed=0x" + pocketed.ToString("X8"));
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

    private Vector3 _GetRackCenter()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 1; i <= 15; i++)
        {
            if ((table.ballsPocketedLocal & (1u << i)) != 0) continue;
            sum += table.ballsP[i];
            count++;
        }
        if (count <= 0) return table.ballsP[8];
        return sum / count;
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

    private bool _TryEvaluateComboShot(Vector3 cuePos, uint targetBalls, bool debug)
    {
        float bestComboScore = -1f;
        int bestComboBall = -1;
        int bestComboPocket = -1;
        int bestComboIntermediateBall = -1;
        Vector3 bestComboAimDir = Vector3.forward;
        float bestComboShotDist = 1f;
        float bestComboCutAngle = 0f;
        Vector3 bestComboImpact = Vector3.zero;

        for (int b = 1; b <= 15; b++)
        {
            if ((targetBalls & (1u << b)) == 0) continue;
            if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
            Vector3 ballPos = table.ballsP[b];

            for (int p = 0; p < 6; p++)
            {
                Vector3 pocketPos = npcPockets[p];
                float ballToPocket = (pocketPos - ballPos).magnitude;
                if (ballToPocket < 0.05f || ballToPocket > 2.0f) continue;

                Vector3 t2pDir = (pocketPos - ballPos) / ballToPocket;
                if (_IsBallToPocketBlocked(ballPos, pocketPos, b, 0f, table.pocketLocations[p])) continue;
                if (_IsPathCrossesCushion(ballPos, pocketPos)) continue;

                Vector3 targetGhost = ballPos - t2pDir * BALL_DIAMETER;
                if (Mathf.Abs(targetGhost.x) > table.k_TABLE_WIDTH + BALL_RADIUS * 0.5f
                    || Mathf.Abs(targetGhost.z) > table.k_TABLE_HEIGHT + BALL_RADIUS * 0.5f) continue;

                for (int m = 1; m <= 15; m++)
                {
                    if (m == b) continue;
                    if ((targetBalls & (1u << m)) == 0) continue;
                    if ((table.ballsPocketedLocal & (1u << m)) != 0) continue;
                    Vector3 mPos = table.ballsP[m];

                    Vector3 mToTarget = targetGhost - mPos;
                    float mToTargetDist = mToTarget.magnitude;
                    if (mToTargetDist < 0.05f || mToTargetDist > 2.0f) continue;

                    Vector3 mToTargetDir = mToTarget / mToTargetDist;
                    float comboAlign = Vector3.Dot(mToTargetDir, t2pDir);
                    if (comboAlign < 0.60f) continue;

                    Vector3 mGhost = mPos - mToTargetDir * BALL_DIAMETER;
                    if (Mathf.Abs(mGhost.x) > table.k_TABLE_WIDTH + BALL_RADIUS * 0.5f
                        || Mathf.Abs(mGhost.z) > table.k_TABLE_HEIGHT + BALL_RADIUS * 0.5f) continue;

                    Vector3 cueToGhost = mGhost - cuePos;
                    float shotDist = cueToGhost.magnitude;
                    if (shotDist < 0.05f || shotDist > 2.5f) continue;

                    Vector3 aimDir = cueToGhost / shotDist;
                    float cueAlign = Vector3.Dot(aimDir, mToTargetDir);
                    if (cueAlign < 0.72f)
                    {
                        if (debug) _Log("[NPC] 跳过: 组合 球" + m + "->球" + b + "->袋" + p + " 母球入射角不足 align=" + cueAlign.ToString("F2"));
                        continue;
                    }
                    if (!_IsPathClear(cuePos, mGhost, m))
                    {
                        if (debug) _Log("[NPC] 跳过: 组合 球" + m + "->球" + b + "->袋" + p + " cue路径遮挡");
                        continue;
                    }
                    if (_IsPathCrossesCushion(cuePos, mGhost))
                    {
                        if (debug) _Log("[NPC] 跳过: 组合 球" + m + "->球" + b + "->袋" + p + " cue路径穿库");
                        continue;
                    }
                    if (!_IsPathClearBallsOnly(mPos, targetGhost, m))
                    {
                        if (debug) _Log("[NPC] 跳过: 组合 球" + m + "->球" + b + "->袋" + p + " 连传路径遮挡");
                        continue;
                    }
                    if (_IsPathCrossesCushion(mPos, targetGhost))
                    {
                        if (debug) _Log("[NPC] 跳过: 组合 球" + m + "->球" + b + "->袋" + p + " 连传路径穿库");
                        continue;
                    }

                    float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(mToTargetDir, t2pDir), -1f, 1f));
                    float score = comboAlign * 1.7f
                        + Mathf.Clamp01(1.0f - shotDist / 2.0f) * 0.3f
                        + Mathf.Clamp01(1.0f - mToTargetDist / 1.5f) * 0.2f
                        + Mathf.Clamp01(1.0f - ballToPocket / 1.8f) * 0.1f;

                    // 9-ball/10-ball: intermediate ball MUST be the lowest numbered ball (legal first hit)
                    if (table.is9Ball || table.is10Ball)
                    {
                        int lowestBallCombo = table.findLowestUnpocketedBall(table.ballsPocketedLocal);
                        if (lowestBallCombo > 0)
                        {
                            if (m == lowestBallCombo)
                                score += 0.5f; // strong bonus for legal combo
                            else
                                score *= 0.1f; // near-reject: hitting wrong ball first is foul
                        }
                    }

                    if (score > bestComboScore)
                    {
                        bestComboScore = score;
                        bestComboBall = b;
                        bestComboPocket = p;
                        bestComboIntermediateBall = m;
                        bestComboAimDir = aimDir;
                        bestComboShotDist = shotDist;
                        bestComboCutAngle = cutAngle;
                        bestComboImpact = mGhost;
                        if (debug) _Log("[NPC] 组合: 球" + m + "->球" + b + "->袋" + p + " score=" + score.ToString("F2") + " dist=" + shotDist.ToString("F2") + " 连传距=" + mToTargetDist.ToString("F2"));
                    }
                }
            }
        }

        if (bestComboBall < 0 || bestComboScore < 1.60f) return false;

        npcEvalBall = bestComboBall;
        npcEvalPocket = bestComboPocket;
        npcEvalShotType = 4;
        npcEvalKickCushion = -1;
        npcEvalKickCushion2 = -1;
        npcEvalComboIntermediateBall = bestComboIntermediateBall;
        npcEvalAimDir = bestComboAimDir;
        npcEvalFirstImpactPos = bestComboImpact;
        npcEvalShotDist = bestComboShotDist;
        npcEvalSpin = 0f;
        npcEvalCutAngle = bestComboCutAngle * Mathf.Rad2Deg;
        npcEvalScore = bestComboScore;
        return true;
    }

    private void _NpcFireSafetyShot()
    {
        if (npcHasDirectCandidate)
        {
            _Log("[NPC] 直球候选已存在,跳过安全球fallback");
            return;
        }
        Vector3 cuePos = table.ballsP[0];
        uint targetBalls = _GetTargetBalls();
        if (targetBalls == 0) targetBalls = ~table.ballsPocketedLocal & 0xFFFEu;

        // Opponent balls: mode-dependent
        uint opponentBalls = 0u;
        if (table.is9Ball || table.is10Ball)
        {
            // No opponent balls in 9/10-ball — all balls are shared
            opponentBalls = 0u;
        }
        else if (table.isSnooker)
        {
            // In Snooker, opponents are the "other phase" balls
            bool snkRedOnTable = table.sixRedCheckIfRedOnTable(table.ballsPocketedLocal, false);
            if (table.colorTurnLocal)
                opponentBalls = ~table.ballsPocketedLocal & table.SNOOKER_REDS_MASK; // reds are opponent during color turn
            else if (snkRedOnTable)
                opponentBalls = ~table.ballsPocketedLocal & 0x1AEu; // colors are opponent during red phase
            // else: all colors phase — all remaining are targets, no opponent balls
        }
        else // 8-ball
        {
            if (npcGroupId == 0) opponentBalls = ~table.ballsPocketedLocal & 0xFE00u & ~0x2u;
            else if (npcGroupId == 1) opponentBalls = ~table.ballsPocketedLocal & 0x1FCu & ~0x2u;
        }

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
            float cutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, ballToPocket), -1f, 1f));
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

        // Fallback: no clear direct path to any own ball → try thin-cut safety (薄边安全球)
        if (bestBall < 0)
        {
            // If ball positions are corrupted (all outside table), don't attempt safety shot
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
                return;
            }

            _safetyFailCount++;
            _Log("[NPC] 无直接路线 (第" + _safetyFailCount + "次), 尝试薄边安全球");

            // === PASS B: Thin-cut safety (薄边) — graze own ball edge ===
            // When direct center path is blocked, aim to just clip the edge of own ball.
            // Cue ball retains most speed after grazing and travels to a safe area.
            float bestThinScore = float.MinValue;
            int bestThinBall = -1;
            Vector3 bestThinAim = Vector3.zero;
            float bestThinCutAngle = 0f;
            int bestThinSide = 0;

            for (int b = 1; b <= 15; b++)
            {
                if ((targetBalls & (1u << b)) == 0) continue;
                if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                Vector3 ballPos = table.ballsP[b];
                Vector3 toBall = ballPos - cuePos;
                float dist = toBall.magnitude;
                if (dist < 0.02f) continue;
                Vector3 toBallDir = toBall / dist;

                // Try thin grazing on left and right edges
                for (int side = 0; side < 2; side++)
                {
                    Vector3 perp = new Vector3(-toBallDir.z, 0f, toBallDir.x);
                    // Offset ghost ball perpendicular so cue ball clips edge of target
                    // factor 0.85 = thin but reliable contact (~30° contact angle)
                    float grazeOffset = BALL_DIAMETER * 0.85f;
                    Vector3 ghostPos = ballPos + perp * (side == 0 ? grazeOffset : -grazeOffset);
                    Vector3 aimDir = (ghostPos - cuePos).normalized;

                    // Path must be clear to ghost position (exclude target ball — we want contact)
                    if (!_IsPathClear(cuePos, ghostPos, b)) continue;

                    // After thin grazing, cue ball barely deflects and continues ~forward
                    float ghostDist = (ghostPos - cuePos).magnitude;
                    Vector3 postDir = Vector3.Lerp(aimDir, toBallDir, 0.06f).normalized;
                    float travelDist = Mathf.Max(0.5f, 2.5f - ghostDist);
                    Vector3 cueEndpoint = ghostPos + postDir * travelDist;

                    // Clamp endpoint within table bounds
                    cueEndpoint.x = Mathf.Clamp(cueEndpoint.x, -table.k_TABLE_WIDTH + 0.02f, table.k_TABLE_WIDTH - 0.02f);
                    cueEndpoint.z = Mathf.Clamp(cueEndpoint.z, -table.k_TABLE_HEIGHT + 0.02f, table.k_TABLE_HEIGHT - 0.02f);

                    // Safety score: maximize distance to opponent balls, minimize opponent opportunities
                    float minOppDist = float.MaxValue;
                    for (int ob = 1; ob <= 15; ob++)
                    {
                        if ((opponentBalls & (1u << ob)) == 0) continue;
                        if ((table.ballsPocketedLocal & (1u << ob)) != 0) continue;
                        float d = (cueEndpoint - table.ballsP[ob]).magnitude;
                        if (d < minOppDist) minOppDist = d;
                    }
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
                    // Bonus for closer targets (easier to execute thin cut reliably)
                    score += Mathf.Clamp01(1.0f - dist / 2.0f) * 0.3f;
                    // Penalize if post-graze path goes through pocket area (prevents self-scratch)
                    for (int p = 0; p < 6; p++)
                    {
                        if (!_IsPathClear(cueEndpoint, npcPockets[p], -1)) continue;
                        Vector3 pocketDir = (npcPockets[p] - cueEndpoint).normalized;
                        Vector3 toEnd = cueEndpoint - ghostPos;
                        if (toEnd.magnitude < 0.01f) continue;
                        Vector3 endDir = toEnd.normalized;
                        float aligned = Vector3.Dot(endDir, pocketDir);
                        if (aligned > 0.92f) score -= 0.8f;
                    }

                    if (score > bestThinScore)
                    {
                        bestThinScore = score;
                        bestThinBall = b;
                        bestThinAim = aimDir;
                        bestThinCutAngle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(aimDir, toBallDir), -1f, 1f));
                        bestThinSide = side;
                    }
                }
            }

            if (bestThinBall >= 0 && bestThinScore > -2f)
            {
                _safetyFailCount = 0;
                float thinDist = (table.ballsP[bestThinBall] - cuePos).magnitude;
                float thinPower = Mathf.Clamp(thinDist * 1.5f / 4.0f, MIN_POWER, 0.25f);

                npcAimDir = bestThinAim;
                npcPower = thinPower;
                npcSpinValue = 0f;
                npcTargetBall = bestThinBall;
                npcTargetPocket = -1;
                npcShotType = 3; // thin cut (轻碰)
                npcCutAngle = bestThinCutAngle * Mathf.Rad2Deg;
                npcBestScore = 0f;

                _Log("[NPC] 薄边安全球: 击球" + bestThinBall
                    + " 边=" + (bestThinSide == 0 ? "L" : "R")
                    + " 切角=" + npcCutAngle.ToString("F1") + "°"
                    + " 距=" + thinDist.ToString("F2")
                    + " 力=" + npcPower.ToString("F2")
                    + " 分=" + bestThinScore.ToString("F2"));
                npcChargeDuration = testMode ? 0.5f : 1.0f;
                npcChargeElapsed = 0f;
                if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                if (testMode) _RecordShotPre();
                npcState = NPC_CHARGING;
                return;
            }

            // Thin-cut also failed — try cushion kick to own balls as last resort after 3 failures
            if (_safetyFailCount >= 3)
            {
                _Log("[NPC] 薄边失败3次, 尝试勾库安全球(最后手段)");

                float bestKickDist = float.MaxValue;
                int bestKickBall = -1;
                Vector3 bestKickBounce = Vector3.zero;
                int bestKickCush = -1;

                for (int b = 1; b <= 15; b++)
                {
                    if ((targetBalls & (1u << b)) == 0) continue;
                    if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                    Vector3 tPos = table.ballsP[b];
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 cp = _GetCushionPoint(cuePos, tPos, c);
                        if (cp.x == float.MaxValue) continue;
                        if (!_IsCushionBouncePointValid(cp, c)) continue;
                        if (!_IsPathClearBallsOnly(cuePos, cp, -1)) continue;
                        if (!_IsPathClearBallsOnly(cp, tPos, b)) continue;
                        float td = (cp - cuePos).magnitude + (tPos - cp).magnitude;
                        if (td < bestKickDist) { bestKickDist = td; bestKickBall = b; bestKickBounce = cp; bestKickCush = c; }
                    }
                }

                if (bestKickBall >= 0)
                {
                    _safetyFailCount = 0;
                    Vector3 kAim = (bestKickBounce - cuePos).normalized;
                    float kDist = (bestKickBounce - cuePos).magnitude;
                    npcAimDir = kAim;
                    npcPower = Mathf.Clamp(kDist * 1.5f / 4.0f, MIN_POWER, 0.26f);
                    npcSpinValue = 0f;
                    npcTargetBall = bestKickBall;
                    npcTargetPocket = -1;
                    npcShotType = 2;
                    npcKickCushion = bestKickCush;
                    npcKickCushion2 = -1;
                    npcCutAngle = 0f;
                    npcBestScore = 0f;
                    _Log("[NPC] 勾库安全球(最后): 击球" + bestKickBall + " 库" + bestKickCush + " 力=" + npcPower.ToString("F2"));
                    npcChargeDuration = testMode ? 0.5f : 1.0f;
                    npcChargeElapsed = 0f;
                    if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                    table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                    if (testMode) _RecordShotPre();
                    npcState = NPC_CHARGING;
                    return;
                }
            }

            // Ultimate fallback: no safety shot found → force a gentle random tap to avoid infinite wait
            _safetyFallbackCount++;
            if (_safetyFallbackCount > 3)
            {
                _Log("[NPC] 安全球fallback重试" + _safetyFallbackCount + "次, 强制轻推任意球打破僵局");
                _safetyFallbackCount = 0;
                _safetyFailCount = 0;
                // Force a gentle tap on the nearest own ball as last resort
                float bestForceDist = float.MaxValue;
                int forceBall = -1;
                for (int b = 1; b <= 15; b++)
                {
                    if ((targetBalls & (1u << b)) == 0) continue;
                    if ((table.ballsPocketedLocal & (1u << b)) != 0) continue;
                    float d = (table.ballsP[b] - cuePos).magnitude;
                    if (d < bestForceDist) { bestForceDist = d; forceBall = b; }
                }
                if (forceBall >= 0)
                {
                    Vector3 forceAim = (table.ballsP[forceBall] - cuePos).normalized;
                    npcAimDir = forceAim;
                    npcPower = MIN_POWER;
                    npcSpinValue = -0.3f;
                    npcTargetBall = forceBall;
                    npcTargetPocket = -1;
                    npcShotType = 0;
                    npcCutAngle = 0f;
                    npcBestScore = 0f;
                    _Log("[NPC] 强制轻推: 球" + forceBall + " 距=" + bestForceDist.ToString("F2"));
                    npcChargeDuration = 0.5f;
                    npcChargeElapsed = 0f;
                    if (table.activeCue != null) table.activeCue._SetNpcControlled(true);
                    table.desktopManager._NpcStartCharge(npcAimDir, npcPower, npcChargeDuration, npcSpinValue);
                    npcState = NPC_CHARGING;
                    return;
                }
                // Truly nothing to hit — just wait
                npcTimer = 5f;
                return;
            }
            _Log("[NPC] 安全球全层失败(第" + _safetyFallbackCount + "次), 2秒后重试");
            npcTimer = 2f;
            return;
        }
        _safetyFallbackCount = 0;
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
        _safetyFallbackCount = 0;
        _corruptionSkipCount = 0;
        _breakWaitCount = 0;
        _consecutiveMissCount = 0;
        _consecutiveMissMask = 0u;
        if (table.activeCue != null)
        {
            table.activeCue._SetNpcControlled(false);
        }
        if (table.desktopManager != null)
        {
            table.desktopManager._NpcFinishCharge();
        }
        _isFirstNpcShot = true;
        _gameWasLive = false;
    }
}
