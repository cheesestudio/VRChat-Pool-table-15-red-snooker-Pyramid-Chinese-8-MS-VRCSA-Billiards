#define EIJIS_ISSUE_FIX
#define EIJIS_TABLE_LABEL
#define EIJIS_MANY_BALLS
#define EIJIS_SNOOKER15REDS
#define EIJIS_PYRAMID
#define EIJIS_CUEBALLSWAP
#define EIJIS_CAROM
#define EIJIS_CUSHION_EFFECT
#define EIJIS_GUIDELINE2TOGGLE
#define EIJIS_PUSHOUT
#define EIJIS_CALLSHOT
#define EIJIS_SEMIAUTOCALL
#define EIJIS_10BALL
#define CHEESE_ISSUE_FIX

// #define EIJIS_DEBUG_INITIALIZERACK
// #define EIJIS_DEBUG_BALLCHOICE
// #define EIJIS_DEBUG_PIRAMIDSCORE
// #define EIJIS_DEBUG_CUSHIONTOUCH
// #define EIJIS_DEBUG_PUSHOUT
// #define EIJIS_DEBUG_AFTERBREAK
// #define EIJIS_DEBUG_CALLSHOT_BALL
#define EIJIS_DEBUG_BREAKINGFOUL
//#define EIJIS_DEBUG_SNOOKER_COLOR_POINT
// #define EIJIS_DEBUG_10BALL_WPA_RULE

#if UNITY_ANDROID
#define HT_QUEST
#endif

#if !HT_QUEST || true
#define HT8B_DEBUGGER
#endif

#if UDON_CHIPS
using UCS;
#endif

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;
using Metaphira.Modules.CameraOverride;
using TMPro;
using Cheese;
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BilliardsModule : UdonSharpBehaviour
{
    [NonSerialized] public readonly string[] DEPENDENCIES = new string[] { nameof(CameraOverrideModule) };
#if EIJIS_SNOOKER15REDS || EIJIS_PYRAMID || EIJIS_CAROM || EIJIS_10BALL
    [NonSerialized] public readonly string VERSION = "6.0.0 (15Reds|Pyramid|Carom|10Ball)";
#else
    [NonSerialized] public readonly string VERSION = "6.0.0";
#endif

    #region PhysicsVariables

    // table model properties
    [NonSerialized] public float k_TABLE_WIDTH; // horizontal span of table
    [NonSerialized] public float k_TABLE_HEIGHT; // vertical span of table
    [NonSerialized] public float k_CUSHION_RADIUS; // The roundess of colliders
    [NonSerialized] public float k_POCKET_WIDTH_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_HEIGHT_CORNER; // Radius of pockets
    [NonSerialized] public float k_POCKET_RADIUS_SIDE; // Radius of side pockets
    [NonSerialized] public float k_POCKET_DEPTH_SIDE; // Depth of side pockets
    [NonSerialized] public float k_INNER_RADIUS_CORNER; // Pocket 'hitbox' cylinder
    [NonSerialized] public float k_INNER_RADIUS_SIDE; // Pocket 'hitbox' cylinder for corner pockets
    [NonSerialized] public float k_FACING_ANGLE_CORNER; // Angle of corner pocket inner walls
    [NonSerialized] public float k_FACING_ANGLE_SIDE; // Angle of side pocket inner walls
    [NonSerialized] public float K_BAULK_LINE; // Snooker baulk line distance from end of table
    [NonSerialized] public float K_BLACK_SPOT; // Snooker Black ball distance from end of table
    [NonSerialized] public float k_SEMICIRCLERADIUS; // Snooker, radius of D
    [NonSerialized] public float k_RAIL_HEIGHT_UPPER;
    [NonSerialized] public float k_RAIL_HEIGHT_LOWER;
    [NonSerialized] public float k_RAIL_DEPTH_WIDTH;
    [NonSerialized] public float k_RAIL_DEPTH_HEIGHT;
    // advanced physics  variables
    [NonSerialized] public float k_F_SLIDE; // bt_CoefSlide
    [NonSerialized] public float k_F_ROLL; // bt_CoefRoll
    [NonSerialized] public float k_F_SPIN; // bt_CoefSpin
    [NonSerialized] public float k_F_SPIN_RATE; // bt_CoefSpinRate
    [NonSerialized] public bool useRailLower; // useRailHeightLower
    [NonSerialized] public bool isDRate; // bt_isDRate
    [NonSerialized] public float K_BOUNCE_FACTOR; // BounceFactor
    [NonSerialized] public float k_POCKET_RESTITUTION; // Reduces bounce inside of pockets
    [Header("Cushion Model:")]
    [NonSerialized] public bool isHanModel; // bc_UseHan05
    [NonSerialized] public float k_E_C; // bc_CoefRestitution
    [NonSerialized] public bool isDynamicRestitution; // bc_DynRestitution
    [NonSerialized] public bool isCushionFrictionConstant; // bc_UseConstFriction
    [NonSerialized] public float k_Cushion_MU; // bc_ConstFriction
    [Header("Ball Set Configuration:")]
    [NonSerialized] public float k_BALL_E; // bs_CoefRestitution
    [NonSerialized] public float muFactor; // bs_Friction
    [NonSerialized] public float k_BALL_RADIUS; // Radius of balls
    [NonSerialized] public float k_BALL_MASS; // Mass of balls
    [NonSerialized] public float k_BALL_DIAMETRE; // Diameter of balls
    [NonSerialized] public Vector3 k_vE; // corner pocket data
    [NonSerialized] public Vector3 k_vF; // side pocket data
    [NonSerialized] public Vector3 k_rack_position = new Vector3();
    private Vector3 k_rack_direction = new Vector3();
    private GameObject auto_rackPosition;
    [NonSerialized] public GameObject auto_pocketblockers;
    private GameObject auto_colliderBaseVFX;
    [NonSerialized] public MeshRenderer[] tableMRs;
#if EIJIS_CALLSHOT
    [NonSerialized] public GameObject[] pointPocketMarkers;
    [NonSerialized] public GameObject[] pointPocketMarkerSphere;
#if EIJIS_SEMIAUTOCALL
    private float findNearestPocket_x;
    private float findNearestPocket_n;
#endif
#endif

    #endregion

    // cue guideline
    private readonly Color k_aimColour_aim = new Color(0.7f, 0.7f, 0.7f, 1.0f);
    private readonly Color k_aimColour_locked = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    // textures
    [SerializeField] public Texture[] textureSets;
    [SerializeField] public ModelData[] tableModels;
    [SerializeField] public Texture2D[] tableSkins;
    [SerializeField] public Texture2D[] cueSkins;

    // hooks
    [NonSerialized] public UdonBehaviour tableSkinHook;//no need to use

    [Header("CBC_Plug")]
    [SerializeField] public TableHook tableHook;
    [Space(3)]
    [SerializeField] public UdonBehaviour nameColorHook;
    [SerializeField] public ScoreManagerV2 ScoreManager;
    [SerializeField] public Translations _translations;
    [SerializeField] public PersonalDataCounter personalData;
    [SerializeField] public UdonBehaviour DG_LAB;    //芝士郊狼联动

    // globals
    [NonSerialized] public AudioSource aud_main;
    [NonSerialized] public UdonBehaviour callbacks;
#if EIJIS_PYRAMID || EIJIS_CAROM || EIJIS_10BALL
    private Vector3[][] initialPositions = new Vector3[11][];
    private uint[] initialBallsPocketed = new uint[11];
#else
    private Vector3[][] initialPositions = new Vector3[5][];
    private uint[] initialBallsPocketed = new uint[5];
#endif

#if UDON_CHIPS
    //udon Chips
    [Header("Udon Chips")]
    [SerializeField] public int Enter_cost;
    [SerializeField] public int winner_gain;
    [SerializeField] public int loser_lose;
    private UCS.UdonChips udonChips = null;
#endif


    #region BallModeSetting

    // constants
#if EIJIS_MANY_BALLS
    [NonSerialized] public const int MAX_BALLS = 32;
#endif
#if EIJIS_PYRAMID
    [NonSerialized] public const int PYRAMID_BALLS = 16;
#endif
    private const float k_RANDOMIZE_F = 0.0001f;
    private float k_SPOT_POSITION_X = 0.5334f; // First X position of the racked balls
    private const float k_SPOT_CAROM_X = 0.8001f; // Spot position for carom mode
#if EIJIS_SNOOKER15REDS
    private readonly int[] sixredsnooker_ballpoints =
    {
        0, 7, 2, 5, 1, 6, 1, 3,
        4, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 1, 1, 1, 1, 1, 0
    };
    private readonly uint SNOOKER_BALLS_MASK = 0x7E00FFFEu;
    private readonly uint SNOOKER_REDS_MASK = 0x7E00FE50u;
    private const int SNOOKER_REDS_COUNT = 15;
    [NonSerialized]
    public readonly int[] break_order_sixredsnooker =
    {
        4, 6, 9, 10, 11,
        12, 13, 14, 15, 25,
        26, 27, 28, 29, 30,
        2, 7, 8, 3, 5,
        1
    };
#else
    private readonly int[] sixredsnooker_ballpoints = { 0, 7, 2, 5, 1, 6, 1, 3, 4, 1, 1, 1, 1 };
    private readonly int[] break_order_sixredsnooker = { 4, 6, 9, 10, 11, 12, 2, 7, 8, 3, 5, 1 };
#endif
    private readonly int[] break_order_8ball = { 9, 2, 10, 11, 1, 3, 4, 12, 5, 13, 14, 6, 15, 7, 8 };
    private readonly int[] break_order_9ball = { 2, 3, 4, 5, 9, 6, 7, 8, 1 };
    private readonly int[] break_rows_9ball = { 0, 1, 2, 1, 0 };
#if EIJIS_10BALL
    private readonly int[] break_order_10ball = { 2, 1, 9, 8, 10, 5, 3, 6, 7, 4 };
#endif
#if EIJIS_CALLSHOT
    private readonly uint pocket_mask_8ball = 0xFFFEu;
    private readonly uint pocket_mask_9ball = 0x03FEu;
#if EIJIS_10BALL
    private readonly uint pocket_mask_10ball = 0x07FEu;
#endif
    private uint pocketMask = 0x0;
    [NonSerialized] public int ballsLengthByPocketGame = 15;
#endif

    #region InspectorValues
#if EIJIS_TABLE_LABEL
    [Header("Table Label")]
    [SerializeField] public string logLabel;

#endif
    [Header("Managers")]
    [SerializeField] public NetworkingManager networkingManager;
    [SerializeField] public PracticeManager practiceManager;
    [SerializeField] public RepositionManager repositionManager;
    [SerializeField] public DesktopManager desktopManager;
    [SerializeField] public CameraManager cameraManager;
    [SerializeField] public GraphicsManager graphicsManager;
    [SerializeField] public MenuManager menuManager;
    [SerializeField] public UdonSharpBehaviour[] PhysicsManagers;

    [Header("Camera Module")]
    [SerializeField] public UdonSharpBehaviour cameraModule;

    [Space(10)]
    [Header("Sound Effects")]
    [SerializeField] AudioClip snd_Intro;
    [SerializeField] AudioClip snd_Sink;
    [SerializeField] AudioClip snd_OutOfBounds;
    [SerializeField] AudioClip snd_NewTurn;
    [SerializeField] AudioClip snd_PointMade;
    [SerializeField] public AudioClip snd_btn;
    [SerializeField] public AudioClip snd_spin;
    [SerializeField] public AudioClip snd_spinstop;
    [SerializeField] AudioClip snd_hitball;

    [Space(10)]
    [Header("Other")]
    public float LoDDistance = 10;
    [Tooltip("Shuffle positions of ball spawn points in 8ball and 9ball?")]
    public bool RandomizeBallPositions = true;

    [Space(10)]
    [Header("Table Light Colors")]
    // table colors
    [SerializeField] public Color k_colour_foul;        // v1.6: ( 1.2, 0.0, 0.0, 1.0 )
    [SerializeField] public Color k_colour_default;     // v1.6: ( 1.0, 1.0, 1.0, 1.0 )
    [SerializeField] public Color k_colour_off = new Color(0.01f, 0.01f, 0.01f, 1.0f);

    // 8/9 ball
    [SerializeField] public Color k_teamColour_spots;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_teamColour_stripes; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // Snooker
    [SerializeField] public Color k_snookerTeamColour_0;   // v1.6: ( 0.00, 0.75, 1.75, 1.0 )
    [SerializeField] public Color k_snookerTeamColour_1; // v1.6: ( 1.75, 0.25, 0.00, 1.0 )

    // 4 ball
    [SerializeField] public Color k_colour4Ball_team_0; // v1.6: ( )
    [SerializeField] public Color k_colour4Ball_team_1; // v1.6: ( 2.0, 1.0, 0.0, 1.0 )

    // fabrics
    [SerializeField][HideInInspector] public Color k_fabricColour_8ball; // v1.6: ( 0.3, 0.3, 0.3, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_9ball; // v1.6: ( 0.1, 0.6, 1.0, 1.0 )
    [SerializeField][HideInInspector] public Color k_fabricColour_4ball; // v1.6: ( 0.15, 0.75, 0.3, 1.0 )

    [Space(10)]
    [Header("Internal (no touching!)")]
    // Other scripts
    [SerializeField] public CueController[] cueControllers;

    // GameObjects
    [SerializeField] public GameObject[] balls;
    [SerializeField] public GameObject guideline;
    [SerializeField] public GameObject guideline2;
    [SerializeField] public GameObject devhit;
    [SerializeField] public GameObject markerObj;
    [SerializeField] public GameObject marker9ball;
    [NonSerialized] public Transform tableSurface;

    // Texts
    [SerializeField] Text ltext;
    [SerializeField] TextMeshProUGUI infReset;

    public ReflectionProbe reflection_main;
#if EIJIS_CALLSHOT
    
    [Header("Pocket Billiard Call-shot")]
    [SerializeField] public GameObject markerCalledBall;
    [SerializeField] Material calledBallMarkerBlue;
    [SerializeField] Material calledBallMarkerOrange;
    [SerializeField] Material calledBallMarkerWhite;
    [SerializeField] Material calledBallMarkerGray;
#endif
    #endregion

    #endregion

    #region DebugSetting

    // debugger
    [NonSerialized] public int PERF_MAIN = 0;
    [NonSerialized] public int PERF_PHYSICS_MAIN = 1;
    [NonSerialized] public int PERF_PHYSICS_VEL = 2;
    [NonSerialized] public int PERF_PHYSICS_BALL = 3;
    [NonSerialized] public int PERF_PHYSICS_CUSHION = 4;
    [NonSerialized] public int PERF_PHYSICS_POCKET = 5;

    [NonSerialized] public const int PERF_MAX = 6;
    private string[] perfNames = new string[] {
      "main",
      "physics",
      "physicsVel",
      "physicsBall",
      "physicsCushion",
      "physicsPocket"
   };
    private float[] perfCounters = new float[PERF_MAX];
    private float[] perfTimings = new float[PERF_MAX];
    private float[] perfStart = new float[PERF_MAX];
    private const int LOG_MAX = 32;
    private int LOG_LEN = 0;
    private int LOG_PTR = 0;
    private string[] LOG_LINES = new string[32];

    #endregion

    // cached copies of networked data, may be different from local game state
    [NonSerialized] public int[] playerIDsCached = { -1, -1, -1, -1 };//the 4 is MAX_PLAYERS from NetworkingManager

    #region LocalState

    // local game state
    [NonSerialized] public bool lobbyOpen;
    [NonSerialized] public bool gameLive;
    [NonSerialized] public uint gameModeLocal;
    [NonSerialized] public uint timerLocal;
    [NonSerialized] public bool teamsLocal;
    [NonSerialized] public bool noGuidelineLocal;
#if EIJIS_GUIDELINE2TOGGLE
    [NonSerialized] public bool noGuideline2Local;
#endif
    [NonSerialized] public bool noLockingLocal;
#if EIJIS_10BALL
    [NonSerialized] public bool wpa10BallRuleLocal;
#endif
#if EIJIS_CALLSHOT
    [NonSerialized] public bool requireCallShotLocal;
#if EIJIS_SEMIAUTOCALL
    [NonSerialized] public bool semiAutoCallLocal;
#endif
#endif
    [NonSerialized] public uint ballsPocketedLocal;
#if EIJIS_CALLSHOT
    [NonSerialized] public uint targetPocketedLocal;
    [NonSerialized] public uint otherPocketedLocal;
    [NonSerialized] public uint pointPocketsLocal;
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT
    [NonSerialized] public uint calledBallsLocal;
#endif
    [NonSerialized] public uint teamIdLocal;
    [NonSerialized] public uint fourBallCueBallLocal;
    [NonSerialized] public bool isTableOpenLocal;
    [NonSerialized] public uint teamColorLocal;
    [NonSerialized] public int numPlayersCurrent = 0;
    [NonSerialized] public int numPlayersCurrentOrange = 0;
    [NonSerialized] public int numPlayersCurrentBlue = 0;
    [NonSerialized] public int[] playerIDsLocal = { -1, -1, -1, -1 };
    [NonSerialized] public byte[] fbScoresLocal = new byte[2];
    [NonSerialized] public uint winningTeamLocal;
    [NonSerialized] public int activeCueSkin;
    [NonSerialized] public int tableSkinLocal;
    [NonSerialized] public byte gameStateLocal;
    private byte turnStateLocal;
    private int timerStartLocal;
    [NonSerialized] public uint foulStateLocal;
    [NonSerialized] public int tableModelLocal;
    [NonSerialized] public bool colorTurnLocal;

    //Cheese Addition
    [NonSerialized] public bool BreakFinish;
    [NonSerialized] public int ShotCounts;
    [NonSerialized] public int HeightBreak;
#if EIJIS_PYRAMID
    [NonSerialized] public const uint GAMEMODE_PYRAMID = 5u;
#endif
#if EIJIS_CAROM
    [NonSerialized] public const uint GAMEMODE_3CUSHION = 6u;
    [NonSerialized] public const uint GAMEMODE_2CUSHION = 7u;
    [NonSerialized] public const uint GAMEMODE_1CUSHION = 8u;
    [NonSerialized] public const uint GAMEMODE_0CUSHION = 9u;
#endif
#if EIJIS_10BALL
    [NonSerialized] public const uint GAMEMODE_10BALL = 10u;
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_PUSHOUT || EIJIS_CALLSHOT
    [NonSerialized] public int stateIdLocal;
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT
    private bool calledBallOff = false;
    private int calledBallId = -2;
    private float calledBallIdDelayTimestamp = 0;
    private float callDelay = 0.4f;
#endif
#if EIJIS_CALLSHOT
    private bool calledPocketOff = false;
    private int calledPocketId = -2;
    private float calledPocketIdDelayTimestamp = 0;
    private bool callShotLockLocal;
#if EIJIS_SEMIAUTOCALL
    private bool semiAutoCalledPocket;
    private float semiAutoCalledTimeBall;
    private bool semiAutoCallTick;
#endif
#endif
#if EIJIS_PUSHOUT
    [NonSerialized] public byte pushOutStateLocal;
    [NonSerialized] public readonly byte PUSHOUT_BEFORE_BREAK = 0;
    // [NonSerialized] public readonly byte PUSHOUT_ILLEGAL_REACTIONING = 1;
    [NonSerialized] public readonly byte PUSHOUT_DONT = 2;
    [NonSerialized] public readonly byte PUSHOUT_DOING = 3;
    [NonSerialized] public readonly byte PUSHOUT_REACTIONING = 4;
    [NonSerialized] public readonly byte PUSHOUT_ENDED = 5;
#if EIJIS_DEBUG_PUSHOUT
    private string[] PushOutState = new string[] {
        "BEFORE_BREAK",
        "ILLEGAL_REACTIONING",
        "DONT",
        "DOING",
        "REACTIONING",
        "ENDED"
    }; 
#endif
#endif

    #endregion

    // physics simulation data, must be reset before every simulation
    [NonSerialized] public bool isLocalSimulationRunning;
    [NonSerialized] public bool waitingForUpdate;
    [NonSerialized] public bool isLocalSimulationOurs = false;
    [NonSerialized] public int simulationOwnerID;
    private uint numBallsHitCushion = 0; // used to check if 9ball break was legal (4 balls must hit cushion)
    private bool[] ballhasHitCushion;
    private bool ballBounced;//tracks if any ball has touched the cushion after initial ball collision
    private uint ballsPocketedOrig;
#if EIJIS_CALLSHOT
    private uint targetPocketedOrig;
    private uint otherPocketedOrig;
#endif
    private int firstHit = 0;
    private int secondHit = 0;
    private int thirdHit = 0;
    private bool jumpShotFoul;
    private bool fallOffFoul;
#if EIJIS_CAROM
    private int cushionBeforeSecondBall = 0;
    private int cushionHitGoal = 3;
#endif

    private bool fbMadePoint = false;
    private bool fbMadeFoul = false;

    // game state data
#if EIJIS_MANY_BALLS
    [NonSerialized] public Vector3[] ballsP = new Vector3[MAX_BALLS];
    [NonSerialized] public Vector3[] ballsV = new Vector3[MAX_BALLS];
    [NonSerialized] public Vector3[] ballsW = new Vector3[MAX_BALLS];
#else
    [NonSerialized] public Vector3[] ballsP = new Vector3[16];
    [NonSerialized] public Vector3[] ballsV = new Vector3[16];
    [NonSerialized] public Vector3[] ballsW = new Vector3[16];
#endif

    [NonSerialized] public bool canPlayLocal;
    [NonSerialized] public bool isGuidelineValid;
    [NonSerialized] public bool canHitCueBall = false;
    [NonSerialized] public bool isReposition = false;
    [NonSerialized] public float repoMaxX;
    [NonSerialized] public bool timerRunning = false;

    [NonSerialized] public int localPlayerId = -1;
    [NonSerialized] public uint localTeamId = uint.MaxValue;

    [NonSerialized] public UdonSharpBehaviour currentPhysicsManager;
    [NonSerialized] public CueController activeCue;

    // some udon optimizations
    [NonSerialized] public bool is8Ball = false;
    [NonSerialized] public bool is9Ball = false;
#if EIJIS_10BALL
    [NonSerialized] public bool is10Ball = false;
#endif
    [NonSerialized] public bool is4Ball = false;
    [NonSerialized] public bool isJp4Ball = false;
    [NonSerialized] public bool isKr4Ball = false;
#if EIJIS_SNOOKER15REDS
    [NonSerialized] public bool isSnooker = false;
    [NonSerialized] public bool isSnooker15Red = false;
#endif
    [NonSerialized] public bool isSnooker6Red = false;
#if EIJIS_PYRAMID
    [NonSerialized] public bool isPyramid = false;
    [NonSerialized] public bool isChinese8Ball = false;
#endif
#if EIJIS_CAROM
    [NonSerialized] public bool is3Cusion = false;
    [NonSerialized] public bool is2Cusion = false;
    [NonSerialized] public bool is1Cusion = false;
    [NonSerialized] public bool is0Cusion = false;
#endif
    [NonSerialized] public bool isPracticeMode = false;
    [NonSerialized] public bool isPlayer = false;
    [NonSerialized] public bool isOrangeTeamFull = false;
    [NonSerialized] public bool isBlueTeamFull = false;
    [NonSerialized] public bool localPlayerDistant = false;
#if EIJIS_CALLSHOT
    [NonSerialized] public Vector3[] pocketLocations = new Vector3[6];
#if EIJIS_SEMIAUTOCALL
    private Vector3[] findEasiestBallAndPocketConditions = new Vector3[]
    {
        // x:deg, y:t2p, z:c2t
        new Vector3(60.0f, 0.09f, 0.09f), // 0.3f, 0.3f // 0.06 * 5
        new Vector3(60.0f, 0.09f, 0.36f), // 0.3f, 0.6f // 0.06 * 5
        new Vector3(45.0f, 0.36f, 0.36f), // 0.6f, 0.6f // 0.06 * 10
        new Vector3(30.0f, 0.81f, 1.42f), // 0.9f, 1.2f // 0.06 * 12
        new Vector3(60.0f, 0.09f, float.MaxValue), // 0.3f, - // 0.06 * 5
        new Vector3(30.0f, 0.81f, 0.81f), // 0.9f, 0.9f // 0.06 * 12
        new Vector3(45.0f, 0.36f, float.MaxValue), // 0.6f, - // 0.06 * 10
        new Vector3(30.0f, 0.81f, float.MaxValue), // 0.9f, - // 0.06 * 12
        new Vector3(15.0f, float.MaxValue, float.MaxValue),
        new Vector3(30.0f, float.MaxValue, float.MaxValue),
        new Vector3(45.0f, float.MaxValue, float.MaxValue),
        new Vector3(60.0f, float.MaxValue, float.MaxValue),
        new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)
    };
#endif
#endif

    // use this to make sure max simulation is always visible
    [System.NonSerializedAttribute] public bool noLOD;
    // Add 1 to noLOD_ using SetProgramVariable() to prevent LoD check, subtract to undo
    // this allows more than one other script to disable LoD simultaniously
    [System.NonSerializedAttribute, FieldChangeCallback(nameof(noLOD__))] public int noLOD_ = 0;
    public int noLOD__
    {
        set
        {
            noLOD = value > 0;
            noLOD_ = value;
        }
        get => noLOD_;
    }
    bool checkingDistant;
    GameObject debugger;
    [NonSerialized] public CameraOverrideModule cameraOverrideModule;
    public string[] moderators = new string[0];
    [NonSerialized] public const float ballMeshDiameter = 0.06f;//the ball's size as modeled in the mesh file
    private void OnEnable()
    {
#if EIJIS_TABLE_LABEL
        logLabel = string.IsNullOrEmpty(logLabel) ? string.Empty : " " + logLabel;
#endif
        _LogInfo("initializing billiards module");

        cameraOverrideModule = (CameraOverrideModule)_GetModule(nameof(CameraOverrideModule));

        resetCachedData();

        currentPhysicsManager = PhysicsManagers[0];

        for (int i = 0; i < balls.Length; i++)
        {
            ballsP[i] = balls[i].transform.localPosition;
            balls[i].GetComponentInChildren<Repositioner>(true)._Init(this, i);

            Rigidbody ballRB = balls[i].GetComponent<Rigidbody>();
            ballRB.maxAngularVelocity = 999;
        }

#if EIJIS_GUIDELINE2TOGGLE
        noGuideline2Local = true;
#endif
#if EIJIS_CALLSHOT
#if EIJIS_10BALL
        pocketMask = is10Ball ? pocket_mask_10ball : (is9Ball ? pocket_mask_9ball : pocket_mask_8ball);
        ballsLengthByPocketGame = (is9Ball
             ? break_order_9ball.Length
             : (is10Ball ? break_order_10ball.Length : break_order_8ball.Length));
#else
        pocketMask = is9Ball ? pocket_mask_9ball : pocket_mask_8ball;
        ballsLengthByPocketGame = (is9Ball
             ? break_order_9ball.Length
             : break_order_8ball.Length);
#endif
        requireCallShotLocal = false;
#if EIJIS_SEMIAUTOCALL
        semiAutoCallLocal = true;
#endif
        pointPocketsLocal = 0;
        calledBallsLocal = 0;

#endif
        aud_main = this.GetComponent<AudioSource>();
        cueControllers[1].TeamBlue = true;
        for (int i = 0; i < cueControllers.Length; i++)
        { cueControllers[i]._Init(); }
        networkingManager._Init(this);
#if EIJIS_GUIDELINE2TOGGLE
        networkingManager.noGuideline2Synced = noGuideline2Local;
#endif
#if EIJIS_CALLSHOT
        networkingManager.requireCallShotSynced = requireCallShotLocal;
#if EIJIS_SEMIAUTOCALL
        networkingManager.semiAutoCallSynced = semiAutoCallLocal;
#endif
        networkingManager.pointPocketsSynced = pointPocketsLocal;
        networkingManager.calledBallsSynced = calledBallsLocal;
#endif
        practiceManager._Init(this);
        repositionManager._Init(this);
        desktopManager._Init(this);
        cameraManager._Init(this);
        graphicsManager._Init(this);
        cameraOverrideModule._Init();
        menuManager._Init(this);
        for (int i = 0; i < tableModels.Length; i++)
        {
            tableModels[i].gameObject.SetActive(false);
            tableModels[i]._Init();
        }

        tableSurface = transform.Find("intl.balls");
#if EIJIS_CALLSHOT
        pointPocketMarkers = new GameObject[6];
        pointPocketMarkerSphere = new GameObject[pointPocketMarkers.Length];
        for (int i = 0; i < pointPocketMarkers.Length; i++)
        {
            Transform pointPocketMarker = tableSurface.Find($"PointPocketMarker_{i}");
            pointPocketMarkers[i] = pointPocketMarker.gameObject;
            pointPocketMarkerSphere[i] = pointPocketMarker.Find("Sphere").gameObject;
        }
#endif
        for (int i = 0; i < PhysicsManagers.Length; i++)
        {
            PhysicsManagers[i].SetProgramVariable("table_", this);
            PhysicsManagers[i].SendCustomEvent("_Init");
        }

        currentPhysicsManager.SendCustomEvent("_InitConstants");


#if EIJIS_ISSUE_FIX
        setTableModel(tableModelLocal);
#else
        setTableModel(0);
#endif

        infReset.text = string.Empty;

        debugger = this.transform.Find("debugger").gameObject;
        debugger.SetActive(true);

        Transform gdisplay = guideline.transform.GetChild(0);
        if (gdisplay)
            gdisplay.GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);
        Transform gdisplay2 = guideline2.transform.GetChild(0);
        if (gdisplay2)
            gdisplay2.GetComponent<MeshRenderer>().material.SetMatrix("_BaseTransform", this.transform.worldToLocalMatrix);

        if (LoDDistance > 0 && !checkingDistant)
        {
            checkingDistant = true;
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), UnityEngine.Random.Range(0, 1f));
        }

        //init table hook
        if(tableHook == null)
        {
            tableHook = GameObject.Find("TableHook (replica) 2").GetComponent<TableHook>();
            if (tableHook == null)
                Debug.Log("Please put table hook in scene! 请把Tablehook放入场景");
        }
        if (tableHook != null)
        {
            tableHook.AddTranslation(_translations);
            tableHook.AddBilliardsModule(this);
        }
    }

    private void OnDisable()
    {
        checkingDistant = false;
    }

    private void FixedUpdate()
    {
        currentPhysicsManager.SendCustomEvent("_FixedTick");
    }

    private void Update()
    {
        if (localPlayerDistant) { return; }
        desktopManager._Tick(gameModeLocal);
        // menuManager._Tick();

        _BeginPerf(PERF_MAIN);
        practiceManager._Tick();
        repositionManager._Tick();
        cameraManager._Tick();
        graphicsManager._Tick();
        tickTimer();
#if EIJIS_CALLSHOT
        _UpdateCalledBallMarker();
#if EIJIS_SEMIAUTOCALL
        if (semiAutoCallTick) tickSemiAutoCall();
#endif
#endif

        networkingManager._FlushBuffer();
        _EndPerf(PERF_MAIN);

        if (perfCounters[PERF_MAIN] % 500 == 0) _RedrawDebugger();
    }

    public UdonSharpBehaviour _GetModule(string type)
    {
        string[] parts = cameraModule.GetUdonTypeName().Split('.');
        if (parts[parts.Length - 1] == type)
        {
            return cameraModule;
        }
        return null;
    }

    #region Triggers
    public void _TriggerLobbyOpen()
    {
#if UDON_CHIPS
        udonChips = GameObject.Find("UdonChips").GetComponent<UdonChips>();
        if (udonChips.money >= Enter_cost)
        {
            udonChips.money -= Enter_cost;
        }
        else
        {
            _LogWarn("u need money to play");
            return;
        }
 
#endif
        if (lobbyOpen) return;
        menuManager._EnableLobbyMenu();
        networkingManager._OnLobbyOpened();

        Debug.Log("_TriggerLobbyOpen");

    }

    public void _TriggerTeamsChanged(bool teamsEnabled)
    {
        networkingManager._OnTeamsChanged(teamsEnabled);
    }

    public void _TriggerNoGuidelineChanged(bool noGuidelineEnabled)
    {
        networkingManager._OnNoGuidelineChanged(noGuidelineEnabled);
    }

#if EIJIS_GUIDELINE2TOGGLE
    public void _TriggerNoGuideline2Changed(bool noGuideline2Enabled)
    {
        networkingManager._OnNoGuideline2Changed(noGuideline2Enabled);
    }
    
#endif
    public void _TriggerNoLockingChanged(bool noLockingEnabled)
    {
        networkingManager._OnNoLockingChanged(noLockingEnabled);
    }

#if EIJIS_10BALL
    public void _TriggerWpa10BallRuleChanged(bool wpa10BallRuleEnabled)
    {
        networkingManager._OnWpa10BallRuleChanged(wpa10BallRuleEnabled);
    }
    
#endif
#if EIJIS_CALLSHOT
    public void _TriggerRequireCallShotChanged(bool callShotEnabled)
    {
        networkingManager._OnRequireCallShotChanged(callShotEnabled);
    }

#if EIJIS_SEMIAUTOCALL
    public void _TriggerSemiAutoCallChanged(bool semiAutoCallEnabled)
    {
        networkingManager._OnSemiAutoCallChanged(semiAutoCallEnabled);
    }

#endif
#endif
    public void _TriggerTimerChanged(byte timerSelected)
    {
        networkingManager._OnTimerChanged(timerSelected);
    }

    public void _TriggerTableModelChanged(uint TableModelSelected)
    {
        networkingManager._OnTableModelChanged(TableModelSelected);
    }

    public void _TriggerPhysicsChanged(uint PhysicsSelected)
    {
        networkingManager._OnPhysicsChanged(PhysicsSelected);
    }

    public void _TriggerGameModeChanged(uint newGameMode)
    {
        networkingManager._OnGameModeChanged(newGameMode);
    }

    public void _TriggerGlobalSettingsUpdated(int newPhysicsMode, int newTableModel)
    {
        networkingManager._OnGlobalSettingsChanged((byte)newPhysicsMode, (byte)newTableModel);
    }

    public void _TriggerCueBallHit()
    {
        if (!isMyTurn()) return;

        _LogWarn("trying to propagate cue ball hit, linear velocity is " + ballsV[0].ToString("F4") + " and angular velocity is " + ballsW[0].ToString("F4"));

        if (float.IsNaN(ballsV[0].x) || float.IsNaN(ballsV[0].y) || float.IsNaN(ballsV[0].z) || float.IsNaN(ballsW[0].x) || float.IsNaN(ballsW[0].y) || float.IsNaN(ballsW[0].z))
        {
            ballsV[0] = Vector3.zero;
            ballsW[0] = Vector3.zero;
            return;
        }

        _TriggerCueDeactivate();

        if (foulStateLocal == 5)//free ball
        {
            if (SixRedCheckObjBlocked(ballsPocketedLocal, colorTurnLocal, true) > 0)
            {
                _LogInfo("6RED: Free ball turn. First hit ball is counted as current objective ball.");
            }
        }

        networkingManager._OnHitBall(ballsV[0], ballsW[0]);
    }
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT
    public void _TriggerOtherBallHit(int ballId, bool desktop)
    {
        if (localTeamId != teamIdLocal && !isPracticeMode) return; // is there a better way to do this?

#if EIJIS_CALLSHOT
        if (!desktop && callShotLockLocal)
        {
            return;
        }

#endif
        if (!desktop && calledBallId == ballId)
        {
            return;
        }

        if (!desktop && Time.time < calledBallIdDelayTimestamp + callDelay)
        {
            return;
        }
        else
        {
#if EIJIS_DEBUG_BALLCHOICE
            _LogInfo($"  calledBallId = {calledBallId}, calledBallIdDelayTimestamp = {calledBallIdDelayTimestamp}, callDelay = {callDelay}");
#endif
            calledBallIdDelayTimestamp = Time.time;
            calledBallId = ballId;
        }

        int id = calledBallId;

#if EIJIS_DEBUG_BALLCHOICE
        _LogInfo($"  id = {id}, ballsPocketedLocal = {ballsPocketedLocal:X4}, (0x1 << id) = {(0x1 << id):X4}");
#endif
        if (0 < id && 0 != (ballsPocketedLocal & (0x1 << id)))
        {
#if EIJIS_DEBUG_BALLCHOICE
            _LogInfo("  return");
#endif
            return;
        }

        if (id < 0)
        {
            calledBallOff = true;
            return;
        }

        if (!calledBallOff && !desktop)
        {
            return;
        }

        uint calledBalls = calledBallsLocal;
        calledBalls |= 0x1u << id;
        if (calledBalls == calledBallsLocal)
        {
            calledBalls ^= 0x1u << id;
        }
        
#if EIJIS_CALLSHOT
#if false // callShotOprationOverwriteMode
        if (!callShotOprationOverwriteModeLocal){
            if (calledBallsLocal != 0 && calledBalls != 0 && !desktop)
            {
#if EIJIS_DEBUG_SEMIAUTO_CALL
                // _LogInfo("TKCH   other chiced exists (return)");
#endif
                return;
            }
        }
#endif
#else 
        if (calledBallsLocal != 0 && calledBalls != 0 && !desktop)
        {
            return;
        }
#endif

        if (Networking.LocalPlayer == null || Networking.GetOwner(activeCue.gameObject) != Networking.LocalPlayer)
        {
#if EIJIS_DEBUG_SEMIAUTO_CALL
            _LogInfo($"TKCH   not cue owner {Networking.GetOwner(activeCue.gameObject)}");
#endif
            if (localPlayerId != teamIdLocal)
            {
#if EIJIS_DEBUG_SEMIAUTO_CALL
                _LogInfo($"TKCH   not team leader localPlayerId = {localPlayerId}, teamIdLocal = {teamIdLocal} (return)");
#endif
                return;
            }
        }

        bool enable = (calledBallsLocal < calledBalls);

        networkingManager._OnCalledBallChanged(enable, (uint)id);
        calledBallOff = false;

        //aud_main.PlayOneShot(snd_btn);
    }
#endif
#if EIJIS_CALLSHOT
    
    public void _TriggerPocketHit(int pocketId, bool desktop)
    {
        if (localTeamId != teamIdLocal && !isPracticeMode) return; // is there a better way to do this?

        if (!desktop && callShotLockLocal)
        {
            return;
        }
        
        if (!desktop && calledPocketId == pocketId)
        {
            return;
        }

        if (!desktop && Time.time < calledPocketIdDelayTimestamp + callDelay)
        {
            return;
        }
        else
        {
#if EIJIS_DEBUG_CALLSHOT_DELAY
            _LogInfo($"  calledPocketId = {calledPocketId}, calledPocketIdDelayTimestamp = {calledPocketIdDelayTimestamp}, callShotDelay = {callShotDelay}");
#endif
            calledPocketIdDelayTimestamp = Time.time;
            calledPocketId = pocketId;
        }

        int id = calledPocketId;

        if (id < 0)
        {
            calledPocketOff = true;
            return;
        }

        if (!calledPocketOff && !desktop)
        {
            return;
        }

        uint pointPockets = pointPocketsLocal;
        pointPockets |= 0x1u << id;
        if (pointPockets == pointPocketsLocal)
        {
            pointPockets ^= 0x1u << id;
        }

#if false // callShotOprationOverwriteMode
        if (!callShotOprationOverwriteModeLocal){
            if (pointPocketsLocal != 0 && pointPockets != 0 && !desktop)
            {
                return;
            }
        }
#endif

        if (Networking.LocalPlayer == null || Networking.GetOwner(activeCue.gameObject) != Networking.LocalPlayer)
        {
            if (localPlayerId != teamIdLocal)
            {
                return;
            }
        }

        bool enable = (pointPocketsLocal < pointPockets);
        
        networkingManager._OnPocketChanged(enable, (uint)id);
        calledPocketOff = false;
        
        //aud_main.PlayOneShot(snd_btn);
    }
#endif

    public void _TriggerCueActivate()
    {
        if (!isMyTurn() || !activeCue) return;

        if (Vector3.Distance(activeCue._GetCuetip().transform.position, ballsP[0]) < k_BALL_RADIUS)
        {
            _TriggerCueDeactivate();
            return;
        }

        canHitCueBall = true;
        this._TriggerOnPlayerPrepareShoot();

#if !HT_QUEST
        this.transform.Find("intl.balls/guide/guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_locked);
#endif
    }

    public void _TriggerCueDeactivate()
    {
        canHitCueBall = false;

#if !HT_QUEST
        guideline.gameObject.transform.Find("guide_display").GetComponent<MeshRenderer>().material.SetColor("_Colour", k_aimColour_aim);
#endif
    }

    public void _OnPickupCue()
    {
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnPickupCue();
    }

    public void _OnDropCue()
    {
#if EIJIS_ISSUE_FIX
        if (ReferenceEquals(null, Networking.LocalPlayer)) return; // キューを持った状態でVRChatを終了するとエラーになる
#endif
        if (!Networking.LocalPlayer.IsUserInVR()) desktopManager._OnDropCue();
    }

    public void _TriggerOnPlayerPrepareShoot()
    {
        networkingManager._OnPlayerPrepareShoot();
    }

    public void _OnPlayerPrepareShoot()
    {
        cameraManager._OnPlayerPrepareShoot();
    }

    public void _TriggerPlaceBall(int idx)
    {
        if (!canPlayLocal) return; // in case player was forced to drop ball since someone else took the shot

        // practiceManager._Record();

        networkingManager._OnRepositionBalls(ballsP);
    }

    public void _TriggerGameStart()
    {

        if (playerIDsLocal[0] == -1)
        {
            _LogWarn("Cannot start without first player");
            return;
        }
        else
        {
            _LogYes("starting game");
        }
        //0 is 8ball, 1 is 9ball, 2 is jp4b, 3 is kr4b, 4 is Snooker6Red)
#if EIJIS_MANY_BALLS
        Vector3[] randomPositions = new Vector3[MAX_BALLS];
        Array.Copy(initialPositions[gameModeLocal], randomPositions, MAX_BALLS);
#else
        Vector3[] randomPositions = new Vector3[16];
        Array.Copy(initialPositions[gameModeLocal], randomPositions, 16);
#endif
        if (RandomizeBallPositions)
        {
            switch (gameModeLocal)
            {
                case 0:
                    // 8ball
                    for (int i = 2; i < 16; i++)
                    {
                        // 8 and 14 are the far corner balls, don't randomize them so that one is always orange and one is always blue
                        if (i == 8 || i == 14) continue;
                        Vector3 temp = randomPositions[i];
                        int rand = UnityEngine.Random.Range(2, 16);
                        while (rand == 8 || rand == 14)
                            rand = UnityEngine.Random.Range(2, 16);

                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    // random swap of the corner colors
                    if (UnityEngine.Random.Range(0, 2) == 1)
                    {
                        Vector3 temp = randomPositions[8];
                        randomPositions[8] = randomPositions[14];
                        randomPositions[14] = temp;
                    }
                    break;
                case 1:
                    // 9ball
                    for (int i = 1; i < 9; i++)
                    {
                        // don't move the 1 or 9 balls
                        if (i == 2 || i == 9) continue;
                        Vector3 temp = randomPositions[i];
                        int rand = UnityEngine.Random.Range(1, 9);
                        while (rand == 2 || rand == 9)
                            rand = UnityEngine.Random.Range(1, 9);

                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    break;
#if EIJIS_10BALL
                case GAMEMODE_10BALL:
                    // 10ball
                    for (int i = 1; i < 10; i++)
                    {
                        // don't move the 1,2,3 or 10 balls
                        if (i == 2 || i == 3 || i == 4 || i == 10) continue;
                        Vector3 temp = randomPositions[i];
                        int rand = UnityEngine.Random.Range(1, 10);
                        while (rand == 2 || rand == 3 || rand == 4 || rand == 10)
                            rand = UnityEngine.Random.Range(1, 10);

                        randomPositions[i] = randomPositions[rand];
                        randomPositions[rand] = temp;
                    }
                    break;
#endif
            }
        }

        Debug.Log("_TriggerGameStart");


        networkingManager._OnGameStart(initialBallsPocketed[gameModeLocal], randomPositions);
    }

    //LocalJoinTeam
    public void _TriggerJoinTeam(int teamId)
    {
        if (networkingManager.gameStateSynced == 0 || networkingManager.gameStateSynced == 3) return;

        _LogInfo("joining team " + teamId);
        //Udon chips
#if UDON_CHIPS
        udonChips = GameObject.Find("UdonChips").GetComponent<UdonChips>();
        VRCPlayerApi localplayer = Networking.LocalPlayer;
        int curSlottmp = _GetPlayerSlot(localplayer, playerIDsLocal);
        if (!(curSlottmp != -1))
        {
            if (udonChips.money >= Enter_cost)
            {
                udonChips.money -= Enter_cost;
            }
            else
            {

                _LogWarn("u need money to play");
                return;
            }
        }
#endif
        int newslot = networkingManager._OnJoinTeam(teamId);
        if (newslot != -1)
        {
            //for responsive menu prediction. These values will be overwritten in deserialization
            isPlayer = true;
            VRCPlayerApi lp = Networking.LocalPlayer;
            int curSlot = _GetPlayerSlot(lp, playerIDsLocal);
            if (curSlot != -1)
            {
                playerIDsLocal[curSlot] = -1;
                if (curSlot % 2 == 0) { numPlayersCurrentOrange--; }
                else { numPlayersCurrentBlue--; }
            }
            int[] playerIDsLocal_new = new int[4];
            Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
            playerIDsLocal_new[newslot] = lp.playerId;

            Debug.Log("_TriggerJoinTeam");

            onRemotePlayersChanged(playerIDsLocal_new);
        }
        else
        {
            _LogWarn("failed to join team " + teamId + ", did someone else beat you to it?");
        }
    }

    //LocalLeaveLobby
    public void _TriggerLeaveLobby()
    {
#if UDON_CHIPS
        udonChips = GameObject.Find("UdonChips").GetComponent<UdonChips>();
        udonChips.money += Enter_cost;
#endif
        if (localPlayerId == -1) return;
        _LogInfo("leaving lobby");

        networkingManager._OnLeaveLobby(localPlayerId);

        //for responsive menu prediction, will be overwritten in deserialization
        isPlayer = false;
        int[] playerIDsLocal_new = new int[4];
        Array.Copy(playerIDsLocal, playerIDsLocal_new, 4);
        if (localPlayerId != -1) // true if lobby was closed
        {
            playerIDsLocal_new[localPlayerId] = -1;
        }

        Debug.Log("_TriggerLeaveLobby");
        onRemotePlayersChanged(playerIDsLocal_new);
    }
    private float lastActionTime;
    private float lastResetTime;
    public void _TriggerGameReset()
    {
        int self = Networking.LocalPlayer.playerId;

        int[] allowedPlayers = playerIDsLocal;

        bool allPlayersOffline = true;
        bool isAllowedPlayer = false;
        foreach (int allowedPlayer in allowedPlayers)
        {
            if (allPlayersOffline && Utilities.IsValid(VRCPlayerApi.GetPlayerById(allowedPlayer))) allPlayersOffline = false;

            if (allowedPlayer == self) isAllowedPlayer = true;
        }

        float nearestPlayer = float.MaxValue;
        for (int i = 0; i < allowedPlayers.Length; i++)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(allowedPlayers[i]);
            if (!Utilities.IsValid(player)) continue;
            float playerDist = Vector3.Distance(transform.position, player.GetPosition());
            if (playerDist < nearestPlayer)
                nearestPlayer = playerDist;
        }
        bool allPlayersAway = nearestPlayer < 20f ? false : true;

        if (Time.time - lastResetTime > 0.3f)
        {
            //infReset.text = "Double Click To Reset"; ClearResetInfo();
            infReset.text = _translations.Get("Double Click To Reset"); ClearResetInfo();
        }
        else if (allPlayersOffline || isAllowedPlayer || _IsModerator(Networking.LocalPlayer) || (Time.time - lastActionTime > 300) || allPlayersAway)
        {
            _LogInfo("force resetting game");
            //infReset.text = "Game Reset!"; ClearResetInfo();

            Debug.Log("_TriggerGameReset");

            infReset.text = _translations.Get("Game Reset!"); ClearResetInfo();
            networkingManager._OnGameReset();
        }
        else
        {
            string playerStr = "";
            bool has = false;
            foreach (int allowedPlayer in allowedPlayers)
            {
                if (allowedPlayer == -1) continue;
                if (has) playerStr += "\n";
                has = true;

                playerStr += graphicsManager._FormatName(VRCPlayerApi.GetPlayerById(allowedPlayer));
            }

            //infReset.text = "<size=60%>Only these players may reset:\n" + playerStr; ClearResetInfo();
            infReset.text = _translations.Get("<size=60%>Only these players may reset:\n") + playerStr; ClearResetInfo();
        }
        lastResetTime = Time.time;


    }

    int resetInfoCount = 0;
    private void ClearResetInfo()
    {
        resetInfoCount++;
        SendCustomEventDelayedSeconds(nameof(_ClearResetInfo), 3f);
    }

    public void _ClearResetInfo()
    {
        resetInfoCount--;
        if (resetInfoCount != 0) return;
        infReset.text = string.Empty;
    }
    #endregion

    #region NetworkingClient
    // the order is important, unfortunately
    public void _OnRemoteDeserialization()
    {
        _LogInfo("processing latest remote state ("/*packet="  + networkingManager.packetIdSynced + " ,*/+ "state=" + networkingManager.stateIdSynced + ")");
#if EIJIS_TABLE_LABEL
        Debug.Log("[BilliardsModule" + logLabel + "] latest game state is " + networkingManager._EncodeGameState());
#endif

        lastActionTime = Time.time;
        waitingForUpdate = false;

        // propagate game settings first
        onRemoteGlobalSettingsUpdated(
            (byte)networkingManager.physicsSynced, (byte)networkingManager.tableModelSynced
        );
        onRemoteGameSettingsUpdated(
            networkingManager.gameModeSynced,
            networkingManager.timerSynced,
            networkingManager.teamsSynced,
            networkingManager.noGuidelineSynced,
#if EIJIS_GUIDELINE2TOGGLE
            networkingManager.noGuideline2Synced,
#endif
            networkingManager.noLockingSynced
#if EIJIS_10BALL
            , networkingManager.wpa10BallRuleSynced
#endif
#if EIJIS_CALLSHOT
            , networkingManager.requireCallShotSynced
#if EIJIS_SEMIAUTOCALL
            , networkingManager.semiAutoCallSynced
#endif
#endif
        );

        // propagate valid players second
        onRemotePlayersChanged(networkingManager.playerIDsSynced);
#if EIJIS_CUEBALLSWAP || EIJIS_PUSHOUT
        bool stateIdChanged = (networkingManager.stateIdSynced != stateIdLocal);
#endif
#if EIJIS_PUSHOUT
        onRemotePushOutStateChanged(networkingManager.pushOutStateSynced, stateIdChanged);
#endif
        // apply state transitions if needed
        onRemoteGameStateChanged(networkingManager.gameStateSynced);

        // now update game state
        onRemoteBallPositionsChanged(networkingManager.ballsPSynced);
        onRemoteTeamIdChanged(networkingManager.teamIdSynced);
        onRemoteFourBallCueBallChanged(networkingManager.fourBallCueBallSynced);
        onRemoteColorTurnChanged(networkingManager.colorTurnSynced);
#if EIJIS_CALLSHOT
        onRemoteBallsPocketedChanged(networkingManager.ballsPocketedSynced, networkingManager.targetPocketedSynced, networkingManager.otherPocketedSynced);
#else
        onRemoteBallsPocketedChanged(networkingManager.ballsPocketedSynced);
#endif
        onRemoteFoulStateChanged(networkingManager.foulStateSynced);
        onRemoteFourBallScoresUpdated(networkingManager.fourBallScoresSynced);
        onRemoteIsTableOpenChanged(networkingManager.isTableOpenSynced, networkingManager.teamColorSynced);
        onRemoteTurnStateChanged(networkingManager.turnStateSynced);
#if EIJIS_CALLSHOT
        onRemotePointPocketsChanged(networkingManager.pointPocketsSynced, networkingManager.callShotLockSynced, stateIdChanged);
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT
        onRemoteCalledBallsChanged(networkingManager.calledBallsSynced, stateIdChanged);
#endif

        // finally, take a snapshot
        practiceManager._Record();

#if EIJIS_CUEBALLSWAP
        stateIdLocal = networkingManager.stateIdSynced;
#endif
        redrawDebugger();
#if EIJIS_SEMIAUTOCALL
        semiAutoCallTick = (requireCallShotLocal && semiAutoCallLocal);
#endif
    }

    private void onRemoteGlobalSettingsUpdated(byte physicsSynced, byte tableModelSynced)
    {
        // if (gameLive) return;

        _LogInfo($"onRemoteGlobalSettingsUpdated physicsMode={physicsSynced} tableModel={tableModelSynced}");

        if (currentPhysicsManager != PhysicsManagers[physicsSynced])
        {
            currentPhysicsManager = PhysicsManagers[physicsSynced];
            currentPhysicsManager.SendCustomEvent("_InitConstants");
            menuManager._RefreshPhysics();
            desktopManager._RefreshPhysics();
        }
        if (tableModelLocal != tableModelSynced)
        {
            setTableModel(tableModelSynced);
            menuManager._RefreshGameMode();
        }
    }

#if EIJIS_GUIDELINE2TOGGLE || EIJIS_CALLSHOT || EIJIS_SEMIAUTOCALL
    private void onRemoteGameSettingsUpdated(uint gameModeSynced, uint timerSynced, bool teamsSynced, bool noGuidelineSynced
#if EIJIS_GUIDELINE2TOGGLE
        , bool noGuideline2Synced
#endif
        , bool noLockingSynced
#if EIJIS_10BALL
        , bool wpa10BallRuleSynced
#endif
#if EIJIS_CALLSHOT
        , bool requireCallShotSynced
#if EIJIS_SEMIAUTOCALL
        , bool semiAutoCallSynced
#endif
#endif
    )
#else
    private void onRemoteGameSettingsUpdated(uint gameModeSynced, uint timerSynced, bool teamsSynced, bool noGuidelineSynced, bool noLockingSynced)
#endif
    {
        if (
            gameModeLocal == gameModeSynced &&
            timerLocal == timerSynced &&
            teamsLocal == teamsSynced &&
            noGuidelineLocal == noGuidelineSynced &&
#if EIJIS_GUIDELINE2TOGGLE
            noGuideline2Local == noGuideline2Synced &&
#endif
            noLockingLocal == noLockingSynced
#if EIJIS_10BALL
            && wpa10BallRuleLocal == wpa10BallRuleSynced
#endif
#if EIJIS_CALLSHOT
            && requireCallShotLocal == requireCallShotSynced
#if EIJIS_SEMIAUTOCALL
            && semiAutoCallLocal == semiAutoCallSynced
#endif
#endif
        )
        {
            return;
        }

#if EIJIS_GUIDELINE2TOGGLE && EIJIS_CALLSHOT && EIJIS_SEMIAUTOCALL && EIJIS_10BALL
        _LogInfo($"onRemoteGameSettingsUpdated gameMode={gameModeSynced} timer={timerSynced} teams={teamsSynced} guideline={!noGuidelineSynced} guideline2={!noGuideline2Synced} locking={!noLockingSynced} wpa10BallRule={wpa10BallRuleSynced} callShot={requireCallShotSynced} semiAutCcall={semiAutoCallSynced}");
#else
        _LogInfo($"onRemoteGameSettingsUpdated gameMode={gameModeSynced} timer={timerSynced} teams={teamsSynced} guideline={!noGuidelineSynced} locking={!noLockingSynced}");
#endif

        if (gameModeLocal != gameModeSynced)
        {
            gameModeLocal = gameModeSynced;

            is8Ball = gameModeLocal == 0u;
            is9Ball = gameModeLocal == 1u;
            isJp4Ball = gameModeLocal == 2u;
            isKr4Ball = gameModeLocal == 3u;
#if EIJIS_SNOOKER15REDS
            isSnooker15Red = gameModeLocal == 4u;
#else
            isSnooker6Red = gameModeLocal == 4u;
#endif
#if EIJIS_PYRAMID
            isPyramid = gameModeLocal == GAMEMODE_PYRAMID;
#endif
#if EIJIS_CAROM
            is3Cusion = gameModeLocal == GAMEMODE_3CUSHION;
            is2Cusion = gameModeLocal == GAMEMODE_2CUSHION;
            is1Cusion = gameModeLocal == GAMEMODE_1CUSHION;
            is0Cusion = gameModeLocal == GAMEMODE_0CUSHION;
            cushionHitGoal = is0Cusion ? 0 : (is1Cusion ? 1 : (is2Cusion ? 2 : 3));
            is4Ball = isJp4Ball || isKr4Ball || is3Cusion || is2Cusion || is1Cusion || is0Cusion;
#else
            is4Ball = isJp4Ball || isKr4Ball;
#endif
#if EIJIS_SNOOKER15REDS
            isSnooker = isSnooker6Red || isSnooker15Red;
#endif
#if EIJIS_10BALL
            is10Ball = gameModeLocal == GAMEMODE_10BALL;
            pocketMask = is10Ball ? pocket_mask_10ball : (is9Ball ? pocket_mask_9ball : pocket_mask_8ball);
            ballsLengthByPocketGame = (is9Ball
                ? break_order_9ball.Length
                : (is10Ball ? break_order_10ball.Length : break_order_8ball.Length));
#endif

            menuManager._RefreshGameMode();
        }

        if (timerLocal != timerSynced)
        {
            timerLocal = timerSynced;

            menuManager._RefreshTimer();
        }

        bool refreshToggles = false;
        if (teamsLocal != teamsSynced)
        {
            teamsLocal = teamsSynced;
            refreshToggles = true;
            isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
            isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
            menuManager._RefreshMenu();
        }

        if (noGuidelineLocal != noGuidelineSynced)
        {
            noGuidelineLocal = noGuidelineSynced;
            refreshToggles = true;
        }

#if EIJIS_GUIDELINE2TOGGLE
        if (noGuideline2Local != noGuideline2Synced)
        {
            noGuideline2Local = noGuideline2Synced;
            refreshToggles = true;
        }
        
#endif
        if (noLockingLocal != noLockingSynced)
        {
            noLockingLocal = noLockingSynced;
            refreshToggles = true;
        }

#if EIJIS_10BALL
        if (wpa10BallRuleLocal != wpa10BallRuleSynced)
        {
            wpa10BallRuleLocal = wpa10BallRuleSynced;
            refreshToggles = true;
        }
        
#endif
#if EIJIS_CALLSHOT
        if (requireCallShotLocal != requireCallShotSynced)
        {
            requireCallShotLocal = requireCallShotSynced;
            refreshToggles = true;
        }
        
#if EIJIS_SEMIAUTOCALL
        if (semiAutoCallLocal != semiAutoCallSynced)
        {
            semiAutoCallLocal = semiAutoCallSynced;
            refreshToggles = true;
        }
        
#endif
#endif
        if (refreshToggles)
        {
            menuManager._RefreshToggleSettings();
            menuManager._RefreshPlayerList();
        }
    }

    private void onRemotePlayersChanged(int[] playerIDsSynced)
    {
        // int myOldSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);

        if (intArrayEquals(playerIDsLocal, playerIDsSynced)) return;

        Array.Copy(playerIDsLocal, playerIDsCached, playerIDsLocal.Length);
        Array.Copy(playerIDsSynced, playerIDsLocal, playerIDsLocal.Length);

        if (networkingManager.gameStateSynced != 3) // don't set practice mode to true after the players are kicked when the game ends
            isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        string[] playerDetails = new string[4];
        for (int i = 0; i < 4; i++)
        {
            VRCPlayerApi plyr = VRCPlayerApi.GetPlayerById(playerIDsSynced[i]);
            playerDetails[i] = (playerIDsSynced[i] == -1 || plyr == null) ? "none" : plyr.displayName;
        }
        _LogInfo($"onRemotePlayersChanged newPlayers={string.Join(",", playerDetails)}");

        localPlayerId = Array.IndexOf(playerIDsLocal, Networking.LocalPlayer.playerId);
        if (localPlayerId != -1) localTeamId = (uint)(localPlayerId & 0x1u);
        else localTeamId = uint.MaxValue;

        cueControllers[0]._SetAuthorizedOwners(new int[] { playerIDsLocal[0], playerIDsLocal[2] });
        cueControllers[1]._SetAuthorizedOwners(new int[] { playerIDsLocal[1], playerIDsLocal[3] });
        cueControllers[1]._RefreshRenderer();// 2nd cue is invisible in practice mode

        if (playerIDsLocal[0] == -1 && playerIDsLocal[2] == -1)
        {
            cueControllers[0]._ResetCuePosition();
        }

        if (playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1)
        {
            cueControllers[1]._ResetCuePosition();
        }

        applyCueAccess();

        if (networkingManager.gameStateSynced != 3) { graphicsManager._SetScorecardPlayers(playerIDsLocal); } // don't remove player names when match is won

        int myNewSlot = _GetPlayerSlot(Networking.LocalPlayer, playerIDsLocal);
        isPlayer = myNewSlot != -1;

        isOrangeTeamFull = teamsLocal ? playerIDsLocal[0] != -1 && playerIDsLocal[2] != -1 : playerIDsLocal[0] != -1;
        isBlueTeamFull = teamsLocal ? playerIDsLocal[1] != -1 && playerIDsLocal[3] != -1 : playerIDsLocal[1] != -1;
        menuManager._RefreshLobby();

        // return gameLive && myOldSlot != myNewSlot;//if our slot changed, we left, or we joined, return true
    }

    private void onRemoteGameStateChanged(byte gameStateSynced)
    {
        if (gameStateLocal == gameStateSynced) return;

        gameStateLocal = gameStateSynced;
        _LogInfo($"onRemoteGameStateChanged newState={gameStateSynced}");

        if (gameStateLocal == 1)
        {
            onRemoteLobbyOpened();
        }
        else if (gameStateLocal == 0)
        {
            onRemoteLobbyClosed();
        }
        else if (gameStateLocal == 2)
        {
            onRemoteGameStarted();
        }
        else if (gameStateLocal == 3)
        {
            onRemoteGameEnded(networkingManager.winningTeamSynced);
        }
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
    }

    private void onRemoteLobbyOpened()
    {
        _LogInfo($"onRemoteLobbyOpened");

        lobbyOpen = true;
        graphicsManager._OnLobbyOpened();
        menuManager._RefreshLobby();
        cueControllers[0].resetScale();
        cueControllers[1].resetScale();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyOpened");
    }

    private void onRemoteLobbyClosed()
    {
        _LogInfo($"onRemoteLobbyClosed");

        lobbyOpen = false;
        localPlayerId = -1;
        graphicsManager._OnLobbyClosed();
        menuManager._RefreshLobby();

        if (networkingManager.winningTeamSynced == 2)
        {
            _LogWarn("game reset");
            graphicsManager._OnGameReset();
        }
        gameLive = false;

        disablePlayComponents();
        resetCachedData();

        if (callbacks != null) callbacks.SendCustomEvent("_OnLobbyClosed");
    }

    private void onRemoteGameStarted()
    {

        _LogInfo($"onRemoteGameStarted");

        HeightBreak = 0;
        ShotCounts = 0;

        lobbyOpen = false;
        gameLive = true;

        Array.Clear(perfCounters, 0, PERF_MAX);
        Array.Clear(perfStart, 0, PERF_MAX);
        Array.Clear(perfTimings, 0, PERF_MAX);

        isPracticeMode = playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1;

        menuManager._RefreshLobby();
        graphicsManager._OnGameStarted();
        desktopManager._OnGameStarted();
        applyCueAccess();
        practiceManager._Clear();
        repositionManager._OnGameStarted();
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        Array.Clear(fbScoresLocal, 0, 2);
        auto_pocketblockers.SetActive(is4Ball);
#if EIJIS_10BALL
#if EIJIS_PYRAMID
        marker9ball.SetActive(is9Ball || is10Ball || isPyramid);
#else
        marker9ball.SetActive(is9Ball || is10Ball);
#endif
#else
#if EIJIS_PYRAMID
        marker9ball.SetActive(is9Ball || isPyramid);
#else
        marker9ball.SetActive(is9Ball);
#endif
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT
        calledBallsLocal = 0;
        calledBallId = -2;
#if EIJIS_CALLSHOT
        pointPocketsLocal = 0;
        calledPocketId = -2;
#if EIJIS_SEMIAUTOCALL
        semiAutoCalledPocket = false;
        semiAutoCalledTimeBall = 0;
#endif
#endif
#else
        marker9ball.SetActive(is9Ball);
#endif
#if EIJIS_PUSHOUT
        pushOutStateLocal = PUSHOUT_BEFORE_BREAK;
        graphicsManager._UpdatePushOut(pushOutStateLocal);
#endif
#if EIJIS_CALLSHOT
        targetPocketedLocal = 0x0u;
        otherPocketedLocal = 0x0u;
#endif

        // Reflect game state
        graphicsManager._UpdateScorecard();
        isReposition = false;
        markerObj.SetActive(false);

        // Effects
        graphicsManager._PlayIntroAnimation();
        aud_main.PlayOneShot(snd_Intro, 1.0f);

        timerRunning = false;

#if EIJIS_ISSUE_FIX
        activeCue = cueControllers[(isPracticeMode ? 0 : teamIdLocal)];
#else
        activeCue = cueControllers[0];
#endif

    }

    private void onRemoteBallPositionsChanged(Vector3[] ballsPSynced)
    {
        if (vector3ArrayEquals(ballsP, ballsPSynced)) return;

        _LogInfo($"onRemoteBallPositionsChanged");

        Array.Copy(ballsPSynced, ballsP, ballsP.Length);

        _Update9BallMarker();
    }

    private void onRemoteGameEnded(uint winningTeamSynced)
    {

        _LogInfo($"onRemoteGameEnded winningTeam={winningTeamSynced}");

        isLocalSimulationRunning = false;

        winningTeamLocal = winningTeamSynced;

        if (winningTeamLocal < 2)
        {
            string p1str = "No one";
            string p2str = "No one";
            VRCPlayerApi winner1 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal]);
            if (Utilities.IsValid(winner1))
                p1str = winner1.displayName;
            VRCPlayerApi winner2 = VRCPlayerApi.GetPlayerById(playerIDsCached[winningTeamLocal + 2]);
            if (Utilities.IsValid(winner2))
                p2str = winner2.displayName;
            // All players are kicked from the match when it's won, so use the previous turn's player names to show the winners (playerIDsCached)
            _LogWarn("game over, team " + winningTeamLocal + " won (" + p1str + " and " + p2str + ")");
            graphicsManager._SetWinners(/* isPracticeMode ? 0u :  */winningTeamLocal, playerIDsCached);
#if UDON_CHIPS
            VRCPlayerApi LocalPlayer = Networking.LocalPlayer;
            udonChips = GameObject.Find("UdonChips").GetComponent<UdonChips>();
            if (LocalPlayer == winner1 ||  LocalPlayer == winner2)
            {
                udonChips.money += winner_gain;
            }
            else
            {
                udonChips.money -= loser_lose;
            }
#endif
            //_LogYes("shotcounts"+ShotCounts);
            if (personalData != null && !isPracticeMode && ShotCounts != 0)
            {
                VRCPlayerApi localPlayer = Networking.LocalPlayer;
                if (localPlayer == winner1 || localPlayer == winner2 )
                {
                    if (isSnooker) personalData.gameCountSnooker++;
                    else personalData.gameCount++;
                    personalData.winCount++;
                }
                else
                {
                    int losingTeam = (winningTeamLocal == 1) ? 0 : 1;
                    VRCPlayerApi loser1 = VRCPlayerApi.GetPlayerById(playerIDsCached[losingTeam]);
                    VRCPlayerApi loser2 = VRCPlayerApi.GetPlayerById(playerIDsCached[losingTeam + 2]);
                    if(localPlayer == loser1 || localPlayer== loser2 )
                    {
                        if (isSnooker) personalData.gameCountSnooker++;
                        else personalData.gameCount++;
                        personalData.loseCount++;
                    }
                }
                personalData.SaveData();
            }
        }

        //UP 24/6/15  重构by cheese  24/9/26 难以想象居然撑了三个月
        if (ScoreManager != null && !isPracticeMode)
        {
            if (!BreakFinish)  //Breakfinish是由复用的参数计算的
                ScoreManager.AddScore(playerIDsCached[0], playerIDsCached[1], playerIDsCached[winningTeamLocal], isSnooker15Red  && (string)tableModels[tableModelLocal].GetProgramVariable("TABLENAME") ==  "Snooker 12ft" );
        }
        //这段代码必须在resetCachedData前面,不然gamemode被重置了,不过用snooker简化就没事,放这里以防万一

        gameLive = false;
        isPracticeMode = false;

        Array.Copy(networkingManager.fourBallScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateTeamColor(winningTeamSynced);
        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();
#if EIJIS_CALLSHOT
        graphicsManager._UpdatePointPocketMarker(0, callShotLockLocal);
#endif

        disablePlayComponents();

        localPlayerId = -1;
        localTeamId = uint.MaxValue;
        applyCueAccess();

        lobbyOpen = false;

        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();

        infReset.text = string.Empty;

        resetCachedData();

        menuManager._RefreshLobby();

    }

#if EIJIS_CALLSHOT
    private void onRemoteBallsPocketedChanged(uint ballsPocketedSynced, uint targetPocketedSynced, uint otherPocketedSynced)
#else
    private void onRemoteBallsPocketedChanged(uint ballsPocketedSynced)
#endif
    {
        if (!gameLive) return;

        // todo: actually use a separate variable to track local modifications to balls pocketed
        if (ballsPocketedLocal != ballsPocketedSynced) _LogInfo($"onRemoteBallsPocketedChanged ballsPocketed={ballsPocketedSynced:X}");

        ballsPocketedLocal = ballsPocketedSynced;
#if EIJIS_CALLSHOT
        targetPocketedLocal = targetPocketedSynced;
        otherPocketedLocal = otherPocketedSynced;
#endif

        graphicsManager._UpdateScorecard();
        graphicsManager._RackBalls();

        refreshBallPickups();
    }

    private void onRemoteFourBallScoresUpdated(byte[] fbScoresSynced)
    {
        if (!gameLive) return;

        if (fbScoresLocal[0] == fbScoresSynced[0] && fbScoresLocal[1] == fbScoresSynced[1])
        {
            _LogInfo($"onRemoteFourBallScoresUpdated team1={fbScoresSynced[0]} team2={fbScoresSynced[1]}");
            //don't escape, as this will always be true for the sender, and they may need to run the rest.
        }
#if EIJIS_SNOOKER15REDS
        if (!isSnooker && !is4Ball && !isPyramid) { return; }   //cheese fix
#else
        if (!isSnooker6Red && !is4Ball) { return; }
#endif

        Array.Copy(fbScoresSynced, fbScoresLocal, 2);
        graphicsManager._UpdateScorecard();
    }

    private void onRemoteTeamIdChanged(uint teamIdSynced)
    {
        if (!gameLive) return;

        if (teamIdLocal != teamIdSynced)
        {
            teamIdLocal = teamIdSynced;
            aud_main.PlayOneShot(snd_NewTurn, 1.0f);
            _LogInfo($"onRemoteTeamIdChanged newTeam={teamIdSynced}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);

        // always use first cue if practice mode
        activeCue = cueControllers[isPracticeMode ? 0 : (int)teamIdLocal];
    }

    private void onRemoteFourBallCueBallChanged(uint fourBallCueBallSynced)
    {
        if (!gameLive) return;

        if (fourBallCueBallLocal != fourBallCueBallSynced)
        {
            _LogInfo($"onRemoteFourBallCueBallChanged cueBall={fourBallCueBallSynced}");
        }

#if EIJIS_SNOOKER15REDS
        if (isSnooker)//reusing this variable for the number of fouls/repeated shots in a row in snooker
#else
        if (isSnooker6Red)//reusing this variable for the number of fouls/repeated shots in a row in snooker
#endif
        {
            fourBallCueBallLocal = fourBallCueBallSynced;
        }
        if (!is4Ball) return;

        fourBallCueBallLocal = fourBallCueBallSynced;

        graphicsManager._UpdateFourBallCueBallTextures(fourBallCueBallLocal);
    }

    private void onRemoteIsTableOpenChanged(bool isTableOpenSynced, uint teamColorSynced)
    {
        if (!gameLive) return;

        if ((teamColorLocal != teamColorSynced || isTableOpenLocal != isTableOpenSynced))
        {
            _LogInfo($"onRemoteIsTableOpenChanged isTableOpen={isTableOpenSynced} teamColor={teamColorSynced}");
        }
        isTableOpenLocal = isTableOpenSynced;
        teamColorLocal = teamColorSynced;

        if (!isTableOpenLocal)
        {
            string color = (teamIdLocal ^ teamColorLocal) == 0 ? "blues" : "oranges";
            _LogInfo($"table closed, team {teamIdLocal} is {color}");
        }

        graphicsManager._UpdateTeamColor(teamIdLocal);
        graphicsManager._UpdateScorecard();
    }
    private void onRemoteColorTurnChanged(bool ColorTurnSynced)
    {
        if (!gameLive) return;

        if (colorTurnLocal == ColorTurnSynced) return;

        _LogInfo($"onRemoteColorTurnChanged colorTurn={ColorTurnSynced}");
        colorTurnLocal = ColorTurnSynced;
    }

    private void onRemoteFoulStateChanged(uint foulStateSynced)
    {
        if (!gameLive) return;

        if (foulStateLocal != foulStateSynced)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={foulStateSynced}");
            // should not escape here because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        }

        foulStateLocal = foulStateSynced;
        bool myTurn = isMyTurn();
#if EIJIS_SNOOKER15REDS
        if (isSnooker)//enable SnookerUndo button if foul
#else
        if (isSnooker6Red)//enable SnookerUndo button if foul
#endif
        {
            if (fourBallCueBallLocal > 0 && foulStateLocal > 0 && foulStateLocal != 6 && myTurn && networkingManager.turnStateSynced != 1)
            {
                menuManager._EnableSnookerUndoMenu();
            }
            else
            {
                menuManager._DisableSnookerUndoMenu();
            }
        }

        if (!myTurn || foulStateLocal == 0)
        {
            isReposition = false;
            setFoulPickupEnabled(false);
            return;
        }

        if (foulStateLocal > 0 && foulStateLocal < 4)
        {
            isReposition = true;

            switch (foulStateLocal)
            {
                case 1://kitchen
                    repoMaxX = -(k_TABLE_WIDTH - k_CUSHION_RADIUS) / 2;
                    break;
                case 2://anywhere
#if CHEESE_ISSUE_FIX
                    repoMaxX = k_TABLE_WIDTH - k_BALL_RADIUS;
#else
                    Vector3 k_pR = (Vector3)currentPhysicsManager.GetProgramVariable("k_pR");
                    repoMaxX = k_pR.x;
#endif
                    break;
                case 3://snooker D
                    repoMaxX = K_BAULK_LINE;
                    break;
            }
            setFoulPickupEnabled(true);
        }
    }

    private void onRemoteTurnBegin(int timerStartSynced)
    {
        _LogInfo("onRemoteTurnBegin");

        canPlayLocal = true;
        timerStartLocal = timerStartSynced;

        enablePlayComponents();
        Array.Clear(ballsV, 0, ballsV.Length);
        Array.Clear(ballsW, 0, ballsW.Length);
#if EIJIS_CALLSHOT
        graphicsManager._UpdatePointPocketMarker(pointPocketsLocal, callShotLockLocal);
#endif
    }

    private void onRemoteTurnSimulate(Vector3 cueBallV, Vector3 cueBallW, bool fake = false)
    {
        VRCPlayerApi owner = Networking.GetOwner(networkingManager.gameObject);
        simulationOwnerID = Utilities.IsValid(owner) ? owner.playerId : -1;
        bool isOwner = owner == Networking.LocalPlayer || fake;
        _LogInfo($"onRemoteTurnSimulate cueBallV={cueBallV.ToString("F4")} cueBallW={cueBallW.ToString("F4")} owner={simulationOwnerID}");

        if (!fake)
            balls[0].GetComponent<AudioSource>().PlayOneShot(snd_hitball, 1.0f);

        canPlayLocal = false;
        disablePlayComponents();

        bool TableVisible = !localPlayerDistant;
        if (TableVisible)
        {
            for (int i = 0; i < tableMRs.Length; i++)
            {
                if (tableMRs[i].isVisible)
                {
                    TableVisible = true;
                    break;
                }
            }
        }
        if (!_IsPlayer(Networking.LocalPlayer) && !isOwner && (!TableVisible || localPlayerDistant))
        {
            // don't bother simulating if the table isn't even visible
            _LogWarn("skipping simulation");
            return;
        }

        isLocalSimulationRunning = true;
        firstHit = 0;
        secondHit = 0;
        thirdHit = 0;
#if EIJIS_CAROM
        cushionBeforeSecondBall = 0;
#endif
        fbMadePoint = false;
        fbMadeFoul = false;
        ballBounced = false;
        numBallsHitCushion = 0;
        ballhasHitCushion = new bool[MAX_BALLS];
        ballsPocketedOrig = ballsPocketedLocal;
#if EIJIS_CALLSHOT
        targetPocketedOrig = targetPocketedLocal;
        otherPocketedOrig = otherPocketedLocal;
#endif
        jumpShotFoul = false;
        fallOffFoul = false;
        currentPhysicsManager.SendCustomEvent("_ResetSimulationVariables");
        numBallsPocketedThisTurn = 0;

        if (Networking.LocalPlayer.playerId == simulationOwnerID || fake)
        {
            isLocalSimulationOurs = true;
        }

        for (int i = 0; i < ballsV.Length; i++)
        {
            ballsV[i] = Vector3.zero;
            ballsW[i] = Vector3.zero;
        }
        ballsV[0] = cueBallV;
        ballsW[0] = cueBallW;

        auto_colliderBaseVFX.SetActive(true);
    }

    private void onRemoteTurnStateChanged(byte turnStateSynced)
    {
        if (!gameLive) return;
        // should not escape because it can stay the same turn to turn while whos turn it is changes (especially with Undo/SnookerUndo)
        bool stateChanged = false;
        if (turnStateSynced != turnStateLocal)
        {
            _LogInfo($"onRemoteFoulStateChanged foulState={turnStateSynced}");
            stateChanged = true;
        }
        turnStateLocal = turnStateSynced;

        if (turnStateLocal == 0 || turnStateLocal == 2)
        {
            /* if (turnStateLocal == 2) */
            turnStateLocal = 0; // synthetic state

            onRemoteTurnBegin(networkingManager.timerStartSynced);
            // practiceManager._Record();
            auto_colliderBaseVFX.SetActive(false);
        }
        else if (turnStateLocal == 1)
        {
            // prevent simulation from running twice if a serialization was sent during sim
            if (stateChanged || networkingManager.isUrgentSynced == 2)
                onRemoteTurnSimulate(networkingManager.cueBallVSynced, networkingManager.cueBallWSynced);
            // practiceManager._Record();
        }
        else
        {
            canPlayLocal = false;
            disablePlayComponents();
        }
    }
#if EIJIS_CALLSHOT
    
    private void onRemotePointPocketsChanged(uint pointPocketsSynced, bool callShotLockSynced, bool stateIdChanged)
    {
        if (!gameLive) return;

        if (pointPocketsLocal == pointPocketsSynced && callShotLockLocal == callShotLockSynced && 0 < stateIdLocal ) return;

        _LogInfo($"onRemotePointPocketsChanged pointPockets={pointPocketsSynced:X2}, callShotLock={callShotLockSynced}");
        pointPocketsLocal = pointPocketsSynced;
        callShotLockLocal = callShotLockSynced;
#if EIJIS_CUEBALLSWAP
        if (isPyramid)
        {
            marker9ball.GetComponent<MeshRenderer>().material =
                callShotLockLocal ? calledBallMarkerGray : calledBallMarkerWhite;
        }
#endif
        graphicsManager._UpdatePointPocketMarker(pointPocketsLocal, callShotLockLocal);
        if (!stateIdChanged)
        {
            aud_main.PlayOneShot(snd_btn);
        }
    }
#endif
#if EIJIS_CUEBALLSWAP || EIJIS_CALLSHOT

    private void onRemoteCalledBallsChanged(uint calledBallsSynced, bool stateIdChanged)
    {
        if (!gameLive) return;

        if (calledBallsLocal == calledBallsSynced && 0 < stateIdLocal) return;

        _LogInfo($"onRemoteCalledBallsChanged calledBalls={calledBallsSynced:X4}");
#if EIJIS_CALLSHOT
        if (isPyramid)
        {
            calledBallsLocal = 0; //calledBallsSynced;
            calledBallId = -2;
        }
        else
        {
            calledBallsLocal = calledBallsSynced;
        }
#else
        calledBallsLocal = 0; //calledBallsSynced;
        calledBallId = -2;
#endif
        if (!stateIdChanged)
        {
#if EIJIS_DEBUG_PUSHOUT
            _LogInfo("  onRemoteCalledBallsChanged() !stateIdChanged PlayOneShot");
#endif
            aud_main.PlayOneShot(snd_btn);
        }
    }
#endif
#if EIJIS_PUSHOUT

    private void onRemotePushOutStateChanged(byte pushOutStateSynced, bool stateIdChanged)
    {
        if (!gameLive) return;

        if (pushOutStateLocal == pushOutStateSynced /* && 0 < stateIdLocal */) return;

        _LogInfo($"onRemotePushOutStateChanged pushOutState={pushOutStateSynced}");
        pushOutStateLocal = pushOutStateSynced;
        graphicsManager._UpdatePushOut(pushOutStateLocal);
        if (!stateIdChanged)
        {
#if EIJIS_DEBUG_PUSHOUT
            _LogInfo("  onRemotePushOutStateChanged() !stateIdChanged PlayOneShot");
#endif
            aud_main.PlayOneShot(snd_btn);
        }
    }
#endif
#endregion

    #region PhysicsEngineCallbacks
#if EIJIS_CUSHION_EFFECT
    public void _TriggerBounceCushion(int ball, Vector3 pos)
#else
    public void _TriggerBounceCushion(int ball)
#endif
    {
        if (!ballhasHitCushion[ball] && ball != 0)
        {
            numBallsHitCushion++;
            ballhasHitCushion[ball] = true;
        }
        if (firstHit != 0)
        { ballBounced = true; }
#if EIJIS_CAROM
#if EIJIS_DEBUG_CUSHIONTOUCH
        _LogInfo($"EIJIS_DEBUG BilliardsModule::_TriggerCushionAtPosition(ball = {ball}, pos = {pos})");
#endif
        if ((is3Cusion || is2Cusion || is1Cusion /* || is0Cusion */) && ball == 0 && secondHit == 0)
        {
            if (cushionBeforeSecondBall < cushionHitGoal)
            {
                graphicsManager._SpawnCushionTouch(pos, cushionBeforeSecondBall);
            }
            cushionBeforeSecondBall++;
        }
#endif
    }
    public void _TriggerCollision(int srcId, int dstId)
    {
        if (dstId < srcId)
        {
            int tmp = dstId;
            dstId = srcId;
            srcId = tmp;
        }
        if (srcId != 0) return;

        switch (gameModeLocal)
        {
            case 0:
            case 1:
#if EIJIS_PYRAMID
            case GAMEMODE_PYRAMID:
#endif
#if EIJIS_10BALL
            case GAMEMODE_10BALL:
#endif
                if (firstHit == 0) firstHit = dstId;
                break;
            case 2:
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                if (thirdHit == 0)
                {
                    if (dstId != firstHit && dstId != secondHit)
                    {
                        thirdHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 3:
                if (dstId == 13)
                {
                    handle4BallHit(ballsP[dstId], false);
                    break;
                }
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        handle4BallHit(ballsP[dstId], true);
                    }
                    break;
                }
                break;
            case 4:
                //Snooker
                if (firstHit == 0) firstHit = dstId;
                break;
#if EIJIS_CAROM
            case GAMEMODE_3CUSHION:
            case GAMEMODE_2CUSHION:
            case GAMEMODE_1CUSHION:
            case GAMEMODE_0CUSHION:
                if (firstHit == 0)
                {
                    firstHit = dstId;
                    break;
                }
                if (secondHit == 0)
                {
                    if (dstId != firstHit)
                    {
                        secondHit = dstId;
                        if (cushionHitGoal <= cushionBeforeSecondBall)
                        {
                            handle4BallHit(ballsP[dstId], true);
                        }
                    }
                }
                break;
#endif
        }
    }

    private int numBallsPocketedThisTurn;
#if EIJIS_CALLSHOT
    public void _TriggerPocketBall(int id, int pocketId)
#else
    public void _TriggerPocketBall(int id, bool outOfBounds)
#endif
    {
        uint total = 0U;

        // Get total for X positioning
#if EIJIS_10BALL
#if EIJIS_SNOOKER15REDS
        int count_extent = is9Ball ? 10 : (is10Ball ? 11 : (isSnooker15Red ? 31 : 16));
#else
        int count_extent = is9Ball ? 10 : (is10Ball ? 11 : 16);
#endif
#else
#if EIJIS_SNOOKER15REDS
        int count_extent = is9Ball ? 10 : (isSnooker15Red ? 31 : 16);
#else
        int count_extent = is9Ball ? 10 : 16;
#endif
#endif
        for (int i = 1; i < count_extent; i++)
        {
            total += (ballsPocketedLocal >> i) & 0x1U;
        }

        // place ball on the rack
        if (isChinese8Ball)
        {
            int i = 0, j = 0;
            int count = 0;
            bool breaktime = false;
            for (i = 0; i < 5; i++)
            {
                for (j = 5; j > i; j--)
                {
                    if (count == (int)total)
                    {
                        breaktime = true; break;
                    }
                    count++;

                }
                if (breaktime) break;
            }
            float k_BALL_PL_X = -k_BALL_RADIUS; // break placement X
            float k_BALL_PL_Y = Mathf.Sin(60 * Mathf.Deg2Rad) * k_BALL_DIAMETRE; // break placement Y
            //Vector3 result = new Vector3(k_rack_position.x, k_rack_position.y + col_offset * k_BALL_DIAMETRE, k_rack_position.z + row  * k_BALL_DIAMETRE);
            ballsP[id] = new Vector3
                    (
                        (k_rack_position.x - i * k_BALL_PL_Y),
                        5 - i * 5,
                        ((-i + j * 2) * k_BALL_PL_X) + 0.17f
                        );
            //_LogInfo($"i={i},j={j},total={total},pos={balls[id]}");
        }
        else
        {
            ballsP[id] = k_rack_position + (float)total * k_BALL_DIAMETRE * k_rack_direction;
        }
        ballsPocketedLocal ^= 1U << id;

#if EIJIS_CALLSHOT
        bool callSuccess = ((calledBallsLocal & (0x1u << id)) != 0) && ((pointPocketsLocal & (0x1u << pocketId)) != 0);
#endif
        bool foulPocket = false;
#if EIJIS_SNOOKER15REDS
        if (isSnooker) // recreation of the rules in _TriggerSimulationEnded()
#else
        if (isSnooker6Red) // recreation of the rules in _TriggerSimulationEnded()
#endif
        {
#if EIJIS_SNOOKER15REDS
            uint bmask = SNOOKER_REDS_MASK;
#else
            uint bmask = 0x1E50u;// reds
#endif
            int nextcolor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);
            bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig, false);
            bool freeBall = foulStateLocal == 5;
            if (colorTurnLocal)
            {
                bmask = 0x1AE; // color balls
                bmask &= 1u << firstHit; // only firsthit is legal
            }
            else if (!redOnTable)
            {
                bmask = 1u << break_order_sixredsnooker[nextcolor];
                if (freeBall)
                {
                    bmask |= 1u << firstHit;
                }
            }
            else
            {
                if (freeBall)
                {
                    bmask |= 1u << firstHit;
                }
            }
            if (((0x1U << id) & bmask) > 0)
            {
                if (colorTurnLocal)
                {
                    if (numBallsPocketedThisTurn > 0)//potting 2 colors is always a foul
                    {
                        foulPocket = true;
                    }
                    numBallsPocketedThisTurn++;
                }
            }
            else
            {
                foulPocket = true;
            }
        }
        else if (is8Ball)
        {
            uint bmask = 0x1FCU << ((int)(teamIdLocal ^ teamColorLocal) * 7);
            if (colorTurnLocal)
                bmask |= 2u; // add black to mask in case of golden break (colorturnlocal = break in 8/9ball)
            if (!(((0x1U << id) & ((bmask) | (isTableOpenLocal ? 0xFFFCU : 0x0000U) | ((bmask & ballsPocketedLocal) == bmask ? 0x2U : 0x0U))) > 0))
            {
                foulPocket = true;
            }
#if EIJIS_CALLSHOT
            if ((!requireCallShotLocal || callSuccess) &&
                (((0x1U << id) & ((bmask) | (isTableOpenLocal ? 0xFFFCU : 0x0000U) | ((bmask & ballsPocketedLocal) == bmask ? 0x2U : 0x0U))) > 0))
            {
                targetPocketedLocal |= 1U << id;
            }
            else
            {
                if (0 != id)
                {
                    otherPocketedLocal |= 1U << id;
                }
            }
#endif
        }
#if EIJIS_10BALL
        else if (is9Ball || is10Ball)
#else
        else if (is9Ball)
#endif
        {
            foulPocket = !(findLowestUnpocketedBall(ballsPocketedOrig) == firstHit) || id == 0;
#if EIJIS_CALLSHOT
            if (!requireCallShotLocal || callSuccess)
            {
                targetPocketedLocal |= 1U << id;
                calledBallsLocal = 0;
            }
            else
            {
                if (0 != id)
                {
                    otherPocketedLocal |= 1U << id;
                }
            }
#endif
        }
        foulPocket |= fallOffFoul;
        if (foulPocket)
        {
            graphicsManager._FlashTableError();
        }
        else
        {
            graphicsManager._FlashTableLight();
        }
#if EIJIS_CALLSHOT
        if (pocketId < 0)
#else
        if (outOfBounds)
#endif
        { if (snd_OutOfBounds) aud_main.PlayOneShot(snd_OutOfBounds, 1.0f); }
        else
        { if (snd_Sink) aud_main.PlayOneShot(snd_Sink, 1.0f); }

        // VFX ( make ball move )
        Rigidbody body = balls[id].GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.velocity = transform.TransformDirection(ballsV[id]);
        body.angularVelocity = transform.TransformDirection(ballsW[id].normalized) * -ballsW[id].magnitude;

        ballsV[id] = Vector3.zero;
        ballsW[id] = Vector3.zero;
    }

    public void _TriggerJumpShotFoul() { jumpShotFoul = true; }
    public void _TriggerBallFallOffFoul() { fallOffFoul = true; }

    public void _TriggerSimulationEnded(bool forceScratch, bool forceRun = false)
    {
        if (!isLocalSimulationRunning && !forceRun) return;

        isLocalSimulationRunning = false;
        waitingForUpdate = !isLocalSimulationOurs;

        if (!isLocalSimulationOurs && networkingManager.delayedDeserialization)
            networkingManager.OnDeserialization();

        cameraManager._OnLocalSimEnd();

        auto_colliderBaseVFX.SetActive(false);

#if EIJIS_PUSHOUT
#if EIJIS_DEBUG_AFTERBREAK
        _LogInfo($"  afterBreak {afterBreak}");
#endif
#if EIJIS_DEBUG_PUSHOUT
        _LogInfo($"  pushOutState {PushOutState[pushOutStateLocal]}({pushOutStateLocal})");
#endif
        bool pushOut = false;
        if (pushOutStateLocal == PUSHOUT_BEFORE_BREAK)
        {
#if EIJIS_DEBUG_PUSHOUT
            _LogInfo($"  set DONT pushOutState {PushOutState[pushOutStateLocal]}({pushOutStateLocal})");
#endif
            pushOutStateLocal = PUSHOUT_DONT;
        }
        else if (pushOutStateLocal == PUSHOUT_DOING)
        {
            pushOut = true;
            pushOutStateLocal = PUSHOUT_REACTIONING;
        }
        else if (pushOutStateLocal == PUSHOUT_DONT || pushOutStateLocal == PUSHOUT_REACTIONING /* || pushOutStateLocal == PUSHOUT_ILLEGAL_REACTIONING */)
        {
#if EIJIS_DEBUG_PUSHOUT
            _LogInfo($"  set ENDED pushOutState {PushOutState[pushOutStateLocal]}({pushOutStateLocal})");
#endif
            pushOutStateLocal = PUSHOUT_ENDED;
        }

#endif
        // Make sure we only run this from the client who initiated the move
        if (isLocalSimulationOurs || forceRun)
        {
            isLocalSimulationOurs = false;

            // Common informations
            bool isScratch = (ballsPocketedLocal & 0x1U) == 0x1U || forceScratch;
            bool nextTurnBlocked = false;

            ballsPocketedLocal = ballsPocketedLocal & ~(0x1U);
            if (isScratch) ballsP[0] = Vector3.zero;
            //keep moving ball down the table until it's not touching any other balls
            moveBallInDirUntilNotTouching(0, Vector3.right * k_BALL_RADIUS * .051f);

            // These are the resultant states we can set for each mode
            // then the rest is taken care of
            bool
                isObjectiveSink,
                isOpponentSink,
                winCondition,
                foulCondition,
                deferLossCondition
            ;
            bool snookerDraw = false;

#if EIJIS_CALLSHOT
            bool isOnBreakShot = colorTurnLocal;
            bool isAnyPocketSink = (ballsPocketedLocal & pocketMask) > (ballsPocketedOrig & pocketMask);
#if EIJIS_10BALL
            uint gameBallMask = is8Ball ? 0x2u : (is9Ball ? 0x200u : (is10Ball ? 0x400u : 0));
            int gameBallId = is8Ball ? 1 : (is9Ball ? 9 : (is10Ball ? 10 : -1));
#endif

#endif
#if EIJIS_PUSHOUT
            if (pushOut && isScratch)
            {
                pushOutStateLocal = PUSHOUT_ENDED;
            }

#endif
            if (is8Ball)
            {
                // 8ball rules are based on APA, some rules are not yet implemented.

                uint bmask = 0xFFFCu;
                uint emask = 0x0u;

                // Quash down the mask if table has closed
                if (!isTableOpenLocal)
                {
                    bmask = bmask & (0x1FCu << ((int)(teamIdLocal ^ teamColorLocal) * 7));
                    emask = 0x1FCu << ((int)(teamIdLocal ^ teamColorLocal ^ 0x1U) * 7);
                }

                bool isSetComplete = (ballsPocketedOrig & bmask) == bmask;

                // Append black to mask if set is done or it's the break (Golden break rule)
                if (isSetComplete || colorTurnLocal)
                {
                    bmask |= 0x2U;
                }

                isObjectiveSink = (ballsPocketedLocal & bmask) > (ballsPocketedOrig & bmask);
                isOpponentSink = (ballsPocketedLocal & emask) > (ballsPocketedOrig & emask);

                // Calculate if objective was not hit first
                bool isWrongHit = ((0x1U << firstHit) & bmask) == 0;

                bool is8Sink = (ballsPocketedLocal & 0x2U) == 0x2U;

                winCondition = (isSetComplete || colorTurnLocal) && is8Sink;

#if EIJIS_PUSHOUT
                if (is8Sink && ((isPracticeMode && !winCondition) || pushOut))
#else
                if (is8Sink && isPracticeMode && !winCondition)
#endif
                {
                    is8Sink = false;

                    ballsPocketedLocal = ballsPocketedLocal & ~(0x2U);
#if EIJIS_CALLSHOT
                    targetPocketedLocal = targetPocketedLocal & ~(0x2U);
                    otherPocketedLocal = otherPocketedLocal & ~(0x2U);
#endif
                    ballsP[1] = Vector3.zero;
                    moveBallInDirUntilNotTouching(1, Vector3.right * k_BALL_RADIUS * .051f);
                }

                foulCondition = isScratch || isWrongHit || fallOffFoul || ((!isObjectiveSink && !isOpponentSink) && (!ballBounced || (colorTurnLocal && numBallsHitCushion < 4)));
#if EIJIS_DEBUG_BREAKINGFOUL
                if ((!isObjectiveSink && !isOpponentSink) && colorTurnLocal && numBallsHitCushion < 4)
                {
                    _LogInfo($"  BREAKING FOUL numBallsHitCushion = {numBallsHitCushion}");
                }
#endif

                if (isScratch && colorTurnLocal)
                {
                    nextTurnBlocked = true; // re-using snooker variable for reposition to kitchen
                    ballsP[0].x = -k_TABLE_WIDTH / 2;
                }

                deferLossCondition = is8Sink;

                if (personalData != null && !isPracticeMode)
                {
                    if (colorTurnLocal && is8Sink)                //黄金开球
                    {
                        personalData.goldenBreak++;
                        if (!foulCondition)
                        {
                            personalData.breakClearance--;
                            personalData.clearance--;
                        }
                    }
                        if (isScratch)
                            personalData.scratchCount++;
                    personalData.SaveData();
                }

                if (is8Sink && isChinese8Ball && colorTurnLocal)//中式开球复位
                {
                    is8Sink = false;
                    winCondition = false;           //赢不了一点
                    deferLossCondition = false;     //也别想输
                    ballsPocketedLocal = ballsPocketedLocal & ~(0x2U);
#if EIJIS_CALLSHOT
                    targetPocketedLocal = targetPocketedLocal & ~(0x2U);
                    otherPocketedLocal = otherPocketedLocal & ~(0x2U);
#endif
                    ballsP[1] = initialPositions[1][1]; //初始点
                    moveBallInDirUntilNotTouching(1, Vector3.right * .051f);
                }
                if (is8Sink && colorTurnLocal) BreakFinish = true;
                else BreakFinish = false;

#if EIJIS_CALLSHOT
                // call-shot additional rules
                {
#if EIJIS_DEBUG_CALLSHOT_BALL
                    _LogInfo($"  isObjectiveSink = {isObjectiveSink}, isOpponentSink = {isOpponentSink}");
#endif
                    if (requireCallShotLocal)
                    {
#if EIJIS_DEBUG_CALLSHOT_BALL
                        _LogInfo($"  requireCallShot targetPocketedLocal = {targetPocketedLocal:X4}, targetPocketedOrig = {targetPocketedOrig:x4}");
                        _LogInfo($"           masked targetPocketedLocal = {(targetPocketedLocal & bmask):X4}, targetPocketedOrig = {(targetPocketedOrig & bmask):x4}");
                        _LogInfo($"                  otherPocketedLocal = {otherPocketedLocal:X4}, otherPocketedLocal = {otherPocketedLocal:x4}");
                        _LogInfo($"           masked otherPocketedLocal = {(otherPocketedLocal & pocketMask):X4}, otherPocketedLocal = {(otherPocketedOrig & pocketMask):x4}");
#endif
                        isObjectiveSink = (targetPocketedLocal & bmask) > (targetPocketedOrig & bmask);
                        isOpponentSink = isOpponentSink | (otherPocketedLocal & pocketMask) > (otherPocketedOrig & pocketMask);
#if EIJIS_DEBUG_CALLSHOT_BALL
                        _LogInfo($"  isObjectiveSink = {isObjectiveSink}, isOpponentSink = {isOpponentSink}");
#endif
                    }

                    if (isObjectiveSink && isOpponentSink)
                    {
                        isOpponentSink = false;
                    }

                    if (is8Sink && (pushOut || (isOnBreakShot && !winCondition && deferLossCondition)))
                    {
                        moveBallInDirUntilNotTouching(9, Vector3.right * .051f);
                        deferLossCondition = false;
                    }

                    if (isOnBreakShot && isAnyPocketSink)
                    {
                        isObjectiveSink = true;
                        isOpponentSink = false;
                    }
#if EIJIS_DEBUG_CALLSHOT_BALL
                    _LogInfo($"  isObjectiveSink = {isObjectiveSink}, isOpponentSink = {isOpponentSink}");
#endif
                }

#endif
                if (personalData != null && foulCondition && colorTurnLocal && !isPracticeMode)
                    personalData.breakFoul++;
                // try and close the table if possible
                if (!foulCondition && isTableOpenLocal)
                {
                    uint sink_orange = 0;
                    uint sink_blue = 0;
#if EIJIS_CALLSHOT
                    uint pmask = (requireCallShotLocal ? (targetPocketedLocal & ~targetPocketedOrig) : (ballsPocketedLocal ^ ballsPocketedOrig)) >> 2;
#else
                    uint pmask = (ballsPocketedLocal ^ ballsPocketedOrig) >> 2; // only check balls that were pocketed this turn
#endif

                    for (int i = 0; i < 7; i++)
                    {
                        if ((pmask & 0x1u) == 0x1u)
                            sink_blue++;

                        pmask >>= 1;
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        if ((pmask & 0x1u) == 0x1u)
                            sink_orange++;

                        pmask >>= 1;
                    }

                    bool closeTable = false;
                    if (sink_blue > 0 && sink_orange == 0)
                    {
                        teamColorLocal = teamIdLocal;
                        closeTable = true;
                    }
                    else if (sink_orange > 0 && sink_blue == 0)
                    {
                        teamColorLocal = teamIdLocal ^ 0x1u;
                        closeTable = true;
                    }
                    if (isChinese8Ball && colorTurnLocal) closeTable = false; //中式开球不判球

                    if (personalData != null && !isPracticeMode)
                    {
                        if (colorTurnLocal && (foulCondition || (!isObjectiveSink && !isOpponentSink))) personalData.lossOfChange++;
                    }
                    if (closeTable)
                    {
                        networkingManager._OnTableClosed(teamColorLocal);
                    }
                }
                colorTurnLocal = false; // colorTurnLocal tracks if it's the break
            }
#if EIJIS_10BALL
            else if (is9Ball || is10Ball)
#else
            else if (is9Ball)
#endif
            {
                // Rule #1: Cueball must strike the lowest number ball, first
                bool isWrongHit = !(findLowestUnpocketedBall(ballsPocketedOrig) == firstHit);

                // Rule #2: Pocketing cueball, is a foul
#if EIJIS_CALLSHOT
                isObjectiveSink = (targetPocketedLocal & pocketMask) > (targetPocketedOrig & pocketMask);
#else
                isObjectiveSink = (ballsPocketedLocal & 0x3FEu) > (ballsPocketedOrig & 0x3FEu);
#endif
#if EIJIS_DEBUG_CALLSHOT_BALL
                if (requireCallShotLocal)
                {
                    _LogInfo($"  requireCallShot targetPocketedLocal = {targetPocketedLocal:X4}, targetPocketedOrig = {targetPocketedOrig:x4}");
                    _LogInfo($"           masked targetPocketedLocal = {(targetPocketedLocal & pocketMask):X4}, targetPocketedOrig = {(targetPocketedOrig & pocketMask):x4}");
                    _LogInfo($"                  otherPocketedLocal = {otherPocketedLocal:X4}, otherPocketedLocal = {otherPocketedLocal:x4}");
                    _LogInfo($"           masked otherPocketedLocal = {(otherPocketedLocal & pocketMask):X4}, otherPocketedLocal = {(otherPocketedOrig & pocketMask):x4}");
                }
#endif

#if EIJIS_CALLSHOT
                isOpponentSink = (otherPocketedLocal & pocketMask) > (otherPocketedOrig & pocketMask);
                if (isObjectiveSink || isOnBreakShot)
                {
                    isOpponentSink = false;
                }
#else
                isOpponentSink = false;
#endif
#if EIJIS_DEBUG_CALLSHOT_BALL
                _LogInfo($"  isObjectiveSink = {isObjectiveSink}, isOpponentSink = {isOpponentSink}");
#endif
                deferLossCondition = false;

                foulCondition = isWrongHit || isScratch || fallOffFoul || (!isAnyPocketSink && (!ballBounced || (colorTurnLocal && numBallsHitCushion < 4)));
#if EIJIS_DEBUG_BREAKINGFOUL
                if (!isAnyPocketSink && colorTurnLocal && numBallsHitCushion < 4)
                {
                    _LogInfo($"  BREAKING FOUL numBallsHitCushion = {numBallsHitCushion}");
                }
#endif

                colorTurnLocal = false;// colorTurnLocal tracks if it's the break,

#if EIJIS_10BALL

#if EIJIS_DEBUG_10BALL_WPA_RULE
                if (is10Ball)
                {
                    _LogInfo($"  isOnBreakShot = {isOnBreakShot}, wpa10BallRuleLocal(mustPocket10BallLast) = {wpa10BallRuleLocal}, isObjectiveSink = {isObjectiveSink}");
                    _LogInfo($"  (ballsPocketedLocal & pocketMask) = {(ballsPocketedLocal & pocketMask):x8}, pocketMask = {pocketMask:x8}");
                }
#endif
                // Win condition: Pocket game ball ( and do not foul )
#if EIJIS_CALLSHOT
                winCondition = ((targetPocketedLocal & gameBallMask) == gameBallMask) && !foulCondition;
#else
                winCondition = ((ballsPocketedLocal & gameBallMask) == gameBallMask) && !foulCondition;
#endif

                bool isGameBallSink = (ballsPocketedLocal & gameBallMask) == gameBallMask;

                if (is9Ball)
                {
                    winCondition |= isOnBreakShot && isGameBallSink && !foulCondition;
                }
                else if (is10Ball)
                {
                    winCondition &= !(
                        (isOnBreakShot && isGameBallSink) ||
                        (wpa10BallRuleLocal && (ballsPocketedLocal & pocketMask) != pocketMask)
                        );
                }

#if EIJIS_PUSHOUT
                if (isGameBallSink /* && isPracticeMode */ && (!winCondition || pushOut))
#else
                if (isGameBallSink /* && isPracticeMode */ && !winCondition)
#endif
                {
                    ballsPocketedLocal = ballsPocketedLocal & ~(gameBallMask);
#if EIJIS_CALLSHOT
                    targetPocketedLocal = targetPocketedLocal & ~(gameBallMask);
                    otherPocketedLocal = otherPocketedLocal & ~(gameBallMask);
#endif
                    ballsP[gameBallId] = initialPositions[gameModeLocal][gameBallId];
                    //keep moving ball down the table until it's not touching any other balls
                    moveBallInDirUntilNotTouching(gameBallId, Vector3.right * .051f);
                }
#else
                // Win condition: Pocket 9 ball ( and do not foul )
                winCondition = ((ballsPocketedLocal & 0x200u) == 0x200u) && !foulCondition;

                bool is9Sink = (ballsPocketedLocal & 0x200u) == 0x200u;

                if (is9Sink /* && isPracticeMode */ && !winCondition)
                {
                    is9Sink = false;
                    ballsPocketedLocal = ballsPocketedLocal & ~(0x200u);
                    ballsP[9] = initialPositions[1][9];
                    //keep moving ball down the table until it's not touching any other balls
                    moveBallInDirUntilNotTouching(9, Vector3.right * .051f);
                }
#endif
#if EIJIS_CALLSHOT

                if (isOnBreakShot && isAnyPocketSink)
                {
                    isObjectiveSink = true;
                }
#if EIJIS_DEBUG_CALLSHOT_BALL
                _LogInfo($"  isObjectiveSink = {isObjectiveSink}, isOpponentSink = {isOpponentSink}");
#endif
#endif
            }
            else if (is4Ball)
            {
                isObjectiveSink = fbMadePoint;
                isOpponentSink = fbMadeFoul;
                foulCondition = false;
                deferLossCondition = false;
                if (isScratch)
                    ballsPocketedLocal |= 1u; // make the following function move the cue ball
                fourBallReturnBalls();

                winCondition = fbScoresLocal[teamIdLocal] >= 10;
            }
#if EIJIS_PYRAMID
            else if (isSnooker)
#else
            else /* if (isSnooker6Red) */
#endif
            {
                if (isScratch)
                {
                    ballsP[0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);
                    moveBallInDirUntilNotTouching(0, Vector3.back * k_BALL_RADIUS * .051f);
                }
                isOpponentSink = false;
                deferLossCondition = false;
                foulCondition = false;
                bool freeBall = foulStateLocal == 5;
                if (jumpShotFoul)
                {
                    foulCondition = jumpShotFoul;
                    _LogInfo("6RED: Foul: Jumped over a ball");
                }

                int nextColor = sixRedFindLowestUnpocketedColor(ballsPocketedOrig);
                bool redOnTable = sixRedCheckIfRedOnTable(ballsPocketedOrig, true);
                uint objective = sixRedGetObjective(colorTurnLocal, redOnTable, nextColor, true, true);
                if (isScratch) { _LogInfo("6RED: White ball pocketed"); }
                isObjectiveSink = (ballsPocketedLocal & (objective)) > (ballsPocketedOrig & (objective));
                int ballScore = 0, numBallsPocketed = 0, highestPocketedBallScore = 0;
                int foulFirstHitScore = 0;
                sixRedScoreBallsPocketed(redOnTable, nextColor, ref ballScore, ref numBallsPocketed, ref highestPocketedBallScore);
                if (redOnTable || colorTurnLocal)
                {
                    int pocketedBallTypes = sixRedCheckBallTypesPocketed(ballsPocketedOrig, ballsPocketedLocal);
                    int firsthittype = sixRedCheckFirstHit(firstHit);
                    if (firsthittype == 0)//red or free ball
                    {
                        if (colorTurnLocal)
                        {
                            _LogInfo("6RED: Foul: Color was not first hit on color turn");
                            foulFirstHitScore = 7;
                            foulCondition = true;
                        }
                    }
                    else if (firsthittype == 1)//color
                    {
                        if (!colorTurnLocal)
                        {
                            _LogInfo("6RED: Foul: Red was not hit first on non-color turn");
                            foulFirstHitScore = sixredsnooker_ballpoints[firstHit];
                            foulCondition = true;
                        }
                    }
                    else
                    {
                        _LogInfo("6RED: Foul: No balls hit");
                        foulCondition = true;
                    }
                    if (colorTurnLocal)
                    {
                        if (pocketedBallTypes == 0 || pocketedBallTypes == 2) // red or red and color
                        {
                            _LogInfo("6RED: Foul: Red was pocketed on color turn");
                            foulCondition = true;
                            //pocketing a red on a colorturn is a foul with a penalty of 7
                            highestPocketedBallScore = 7;
                        }
                        if (numBallsPocketed > 1)
                        {
                            _LogInfo("6RED: Foul: Two balls were pocketed on a colorTurn");
                            foulCondition = true;
                        }
                        if (numBallsPocketed == 1 && ((1u << firstHit & ballsPocketedLocal) == 0))
                        {
                            _LogInfo("6RED: Foul: Pocketed color ball was not first hit");
                            foulCondition = true;
                        }
                    }
                    else
                    {
                        if (pocketedBallTypes > 0) // color or red and color
                        {
                            _LogInfo("6RED: Foul: Color was pocketed on non-color turn");
                            foulCondition = true;
                        }
                    }
                }
                else
                {
                    if (firstHit != break_order_sixredsnooker[nextColor] && !freeBall)
                    {
                        _LogInfo("6RED: Foul: Wrong color was first hit");
                        foulFirstHitScore = sixredsnooker_ballpoints[firstHit];
                        foulCondition = true;
                    }
                    //if pocketed a ball that was not the objective, foul
                    if ((ballsPocketedOrig & 0x1AE) < (ballsPocketedLocal & (0x1AE - objective)))//freeball is included in objective
                    {
                        _LogInfo("6RED: Foul: Pocketed incorrect color");
                        foulCondition = true;
                    }
                }
                foulCondition |= isScratch || fallOffFoul;

                //return balls to table before setting allBallsPocketed
                if (redOnTable || colorTurnLocal)
#if EIJIS_SNOOKER15REDS
                { sixRedReturnColoredBalls(SNOOKER_REDS_COUNT); }
#else
                { sixRedReturnColoredBalls(6); }
#endif
                else
                {
                    if (foulCondition)
                    { sixRedReturnColoredBalls(nextColor); }
                    else
                    {
                        // if freeball was pocketed it needs to be returned but nextcolor shouldn't be returned.
                        int returnFrom = nextColor + 1;
#if EIJIS_SNOOKER15REDS
                        if (returnFrom < (break_order_sixredsnooker.Length - 1))
#else
                        if (returnFrom < 11 /* break_order_sixredsnooker.Length - 1 */)
#endif
                        {
                            sixRedReturnColoredBalls(returnFrom);
                        }
                    }
                }
#if EIJIS_SNOOKER15REDS
                bool allBallsPocketed = ((ballsPocketedLocal & SNOOKER_BALLS_MASK) == SNOOKER_BALLS_MASK);
#else
                bool allBallsPocketed = ((ballsPocketedLocal & 0x1FFEu) == 0x1FFEu);
#endif
                //free ball rules
                if (!isScratch && !allBallsPocketed)
                {
                    nextTurnBlocked = SixRedCheckObjBlocked(ballsPocketedLocal, false, false) > 0;
                    if (freeBall && !isObjectiveSink && firstHit != 0 && !foulCondition)
                    {
                        // it's a foul if you use the free ball to block the opponent from hitting object ball
                        // free ball is defined as first ball hit
                        for (int i = 0; i < objVisible_blockingBalls_len; i++)
                        {
                            if (objVisible_blockingBalls[i] == firstHit) // objVisible_blockingBalls is updated inside the above call to SixRedCheckObjBlocked
                            {
                                foulCondition = true;
                                _LogInfo("6RED: Foul: Free ball was used to block");
                                break;
                            }
                        }
                    }
                    if (foulCondition)
                    {
                        if (nextTurnBlocked)
                        {
                            _LogInfo("6RED: Objective blocked with a foul. Next turn is Free Ball.");
                        }
                    }
                }

                if (foulCondition)//points given to other team if foul
                {
                    int foulscore = Mathf.Max(highestPocketedBallScore, foulFirstHitScore);
#if EIJIS_DEBUG_SNOOKER_COLOR_POINT
                    _LogInfo($"  foulscore = {foulscore}, highestPocketedBallScore = {highestPocketedBallScore}, foulFirstHitScore = {foulFirstHitScore}");
#endif
                    if (firstHit == 0)
                    {
#if EIJIS_DEBUG_SNOOKER_COLOR_POINT
                        _LogInfo($"  ballsPocketedLocal = {ballsPocketedLocal:x8}");
#endif
                        int lowestScoringBall = 7;
                        for (int i = 1; i < sixredsnooker_ballpoints.Length; i++)
                        {
                            if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
                            lowestScoringBall = Mathf.Min(lowestScoringBall, sixredsnooker_ballpoints[i]);
#if EIJIS_DEBUG_SNOOKER_COLOR_POINT
                            _LogInfo($"  lowestScoringBall = {lowestScoringBall}");
#endif
                        }

                        foulscore = lowestScoringBall;
#if EIJIS_DEBUG_SNOOKER_COLOR_POINT
                        _LogInfo($"  foulscore = {foulscore}, lowestScoringBall = {lowestScoringBall}");
#endif
                    }
                    fbScoresLocal[1 - teamIdLocal] = (byte)Mathf.Min(fbScoresLocal[1 - teamIdLocal] + Mathf.Max(foulscore, 4), byte.MaxValue);
                    _LogInfo("6RED: Team " + (1 - teamIdLocal) + " awarded for foul " + Mathf.Max(foulscore, 4) + " points");
                }
                else
                {
                    fbScoresLocal[teamIdLocal] = (byte)Mathf.Min(fbScoresLocal[teamIdLocal] + ballScore, byte.MaxValue);
                    if(personalData != null)
                    {
                        HeightBreak += ballScore;
                        if (ballScore > 1) personalData.pocketCountSnooker++;
                    }
                    _LogInfo("6RED: Team " + (teamIdLocal) + " awarded " + ballScore + " points");
                }
                _LogInfo("6RED: TeamScore 0: " + fbScoresLocal[0]);
                _LogInfo("6RED: TeamScore 1: " + fbScoresLocal[1]);
                if (redOnTable)
                {
                    if (foulCondition)
                    { colorTurnLocal = false; }
                    else if (isObjectiveSink)
                    { colorTurnLocal = !colorTurnLocal; }
                    else
                    { colorTurnLocal = false; }
                }
                else
                { colorTurnLocal = false; }

                if (fbScoresLocal[teamIdLocal] == fbScoresLocal[1 - teamIdLocal] && allBallsPocketed)
                {
                    // tie rules, cue and black are re-spotted, and a random player gets to go, cue ball in hand, ->onLocalTurnTie()
                    winCondition = false;
                    deferLossCondition = false;
                    foulCondition = false;
#if EIJIS_SNOOKER15REDS
                    sixRedReturnColoredBalls(break_order_sixredsnooker.Length - 1);
#else
                    sixRedReturnColoredBalls(11);
#endif
                    ballsP[0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);
                    snookerDraw = true;
                }
                else
                {
                    // win = all balls pocketed and have more points than opponent
                    bool myTeamWinning = fbScoresLocal[teamIdLocal] > fbScoresLocal[1 - teamIdLocal];
                    winCondition = myTeamWinning && allBallsPocketed;
                    if (winCondition) { foulCondition = false; }
                    deferLossCondition = allBallsPocketed && !myTeamWinning;
                    /*                 _LogInfo("6RED: " + Convert.ToString((ballsPocketedLocal & 0x1FFEu), 2));
                                    _LogInfo("6RED: " + Convert.ToString(0x1FFEu, 2)); */
                }
            }
#if EIJIS_PYRAMID
            else /* if (isPyramid) */
            {
                isObjectiveSink = isScratch || (ballsPocketedLocal & 0xFFFEu) > (ballsPocketedOrig & 0xFFFEu);
                // foulCondition = (firstHit == 0 && !isObjectiveSink) || fallOffFoul;      eijis
                foulCondition = (firstHit == 0 && isScratch) || (!ballBounced && !isObjectiveSink) || fallOffFoul;// hope it works ,as the var from 9ball but seems not
#if EIJIS_DEBUG_PIRAMIDSCORE
                _LogInfo($"  isObjectiveSink = {isObjectiveSink}, foulCondition = {foulCondition}");
#endif
                if (!foulCondition)
                {
                    int ballScore = (int)SoftwareFallback((ballsPocketedLocal & ~ballsPocketedOrig) & 0xFFFEu);
                    ballScore += isScratch ? 1 : 0;
                    fbScoresLocal[teamIdLocal] += (byte)ballScore;
#if EIJIS_DEBUG_PIRAMIDSCORE
                    _LogInfo($"  fbScoresLocal[teamIdLocal = {teamIdLocal}] = {fbScoresLocal[teamIdLocal]}");
#endif
                }
                winCondition = (8 <= fbScoresLocal[teamIdLocal]);
                deferLossCondition = false;
                isOpponentSink = false;
            }
#endif
#if EIJIS_PUSHOUT

            if (pushOut && !isScratch)
            {
                foulCondition = false;
                winCondition = false;
                deferLossCondition = false;
                isObjectiveSink = false;
            }
#endif
#if EIJIS_CALLSHOT
            calledBallId = -2;
            calledPocketId = -2;
#if EIJIS_SEMIAUTOCALL
            // semiAutoCalledBall = false;
            semiAutoCalledPocket = false;
            semiAutoCalledTimeBall = 0;
#endif
#endif

#if EIJIS_PUSHOUT || EIJIS_CALLSHOT
            networkingManager._OnSimulationEnded(ballsP, ballsPocketedLocal
#if EIJIS_CALLSHOT
                , targetPocketedLocal, otherPocketedLocal
#endif
                , fbScoresLocal, colorTurnLocal
#if EIJIS_PUSHOUT
                , pushOutStateLocal
#endif
            );
#else
            networkingManager._OnSimulationEnded(ballsP, ballsPocketedLocal, fbScoresLocal, colorTurnLocal);
#endif
            if (winCondition)
            {
                if (foulCondition)
                {
                    // Loss
                    onLocalTeamWin(teamIdLocal ^ 0x1U);

                    if(personalData!= null && !isPracticeMode)
                    {
                        shotCountData();
                        personalData.foulEnd++;
                    }
                    if (DG_LAB != null)
                    {
                        DG_LAB.SendCustomEvent("JustShock");
                        _LogYes("输了要电");
                    }
                }
                else
                {
                    // Win
                    onLocalTeamWin(teamIdLocal);
                    if (personalData != null && !isPracticeMode)
                    {
                        shotCountData();
                    }
                }
            }
            else if (deferLossCondition)
            {
                // Loss
                onLocalTeamWin(teamIdLocal ^ 0x1U);

                if (personalData != null && !isPracticeMode)
                {
                    shotCountData();
                }
                if (DG_LAB != null)
                {
                    DG_LAB.SendCustomEvent("JustShock");
                    _LogYes("输了要电");
                }

            }
            else if (foulCondition)
            {
                // Foul
                onLocalTurnFoul(isScratch, nextTurnBlocked);

                if (personalData != null && !isPracticeMode)
                {
                    shotCountData();
                }
                if (DG_LAB != null)
                {
                    DG_LAB.SendCustomEvent("JustShock");
                    _LogYes("犯规了要电");
                }
            }
            else if (snookerDraw)
            {
                // Snooker Draw
                onLocalTurnTie();
            }
            else if (isObjectiveSink && (!isOpponentSink || is8Ball))
            {
                // Continue
                onLocalTurnContinue();
            }
            else
            {
                // Pass
                onLocalTurnPass();
                if (personalData != null && !isPracticeMode)
                {
                    shotCountData();
                }
            }

            //Save personal datas
            if (personalData != null && !isPracticeMode)
            {
                uint bpl = ballsPocketedLocal & ~(0x1U);
                uint opl = ballsPocketedOrig & ~(0x1U);
                uint number = bpl ^ opl;
                int count = 0;
                while (number != 0)
                {
                    number &= (number - 1);  // 去掉最右边的 1
                    count++;
                }
                //Debug.Log("进球:" + count);
                if (isSnooker)
                {
                    personalData.pocketCountSnooker += count;
                    personalData.inningCountSnooker++;
                }
                else
                {
                    personalData.pocketCount += count;
                    personalData.inningCount++;
                }

                if (foulCondition) personalData.foulCount++;

                uint localTeam = 0;
                if (Networking.LocalPlayer.playerId == playerIDsLocal[0])
                    localTeam = 1;
                else if (Networking.LocalPlayer.playerId == playerIDsLocal[1])
                    localTeam = 2;

                if (winCondition && ShotCounts == 1 && localTeam == 1) //炸清
                {
                    personalData.breakClearance++; personalData.clearance++;
                    personalData.syncData();
                } 
                if (winCondition && ShotCounts == 1 && localTeam == 2)
                {
                    personalData.clearance++;
                    personalData.syncData();
                }
                personalData.SaveData();

            }
        }
#if EIJIS_PUSHOUT || EIJIS_CALLSHOT
        else
        {
#if EIJIS_CALLSHOT
            calledBallId = -2;
            calledPocketId = -2;
#if EIJIS_SEMIAUTOCALL
            // semiAutoCalledBall = false;
            semiAutoCalledPocket = false;
            semiAutoCalledTimeBall = 0;
#endif
#endif
        }
#endif
    }
    
    /// <summary>
    /// calculate personal date(tracking shot count and snooker height break
    /// cheese
    /// </summary>
    private void shotCountData()
    {
        ShotCounts++;
        if (isSnooker && (string)tableModels[tableModelLocal].GetProgramVariable("TABLENAME") == "Snooker 12ft")
        {
            personalData.shotCountSnooker++;
            if(HeightBreak > personalData.heightBreak)
            {
                personalData.heightBreak = HeightBreak;
            }
            HeightBreak = 0;
        }
        else
            personalData.shotCount++;

        personalData.SaveData();
    }
    private void sixRedMoveBallUntilNotTouching(int Ball)
    {
        //replace colored ball on its own spot
        ballsP[Ball] = initialPositions[4][Ball];
        //check if it's touching another ball
        int blockingBall = CheckIfBallTouchingBall(Ball);
        if (CheckIfBallTouchingBall(Ball) < 0)
        { return; }
        //if it's touching another ball, place it on other ball spots, starting at black, and moving down
        //the colors until it finds one it can sit without touching another ball
#if EIJIS_SNOOKER15REDS
        for (int i = break_order_sixredsnooker.Length - 1; i >= SNOOKER_REDS_COUNT; i--)
#else
        for (int i = break_order_sixredsnooker.Length - 1; i > 5; i--)
#endif
        {
            ballsP[Ball] = initialPositions[4][break_order_sixredsnooker[i]];
            if (CheckIfBallTouchingBall(Ball) < 0)
            {
                return;
            }
        }
        //if it still can't find a free spot, place at it's original spot and move away from blockage until finding a spot
        ballsP[Ball] = initialPositions[4][Ball];
        Vector3 moveDir = ballsP[Ball] - ballsP[blockingBall];
        moveDir.y = 0;//just to be certain
        if (moveDir.sqrMagnitude == 0)
        { moveDir = -ballsP[Ball]; }
        if (moveDir.sqrMagnitude == 0)
        { moveDir = Vector3.left; }
        moveDir = moveDir.normalized;
        moveBallInDirUntilNotTouching(Ball, moveDir * k_BALL_RADIUS * .051f);
    }
    private void moveBallToNearestFreePointBySpot(int Ball, Vector3 Spot)
    {
        //TODO: Make this function and use it instead of moveBallInDirUntilNotTouching() at the end of sixRedMoveBallUntilNotTouching()
        //TODO: check positions in all directions around spot instead of just moving in one direction 
    }
    private void moveBallInDirUntilNotTouching(int Ball, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall(Ball) > -1)
        {
            ballsP[Ball] += Dir;
        }
    }
    private int CheckIfBallTouchingBall(int Input)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == Input) { continue; }
            if (((ballsPocketedLocal >> i) & 0x1u) == 0x1u) { continue; }
            if ((ballsP[Input] - ballsP[i]).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    private void moveBallInDirUntilNotTouching_Transform(int id, Vector3 Dir)
    {
        //keep moving ball down the table until it's not touching any other balls
        while (CheckIfBallTouchingBall_Transform(id) > -1)
        {
            balls[id].transform.localPosition += Dir;
        }
    }
    private int CheckIfBallTouchingBall_Transform(int id)
    {
        float ballDiameter = k_BALL_RADIUS * 2f;
        float k_BALL_DSQR = ballDiameter * ballDiameter;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == id) { continue; }
            if (((ballsPocketedLocal >> i) & 0x1u) == 0x1u) { continue; }
            if ((balls[id].transform.position - balls[i].transform.position).sqrMagnitude < k_BALL_DSQR)
            {
                return i;
            }
        }
        return -1;
    }
    #endregion

    #region GameLogic
    private void initializeRack()
    {
#if EIJIS_DEBUG_INITIALIZERACK
        _LogInfo("BilliardsModule::initializeRack()");
#endif
        float k_BALL_PL_X = k_BALL_RADIUS; // break placement X
        float k_BALL_PL_Y = Mathf.Sin(60 * Mathf.Deg2Rad) * k_BALL_DIAMETRE; // break placement Y
        float quarterTable = k_TABLE_WIDTH / 2;
#if EIJIS_PYRAMID || EIJIS_CAROM || EIJIS_10BALL
        for (int i = 0; i < 11; i++)
#else
        for (int i = 0; i < 5; i++)
#endif
        {
#if EIJIS_MANY_BALLS
            initialPositions[i] = new Vector3[MAX_BALLS];
            for (int j = 0; j < MAX_BALLS; j++)
#else
            initialPositions[i] = new Vector3[16];
            for (int j = 0; j < 16; j++)
#endif
            {
                initialPositions[i][j] = Vector3.zero;
            }

            // cue ball always starts here (unless four ball, but we override below)
            initialPositions[i][0] = new Vector3(-quarterTable, 0.0f, 0.0f);
        }

        {
            // 8 ball
#if EIJIS_MANY_BALLS
            initialBallsPocketed[0] = 0xFFFF0000u;
#else
            initialBallsPocketed[0] = 0x00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[0][break_order_8ball[k++]] = new Vector3
                    (
                       quarterTable + i * k_BALL_PL_Y /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/,
                       0.0f,
                       (-i + j * 2) * k_BALL_PL_X /*+ UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)*/
                    );
                }
            }
        }

        {
            // 9 ball
#if EIJIS_MANY_BALLS
            initialBallsPocketed[1] = 0xFFFFFC00u;
#else
            initialBallsPocketed[1] = 0xFC00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                int rown = break_rows_9ball[i];
                for (int j = 0; j <= rown; j++)
                {
                    initialPositions[1][break_order_9ball[k++]] = new Vector3
                    (
                       quarterTable - (k_BALL_PL_Y * 2) + i * k_BALL_PL_Y /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */,
                       0.0f,
                       (-rown + j * 2) * k_BALL_PL_X /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */
                    );
                }
            }
        }

        {
            // Snooker
#if EIJIS_MANY_BALLS
            initialBallsPocketed[4] = 0x81FF0000u;
#else
            initialBallsPocketed[4] = 0xE000u;
#endif
            initialPositions[4][0] = new Vector3(K_BAULK_LINE - k_SEMICIRCLERADIUS * .5f, 0f, 0f);//whte, middle of the semicircle
            initialPositions[4][1] = new Vector3//black
                    (
                       K_BLACK_SPOT,
                       0f,
                       0f
                    );
            initialPositions[4][5] = new Vector3//pink
                    (
                       k_SPOT_POSITION_X,
                       0f,
                       0
                    );
            initialPositions[4][2] = new Vector3//yellow
                    (
                       K_BAULK_LINE,
                       0f,
                       -k_SEMICIRCLERADIUS
                    );
            initialPositions[4][7] = new Vector3//green
                    (
                       K_BAULK_LINE,
                       0f,
                       k_SEMICIRCLERADIUS
                    );
            initialPositions[4][8] = new Vector3//brown
                    (
                       K_BAULK_LINE,
                       0f,
                       0f
                    );
            //triangle
            float rackStartSnooker = k_SPOT_POSITION_X + k_BALL_DIAMETRE + k_BALL_DIAMETRE * .03f;
#if EIJIS_SNOOKER15REDS
            for (int i = 0, k = 0; i < 5; i++)
#else
            for (int i = 0, k = 0; i < 3; i++)// change 3 to 5 for 15 balls (rows)
#endif
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[4][break_order_sixredsnooker[k++]] = new Vector3
                    (
                       rackStartSnooker + i * k_BALL_PL_Y,
                       0.0f,
                       (-i + j * 2) * k_BALL_PL_X
                    );
                }
            }
#if EIJIS_DEBUG_INITIALIZERACK
            for (int i = 0; i < MAX_BALLS; i++)
            {
                _LogInfo($"  initialPositions[4][i = {i}] x = {initialPositions[4][i].x}, y = {initialPositions[4][i].y}, z = {initialPositions[4][i].z}");
            }
#endif
        }

        {
            // 4 ball (jp)
#if EIJIS_MANY_BALLS
            initialBallsPocketed[2] = 0xFFFF1FFEu;
#else
            initialBallsPocketed[2] = 0x1FFEu;
#endif
            if (playerIDsLocal[1] == -1 && playerIDsLocal[3] == -1) //lag for break when both player join one side
            {
                initialPositions[2][0] = new Vector3(-quarterTable, 0.0f, k_TABLE_HEIGHT * 0.5f);
                initialPositions[2][13] = new Vector3(-quarterTable, 0.0f, k_TABLE_HEIGHT * -0.5f);
            }
            else
            {
                initialPositions[2][0] = new Vector3(-quarterTable + (quarterTable * -0.5f), 0.0f, 0.0f);
                initialPositions[2][13] = new Vector3(quarterTable + (quarterTable * 0.5f), 0.0f, 0.0f);
            }
            initialPositions[2][14] = new Vector3(quarterTable, 0.0f, 0.0f);
            initialPositions[2][15] = new Vector3(-quarterTable, 0.0f, 0.0f);
        }

        {
            // 4 ball (kr)
            initialBallsPocketed[3] = initialBallsPocketed[2];
            initialPositions[3] = initialPositions[2];
        }
#if EIJIS_PYRAMID

        {
            // Russsian Pyramid
#if EIJIS_MANY_BALLS
            initialBallsPocketed[5] = 0xFFFF0000u;
#else
            initialBallsPocketed[5] = 0x00u;
#endif

            for (int i = 0, k = 0; i < 5; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[5][break_order_8ball[k++]] = new Vector3
                    (
                        k_SPOT_POSITION_X + i * k_BALL_PL_Y + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F),
                        0.0f,
                        (-i + j * 2) * k_BALL_PL_X + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F)
                    );
                }
            }
        }
#endif
#if EIJIS_CAROM

        {
            // 3-Cushion
#if EIJIS_MANY_BALLS
            initialBallsPocketed[6] = 0xFFFF9FFEu;
#else
            initialBallsPocketed[6] = 0x9FFEu;
#endif
            initialPositions[6][0] = new Vector3(-k_SPOT_POSITION_X, 0.0f, -0.15f);
            initialPositions[6][13] = new Vector3(-k_SPOT_POSITION_X, 0.0f, 0.0f);
            initialPositions[6][14] = new Vector3(k_SPOT_POSITION_X, 0.0f, 0.0f);
        }

        for (int i = 7; i <= 9; i++)
        {
            // 0 ～ 2-Cushion
            initialBallsPocketed[i] = initialBallsPocketed[6];
            initialPositions[i] = initialPositions[6];
        }
#endif
#if EIJIS_10BALL
        
        {
            // 10 ball
#if EIJIS_MANY_BALLS
            initialBallsPocketed[GAMEMODE_10BALL] = 0xFFFFF800u;
#else
            initialBallsPocketed[GAMEMODE_10BALL] = 0xF800u;
#endif

            for (int i = 0, k = 0; i < 4; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    initialPositions[GAMEMODE_10BALL][break_order_10ball[k++]] = new Vector3
                    (
                        quarterTable + i * k_BALL_PL_Y /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */,
                        // quarterTable - (k_BALL_PL_Y * 2) + i * k_BALL_PL_Y /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */,
                        0.0f,
                        (-i + j * 2) * k_BALL_PL_X /* + UnityEngine.Random.Range(-k_RANDOMIZE_F, k_RANDOMIZE_F) */
                    );
                }
            }
        }
#endif
    }

    private void resetCachedData()
    {
        for (int i = 0; i < 4; i++)
        {
            playerIDsLocal[i] = -1;
        }
        foulStateLocal = 0;
        gameModeLocal = int.MaxValue;
        turnStateLocal = byte.MaxValue;
    }

    public void setTransform(Transform src, Transform dest, bool doScale = false, float sf = 1f)
    {
        dest.position = src.position;
        dest.rotation = src.rotation;
        if (!doScale) return;
        dest.localScale = src.localScale * sf;
    }

    private void setTableModel(int newTableModel)
    {
        tableModels[tableModelLocal].gameObject.SetActive(false);
        tableModels[newTableModel].gameObject.SetActive(true);

        tableModelLocal = newTableModel;

        isChinese8Ball = (string)tableModels[tableModelLocal].GetProgramVariable("TABLENAME") == "China 9ft";

        ModelData data = tableModels[tableModelLocal];
        k_TABLE_WIDTH = data.tableWidth * .5f;
        k_TABLE_HEIGHT = data.tableHeight * .5f;
        k_CUSHION_RADIUS = data.cushionRadius;
        k_POCKET_WIDTH_CORNER = data.pocketWidthCorner;
        k_POCKET_HEIGHT_CORNER = data.pocketHeightCorner;
        k_POCKET_RADIUS_SIDE = data.pocketRadiusSide;
        k_POCKET_DEPTH_SIDE = data.pocketDepthSide;
        k_INNER_RADIUS_CORNER = data.pocketInnerRadiusCorner;
        k_INNER_RADIUS_SIDE = data.pocketInnerRadiusSide;
        k_FACING_ANGLE_CORNER = data.facingAngleCorner;
        k_FACING_ANGLE_SIDE = data.facingAngleSide;
        K_BAULK_LINE = -(k_TABLE_WIDTH - data.baulkLine);
        K_BLACK_SPOT = k_TABLE_WIDTH - data.blackSpot;
        k_SEMICIRCLERADIUS = data.semiCircleRadius;
        k_BALL_DIAMETRE = data.bs_BallDiameter / 1000f;
        k_BALL_RADIUS = k_BALL_DIAMETRE * .5f;
        k_BALL_MASS = data.bs_BallMass;
        k_RAIL_HEIGHT_UPPER = data.railHeightUpper;
        k_RAIL_HEIGHT_LOWER = data.railHeightLower;
        k_RAIL_DEPTH_WIDTH = data.railDepthWidth;
        k_RAIL_DEPTH_HEIGHT = data.railDepthHeight;
        k_SPOT_POSITION_X = k_TABLE_WIDTH - data.pinkSpot;
        k_POCKET_RESTITUTION = data.bt_PocketRestitutionFactor;
        k_vE = data.cornerPocket;
        k_vF = data.sidePocket;
#if EIJIS_CALLSHOT
        pocketLocations[0] = k_vE;
        pocketLocations[1] = new Vector3(k_vE.x, k_vE.y, -k_vE.z);
        pocketLocations[2] = new Vector3(-k_vE.x, k_vE.y, k_vE.z);
        pocketLocations[3] = new Vector3(-k_vE.x, k_vE.y, -k_vE.z);
        pocketLocations[4] = k_vF;
        pocketLocations[5] = new Vector3(k_vF.x, k_vF.y, -k_vF.z);
#if EIJIS_SEMIAUTOCALL
        findNearestPocket_x = k_vE.x / 2;
        findNearestPocket_n = findNearestPocket_x / k_vE.z;
#endif
#endif

        //advanced physics
        useRailLower = data.useRailHeightLower;
        k_F_SLIDE = data.bt_CoefSlide;
        k_F_ROLL = data.bt_CoefRoll;
        k_F_SPIN = data.bt_CoefSpin;
        k_F_SPIN_RATE = data.bt_CoefSpinRate;
        isDRate = data.bt_ConstDecelRate;
        K_BOUNCE_FACTOR = data.bt_BounceFactor;
        isHanModel = data.bc_UseHan05;
        k_E_C = data.bc_CoefRestitution;
        isDynamicRestitution = data.bc_DynRestitution;
        isCushionFrictionConstant = data.bc_UseConstFriction;
        k_Cushion_MU = data.bc_ConstFriction;
        k_BALL_E = data.bs_CoefRestitution;
        muFactor = data.bs_Friction;

        tableMRs = tableModels[newTableModel].GetComponentsInChildren<MeshRenderer>();

        float newscale = k_BALL_DIAMETRE / ballMeshDiameter;
        Vector3 newBallSize = Vector3.one * newscale;
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localScale = newBallSize;
        }
        float table_base = _GetTableBase().transform.Find(".TABLE_SURFACE").localPosition.y;
        tableSurface.localPosition = new Vector3(0, table_base + k_BALL_RADIUS, 0);
#if EIJIS_CALLSHOT
        for (int i = 0; i < pointPocketMarkers.Length; i++)
        {
            pointPocketMarkers[i].transform.localPosition = new Vector3(pocketLocations[i].x, - (k_BALL_RADIUS * 2), pocketLocations[i].z);
        }
#endif

        SetTableTransforms();

        k_rack_position = tableSurface.InverseTransformPoint(auto_rackPosition.transform.position);
        k_rack_direction = tableSurface.InverseTransformDirection(auto_rackPosition.transform.up);

        currentPhysicsManager.SendCustomEvent("_InitConstants");
        graphicsManager._InitializeTable();

        cueControllers[0]._RefreshTable();
        cueControllers[1]._RefreshTable();

        desktopManager._RefreshTable();

        //set height of guideline
        Transform guideDisplay = guideline.gameObject.transform.Find("guide_display");
        Vector3 newpos = guideDisplay.localPosition; newpos.y = 0;
        newpos += Vector3.down * (k_BALL_RADIUS - 0.003f) / guideline.transform.localScale.y;// divide to convert back to worldspace distance
        guideDisplay.localPosition = newpos;
        guideDisplay.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));
        Transform guideDisplay2 = guideline2.gameObject.transform.Find("guide_display");
        guideDisplay2.localPosition = newpos;
        guideDisplay2.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));
        guideDisplay2.GetComponent<MeshRenderer>().material.SetVector("_Dims", new Vector4(k_vE.x, k_vE.z, 0, 0));

        //set height of 9ball marker
        newpos = marker9ball.transform.localPosition; newpos.y = 0;
        newpos += Vector3.down * -0.003f / marker9ball.transform.localScale.y;
        marker9ball.transform.localPosition = newpos;
#if EIJIS_CALLSHOT
        newpos = markerCalledBall.transform.localPosition; newpos.y = 0;
        newpos += Vector3.down * 0.003f / markerCalledBall.transform.localScale.y;
        markerCalledBall.transform.localPosition = newpos;
#endif

        initializeRack();
        ConfineBallTransformsToTable();

        menuManager._RefreshTable();
    }

    private void SetTableTransforms()
    {
        Transform table_base = _GetTableBase().transform;
        auto_pocketblockers = table_base.Find(".4BALL_FILL").gameObject;
        auto_rackPosition = table_base.Find(".RACK").gameObject;
        auto_colliderBaseVFX = table_base.Find("collision.vfx").gameObject;

        Transform NAME_0_SPOT = table_base.Find(".NAME_0");
        Transform MENU_SPOT = table_base.Find(".MENU");

        Transform score_info_root = this.transform.Find("intl.scorecardinfo");
        Transform player0name = score_info_root.Find("player0-name");
        if (NAME_0_SPOT && player0name)
            setTransform(NAME_0_SPOT, player0name);

        Transform NAME_1_SPOT = table_base.Find(".NAME_1");
        Transform player1name = score_info_root.Find("player1-name");
        if (NAME_1_SPOT && player1name)
            setTransform(NAME_1_SPOT, player1name);

        Transform SCORE_0_SPOT = table_base.Find(".SCORE_0");
        Transform player0score = score_info_root.Find("player0-score");
        if (SCORE_0_SPOT && player0score)
            setTransform(SCORE_0_SPOT, player0score);

        Transform SCORE_1_SPOT = table_base.Find(".SCORE_1");
        Transform player1score = score_info_root.Find("player1-score");
        if (SCORE_1_SPOT && player1score)
            setTransform(SCORE_1_SPOT, player1score);

        Transform SNOOKER_INSTRUCTIONS_SPOT = table_base.Find(".SNOOKER_INSTRUCTIONS");
        Transform SnookerInstructions = score_info_root.Find("SnookerInstructions");
        if (SNOOKER_INSTRUCTIONS_SPOT && SnookerInstructions)
            setTransform(SNOOKER_INSTRUCTIONS_SPOT, SnookerInstructions);

        Transform menu = this.transform.Find("intl.menu/MenuAnchor");
        if (MENU_SPOT && menu)
            setTransform(MENU_SPOT, menu);
    }

    private void ConfineBallTransformsToTable()
    {
        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].transform.localPosition = ballsP[i];
            Vector3 thisBallPos = balls[i].transform.localPosition;

            float r_k_CUSHION_RADIUS = k_CUSHION_RADIUS + k_BALL_RADIUS;
            if (thisBallPos.x > k_TABLE_WIDTH - r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = k_TABLE_WIDTH - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.x < -k_TABLE_WIDTH + r_k_CUSHION_RADIUS)
            {
                thisBallPos.x = -k_TABLE_WIDTH + r_k_CUSHION_RADIUS;
            }
            if (thisBallPos.z > k_TABLE_HEIGHT - r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = k_TABLE_HEIGHT - r_k_CUSHION_RADIUS;
            }
            else if (thisBallPos.z < -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS)
            {
                thisBallPos.z = -k_TABLE_HEIGHT + r_k_CUSHION_RADIUS;
            }
            balls[i].transform.localPosition = thisBallPos;
            Vector3 moveDir = -thisBallPos.normalized;
            if (moveDir == Vector3.zero) { moveDir = Vector3.right; }
            moveBallInDirUntilNotTouching_Transform(i, moveDir * k_BALL_RADIUS);
        }
    }

    public GameObject _GetTableBase()
    {
        return tableModels[tableModelLocal].transform.Find("table_artwork").gameObject;
    }

    private void handle4BallHit(Vector3 loc, bool good)
    {
#if EIJIS_ISSUE_FIX // 四つ球で的玉が場外してもポイントできる | player can point even if the target ball is fall out of the field in a 4ball. https://github.com/Sacchan-VRC/MS-VRCSA-Billiards/pull/9/commits/5fb055b98df3660f3f2dde2e8f8eb245d4f1cbac
        if (fallOffFoul) return;
        
#endif
        if (good)
        {
            handle4BallHitGood(loc);
        }
        else
        {
            handle4BallHitBad(loc);
        }

        graphicsManager._SpawnFourBallPoint(loc, good);
        graphicsManager._UpdateScorecard();
    }

    private void handle4BallHitGood(Vector3 p)
    {
        fbMadePoint = true;
        aud_main.PlayOneShot(snd_PointMade, 1.0f);

        if (fbScoresLocal[teamIdLocal] < 10)
            fbScoresLocal[teamIdLocal]++;
    }

    private void handle4BallHitBad(Vector3 p)
    {
        if (fbMadeFoul) return;
        fbMadeFoul = true;

        if (fbScoresLocal[teamIdLocal] > 0)
            fbScoresLocal[teamIdLocal]--;
    }

    private void onLocalTeamWin(uint winner)
    {
        Debug.Log("onLocalTeamWin");

        _LogInfo($"onLocalTeamWin {(winner)}");

        networkingManager._OnGameWin(winner);
    }

    private void onLocalTurnPass()
    {
        _LogInfo($"onLocalTurnPass");

        networkingManager._OnTurnPass(teamIdLocal ^ 0x1u);
    }

    private void onLocalTurnTie()
    {
        _LogInfo($"onLocalTurnTie");

        networkingManager._OnTurnTie();
    }

    private void onLocalTurnFoul(bool Scratch, bool objBlocked)
    {
        _LogInfo($"onLocalTurnFoul");

        networkingManager._OnTurnFoul(teamIdLocal ^ 0x1u, Scratch, objBlocked);
    }

    private void onLocalTurnContinue()
    {
        _LogInfo($"onLocalTurnContinue");

        networkingManager._OnTurnContinue();
    }

    private void onLocalTimerEnd()
    {
        timerRunning = false;
#if EIJIS_SEMIAUTOCALL
        semiAutoCallTick = false;
#endif

        _LogWarn("out of time!");

        graphicsManager._HideTimers();

        canPlayLocal = false;

#if EIJIS_CALLSHOT
        calledBallId = -2;
        calledPocketId = -2;
#if EIJIS_SEMIAUTOCALL
        semiAutoCalledPocket = false;
        semiAutoCalledTimeBall = 0;
#endif
#endif
        if (Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject))
        {
            fakeFoulShot();
        }
    }

    private void applyCueAccess()
    {
#if EIJIS_ISSUE_FIX // 2本目のキュー座標がリセットされない場合がある （ issue #3 の修正）
        if (!gameLive)
        {
            if (_TeamPlayersOffline(0)) cueControllers[0]._SetAuthorizedOwners(Networking.IsMaster ? new[] { Networking.LocalPlayer.playerId } : new int[0]);
            if (_TeamPlayersOffline(1)) cueControllers[1]._SetAuthorizedOwners(Networking.IsMaster ? new[] { Networking.LocalPlayer.playerId } : new int[0]);
        }
#endif
        if (localPlayerId == -1 || !gameLive)
        {
            cueControllers[0]._Disable();
            cueControllers[1]._Disable();
            return;
        }

        if (localTeamId == 0)
        {
            cueControllers[0]._Enable();
            cueControllers[1]._Disable();
        }
        else
        {
            cueControllers[1]._Enable();
            cueControllers[0]._Disable();
        }
    }

    private void enablePlayComponents()
    {
        bool isOurTurnVar = isMyTurn();

#if EIJIS_10BALL
#if EIJIS_CUEBALLSWAP
        if (is9Ball || is10Ball || isPyramid)
#else
        if (is9Ball || is10Ball)
#endif
#else
#if EIJIS_CUEBALLSWAP
        if (is9Ball || isPyramid)
#else
        if (is9Ball)
#endif
#endif
        {
            marker9ball.SetActive(true);
            _Update9BallMarker();
        }

        refreshBallPickups();

#if EIJIS_CUEBALLSWAP
        if (isOurTurnVar && isPyramid)
        {
            desktopManager._CallCueBallSetActive(true);
#if EIJIS_CALLSHOT
            if (Networking.LocalPlayer.IsUserInVR()) menuManager._EnableCallLockMenu();
#endif
        }
        else
        {
            desktopManager._CallCueBallSetActive(false);
        }

#endif
#if EIJIS_PUSHOUT
#if EIJIS_10BALL
        bool canPushOut = (isOurTurnVar && (is8Ball || is9Ball || is10Ball) && (pushOutStateLocal == PUSHOUT_DONT || pushOutStateLocal == PUSHOUT_DOING));
#else
        bool canPushOut = (isOurTurnVar && (is8Ball || is9Ball) && (pushOutStateLocal == PUSHOUT_DONT || pushOutStateLocal == PUSHOUT_DOING));
#endif
        desktopManager._PushOutSetActive(canPushOut);
        if (canPushOut)
        {
            menuManager._EnablePushOutMenu();
        }
        else
        {
            menuManager._DisablePushOutMenu();
        }
#endif
#if EIJIS_CALLSHOT
        markerCalledBall.SetActive(false);
#if EIJIS_10BALL
        if (isOurTurnVar && (is8Ball || is9Ball || is10Ball))
#else
        if (isOurTurnVar && (is8Ball || is9Ball))
#endif
        {
            if (requireCallShotLocal)
            {
                if (Networking.LocalPlayer.IsUserInVR()) menuManager._EnableCallLockMenu();
                desktopManager._CallShotSetActive(true);
            }
            else
            {
                menuManager._DisableCallLockMenu();
                desktopManager._CallShotSetActive(false);
            }
        }
        else
        {
            desktopManager._CallShotSetActive(false);
        }

        if (!isOurTurnVar)
        {
            menuManager._DisableCallLockMenu();
        }
#endif
        
        if (isOurTurnVar)
        {
            // Update for desktop
            desktopManager._AllowShoot();
            menuManager._EnableSkipTurnMenu();
        }
        else
        {
            desktopManager._DenyShoot();
            menuManager._DisableSkipTurnMenu();
        }

        if (timerLocal > 0)
        {
            timerRunning = true;
            graphicsManager._ShowTimers();
        }
    }

    public void _SkipTurn()
    {
        if (!isMyTurn()) { return; }
#if EIJIS_CALLSHOT
        calledBallId = -2;
        calledPocketId = -2;
#if EIJIS_SEMIAUTOCALL
        semiAutoCallTick = false;
        semiAutoCalledPocket = false;
        semiAutoCalledTimeBall = 0;
#endif
#endif
#if EIJIS_PUSHOUT
        if (pushOutStateLocal == PUSHOUT_REACTIONING /* || pushOutStateLocal == PUSHOUT_ILLEGAL_REACTIONING */)
        {
#if EIJIS_DEBUG_PUSHOUT
            _LogInfo($"  set {((pushOutStateLocal == PUSHOUT_REACTIONING)? "ENDED" : "DONT")} pushOutState {PushOutState[pushOutStateLocal]}({pushOutStateLocal})");
#endif
            networkingManager.pushOutStateSynced = (pushOutStateLocal == PUSHOUT_REACTIONING)? PUSHOUT_ENDED : PUSHOUT_DONT;
            onLocalTurnPass();
            return;
        }
#endif
        fakeFoulShot();
    }

    public void fakeFoulShot()
    {
        onRemoteTurnSimulate(Vector3.zero, Vector3.zero, true);
        _TriggerSimulationEnded(false, true);
    }

#if EIJIS_PUSHOUT || EIJIS_CUEBALLSWAP
    public void _CallShotLock()
    {
        networkingManager._OnCallShotLockChanged(!callShotLockLocal);
    }

#endif
#if EIJIS_PUSHOUT
    public void _PushOut()
    {
        networkingManager._OnPushOutChanged(pushOutStateLocal);
    }

#endif
    public void _Update9BallMarker()
    {
        if (marker9ball.activeSelf)
        {
#if EIJIS_CUEBALLSWAP
            if (isPyramid)
            {
                marker9ball.transform.localPosition = ballsP[0];
            }
            else
            {
                int target = findLowestUnpocketedBall(ballsPocketedLocal);
                // move without changing y
                Vector3 oldpos = marker9ball.transform.localPosition;
                Vector3 newpos = ballsP[target];
                marker9ball.transform.localPosition = new Vector3(newpos.x, oldpos.y, newpos.z);
            }
#else
            int target = findLowestUnpocketedBall(ballsPocketedLocal);
            // move without changing y
            Vector3 oldpos = marker9ball.transform.localPosition;
            Vector3 newpos = ballsP[target];
            marker9ball.transform.localPosition = new Vector3(newpos.x, oldpos.y, newpos.z);
#endif
        }
    }

#if EIJIS_CALLSHOT
    public void _UpdateCalledBallMarker()
    {
        if (!gameLive)
        {
            return;
        }

#if EIJIS_10BALL
        if (!is8Ball && !is9Ball && !is10Ball)
#else
        if (!is8Ball && !is9Ball)
#endif
        {
            return;
        }

        if (calledBallsLocal == 0)
        {
            markerCalledBall.SetActive(false);
        }
        
        int target = 0;
        uint ball_bit = 0x2u;
#if EIJIS_10BALL
        for (int k = 0; k < (is9Ball ? break_order_9ball.Length : (is10Ball ? break_order_10ball.Length : break_order_8ball.Length)); k++)
#else
        for (int k = 0; k < (is9Ball ? break_order_9ball.Length : break_order_8ball.Length); k++)
#endif
        {
            int i = k + 1;
            if ((calledBallsLocal & ball_bit) != 0x0u)
            {
                target = i;
            }                
            ball_bit <<= 1;
        }

        if (0 < target && 0 == (ballsPocketedLocal & (0x1 << target)))
        {
            bool callShotLock = callShotLockLocal && turnStateLocal != 1;
            markerCalledBall.GetComponent<MeshRenderer>().material = callShotLock ? calledBallMarkerGray :
                (isTableOpenLocal ? calledBallMarkerWhite :
                    ((teamIdLocal ^ teamColorLocal) == 0 ? calledBallMarkerBlue : calledBallMarkerOrange));
            markerCalledBall.transform.localPosition = ballsP[target];
            markerCalledBall.SetActive(true);
        }
        else
        {
            markerCalledBall.SetActive(false);
        }
    }

    public void _UpdateCalledPocketMarker()
    {
        graphicsManager._UpdatePointPocketMarker(pointPocketsLocal, false);
    }

#endif
    // turn off any game elements that are enabled when someone is taking a shot
    private void disablePlayComponents()
    {
        marker9ball.SetActive(false);
#if EIJIS_CALLSHOT
        markerCalledBall.SetActive(false);
#endif
        setFoulPickupEnabled(false);
        refreshBallPickups();
        devhit.SetActive(false);
        guideline.SetActive(false);
        guideline2.SetActive(false);
        isGuidelineValid = false;
        isReposition = false;
        auto_colliderBaseVFX.SetActive(false);

#if EIJIS_CALLSHOT
        menuManager._DisableCallLockMenu();
#endif
#if EIJIS_PUSHOUT
        menuManager._DisablePushOutMenu();
        desktopManager._PushOutSetActive(false);
#if EIJIS_CUEBALLSWAP
        desktopManager._CallCueBallSetActive(false);
#endif
        
#endif
        desktopManager._DenyShoot();
        graphicsManager._HideTimers();
    }

    public void fourBallReturnBalls()
    {
        bool zeroPocketed = false;
        bool thirteenPocketed = false;
        bool fourteenPocketed = false;
        bool fifteenPocketed = false;
        if (fourBallCueBallLocal == 0) // the balls get their positions and color swapped so player 2 can hit the 'yellow' cue ball
        {
            if ((ballsPocketedLocal & (0x1U)) > 0)
            { ballsP[0] = initialPositions[2][0]; zeroPocketed = true; }
            if ((ballsPocketedLocal & (0x1U << 13)) > 0)
            { ballsP[13] = initialPositions[2][13]; thirteenPocketed = true; }
        }
        else
        {
            if ((ballsPocketedLocal & (0x1U)) > 0)
            { ballsP[0] = initialPositions[2][13]; zeroPocketed = true; }
            if ((ballsPocketedLocal & (0x1U << 13)) > 0)
            { ballsP[13] = initialPositions[2][0]; thirteenPocketed = true; }
        }

        if ((ballsPocketedLocal & (0x1U << 14)) > 0)
        { ballsP[14] = initialPositions[2][14]; fourteenPocketed = true; }
        if ((ballsPocketedLocal & (0x1U << 15)) > 0)
        { ballsP[15] = initialPositions[2][15]; fifteenPocketed = true; }
#if EIJIS_CAROM
        fifteenPocketed = (fifteenPocketed && !is3Cusion && !is2Cusion && !is1Cusion && !is0Cusion);
#endif

#if EIJIS_CAROM
        ballsPocketedLocal = initialBallsPocketed[gameModeLocal];
#else
        ballsPocketedLocal = initialBallsPocketed[2];
#endif
#if EIJIS_CAROM
        if (is3Cusion || is2Cusion || is1Cusion || is0Cusion)
        {
            int[] threeBalls = new int[] { 0, 13, 14 };
            bool[] threeBallsPocketed = new bool[] { zeroPocketed, thirteenPocketed, fourteenPocketed };
            Vector3[] threeBallReturnPositions = new Vector3[]
            {
                new Vector3(0.0f, 0.0f, 0.0f), 
                initialPositions[gameModeLocal][13], 
                initialPositions[gameModeLocal][14]
            };
            
            for (int i = 0; i < threeBallsPocketed.Length; i++)
            {
                if (threeBallsPocketed[i])
                {
                    ballsP[threeBalls[i]] = threeBallReturnPositions[i];
                }
            }
            
            for (int i = 0; i < threeBallsPocketed.Length; i++)
            {
                if (threeBallsPocketed[i])
                {
                    int touchBallId = CheckIfBallTouchingBall(threeBalls[i]);
                    if (0 <= touchBallId)
                    {
                        
                        ballsP[touchBallId] = threeBallReturnPositions[Array.IndexOf(threeBalls, touchBallId)];
                        for (int j = 0; j < threeBallsPocketed.Length; j++)
                        {
                            if (j == i)
                                continue;
                            
                            int touchBallId_2 = CheckIfBallTouchingBall(threeBalls[j]);
                            if (0 <= touchBallId_2)
                            {
                                ballsP[touchBallId_2] = threeBallReturnPositions[Array.IndexOf(threeBalls, touchBallId_2)];
                            }
                        }
                    }
                }
            }
            
            return;
        }
#endif
        Vector3 dir = Vector3.right * k_BALL_RADIUS * .051f;
        if (zeroPocketed) moveBallInDirUntilNotTouching(0, dir);
        if (thirteenPocketed) moveBallInDirUntilNotTouching(13, dir);
        if (fourteenPocketed) moveBallInDirUntilNotTouching(14, dir);
        if (fifteenPocketed) moveBallInDirUntilNotTouching(15, dir);
    }

    public string sixRedNumberToColor(int ball, bool doBreakOrder = false)
    {
#if EIJIS_SNOOKER15REDS
        if ((doBreakOrder && (ball < 0 || 20 < ball)) ||
            (!doBreakOrder && ((ball < 0 || 15 < ball) && (ball < 25 || 30 < ball))))
#else
        if (ball < 0 || ball > 12)
#endif
        {
            _LogWarn("sixRedNumberToColor: ball index out of range");
            return "Invalid";
        }
        if (doBreakOrder)
        {
            ball = break_order_sixredsnooker[ball];
        }
        switch (ball)
        {
            case 2: return "Yellow";
            case 7: return "Green";
            case 8: return "Brown";
            case 3: return "Blue";
            case 5: return "Pink";
            case 1: return "Black";
            case 0: return "White";
            default: return "Red";
        }
    }

    private int SixRedCheckObjBlocked(uint field, bool colorTurn, bool includeFreeBall)
    {
        //in case of undo/redo the results of these methods need to be re-calculated
        bool redOnTable = sixRedCheckIfRedOnTable(field, false);
        int nextcolor = sixRedFindLowestUnpocketedColor(field);
        uint objective = sixRedGetObjective(colorTurn, redOnTable, nextcolor, false, includeFreeBall);
        // 0 = fully visible, 1 = left OR right blocked, 2 = both blocked
        return objVisible(objective);
    }

    public int sixRedFindLowestUnpocketedColor(uint field)
    {
#if EIJIS_SNOOKER15REDS
        for (int i = SNOOKER_REDS_COUNT; i < break_order_sixredsnooker.Length; i++)
#else
        for (int i = 6; i < break_order_sixredsnooker.Length; i++)
#endif
        {
            if (((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U)
            {
                return i;
            }
        }

        return -1;
    }

    public bool sixRedCheckIfRedOnTable(uint field, bool writeLog)
    {
#if EIJIS_SNOOKER15REDS
        for (int i = 0; i < SNOOKER_REDS_COUNT; i++)
#else
        for (int i = 0; i < 6; i++)
#endif
        {
            if (((field >> break_order_sixredsnooker[i]) & 0x1U) == 0x00U)
            {
                if (writeLog)
                {
                    _LogInfo("6RED: All reds not yet pocketed");
                }
                return true;
            }
        }
        return false;
    }

    public int sixRedCheckFirstHit(int firstHit)
    {
        //return 0 for red hit
        uint firstHitball = 1u << firstHit;
#if EIJIS_SNOOKER15REDS
        if ((firstHitball & SNOOKER_REDS_MASK) > 0)
#else
        if ((firstHitball & 0x1E50u) > 0)
#endif
        {
            _LogInfo("6RED: Hit first: Red");
            return 0;
        }
        //return 1 for color hit
        if ((firstHitball & 0x1AE) > 0)
        {
            if (foulStateLocal == 5)
            {
                _LogInfo("6RED: Hit first: (free ball)");
                return 0;
            }
            else
            {
                _LogInfo("6RED: Hit first: Color");
                return 1;
            }
        }
        return -1;
    }

    public void sixRedReturnColoredBalls(int from)
    {
#if EIJIS_SNOOKER15REDS
        if (from < SNOOKER_REDS_COUNT)
#else
        if (from < 6)
#endif
        {
            _LogWarn("sixRedReturnColoredBalls() requested return of red balls");
            return;
        }
#if EIJIS_SNOOKER15REDS
        for (int i = Mathf.Max(SNOOKER_REDS_COUNT, from); i < break_order_sixredsnooker.Length; i++)
#else
        for (int i = Mathf.Max(6, from); i < break_order_sixredsnooker.Length; i++)
#endif
        {
            if ((ballsPocketedLocal & (1 << break_order_sixredsnooker[i])) > 0)
            {
                // ballsP[break_order_sixredsnooker[i]] = initialPositions[4][break_order_sixredsnooker[i]];
                sixRedMoveBallUntilNotTouching(break_order_sixredsnooker[i]);
                ballsPocketedLocal = ballsPocketedLocal ^ (1u << break_order_sixredsnooker[i]);
            }
        }
    }

    public void sixRedScoreBallsPocketed(bool redOnTable, int nextColor, ref int ballscore, ref int numBallsPocketed, ref int highestScoringBall)
    {
        bool freeBall = foulStateLocal == 5;
        bool nextColorPocketed = false;
        bool freeBallPocketed = false;
#if EIJIS_SNOOKER15REDS
        for (int i = 1; i < sixredsnooker_ballpoints.Length; i++)
#else
        for (int i = 1; i < 13; i++)
#endif
        {
            if ((ballsPocketedLocal & (1u << i)) > (ballsPocketedOrig & (1u << i)))
            {
                int thisBallScore = sixredsnooker_ballpoints[i];
                if (freeBall)
                {
                    // pocketing freeball and the nextColor in the same turn is actually legal, but you don't add the points up from potting both
                    // because in the freeball rule, they're the same ball, unless they're reds.
                    // since the break_order_sixredsnooker[] is not in sequential order it has to be checked both ways
                    if (i == break_order_sixredsnooker[nextColor])
                    {
                        // _LogInfo("6RED: nextColor Pocketed in freeball turn");
                        nextColorPocketed = true;
                        if (freeBallPocketed)
                        {
                            thisBallScore = 0;
                            numBallsPocketed--;// prevent foul
                        }
                    }
                    else if (i == firstHit)
                    {
                        // _LogInfo("6RED: freeBall Pocketed in freeball turn");
                        freeBallPocketed = true;
                        if (redOnTable)
                        {
                            thisBallScore = 1;
                        }
                        else if (nextColorPocketed)
                        {
                            thisBallScore = 0;
                            numBallsPocketed--;// prevent foul
                        }
                        else
                        {
                            thisBallScore = sixredsnooker_ballpoints[break_order_sixredsnooker[sixRedFindLowestUnpocketedColor(ballsPocketedOrig)]];
                        }
                    }
                }
                if (highestScoringBall < thisBallScore)
                { highestScoringBall = thisBallScore; }
                ballscore += thisBallScore;
                numBallsPocketed++;
                if (freeBall && firstHit == i)
                {
                    _LogInfo("6RED: " + sixRedNumberToColor(i) + "(free ball) pocketed");
                }
                else
                {
                    _LogInfo("6RED: " + sixRedNumberToColor(i) + " ball pocketed");
                }
            }
        }
    }

    public int sixRedCheckBallTypesPocketed(uint ballsPocketedOrig, uint ballsPocketedLocal)
    {
        // for free ball : convert firsthit to a mask and add/remove it from red/color masks
#if EIJIS_SNOOKER15REDS
        uint redMask = SNOOKER_REDS_MASK;
#else
        uint redMask = 0x1E50u;
#endif
        uint colorMask = 0x1AE;
        if (foulStateLocal == 5)
        {
            uint firstHitMask = 1u << firstHit;
            redMask = redMask | firstHitMask;
            colorMask = colorMask & ~firstHitMask;
        }
        int result = -1;
        if ((ballsPocketedOrig & redMask) < (ballsPocketedLocal & redMask))
        {
            // _LogInfo("6RED: At least one red ball was pocketed");
            result = 0;
        }
        if ((ballsPocketedOrig & colorMask) < (ballsPocketedLocal & colorMask))
        {
            if (result == 0)
            {
                result = 2;
                _LogInfo("6RED: Both Red and color balls were pocketed");
            }
            else
            {
                result = 1;
                // _LogInfo("6RED: At least one color ball pocketed");
            }

        }
        return result;
    }

    public uint sixRedGetObjective(bool _colorTurn, bool _redOnTable, int _nextcolor, bool writeLog, bool includeFreeBall)
    {
#if EIJIS_SNOOKER15REDS
        uint objective = SNOOKER_REDS_MASK;
#else
        uint objective = 0x1E50u;
#endif
        if (writeLog)
        {
            if (_colorTurn) { _LogInfo("6RED: That was a ColorTurn"); }
            else { _LogInfo("6RED: That was not a ColorTurn"); }
        }
        if (_colorTurn)
        {
            objective = 0x1AE;//color balls
            if (writeLog) { _LogInfo("6RED: Objective is: Any color"); }
        }
        else if (!_redOnTable)
        {
            objective = (uint)(1 << break_order_sixredsnooker[_nextcolor]);
            if (writeLog) { _LogInfo("6RED: Objective is: " + sixRedNumberToColor(_nextcolor, true)); }
        }
        else
        {
            if (writeLog) { _LogInfo("6RED: Objective is: Red"); }
        }
        if (includeFreeBall && foulStateLocal == 5) // add freeball to objective
        {
            objective = objective | 1u << firstHit;
        }
        return objective;
    }

    public int findLowestUnpocketedBall(uint field)
    {
        for (int i = 2; i <= 8; i++)
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        if (((field) & 0x2U) == 0x00U)
            return 1;

#if EIJIS_MANY_BALLS
        for (int i = 9; i < MAX_BALLS; i++)
#else
        for (int i = 9; i < 16; i++)
#endif
        {
            if (((field >> i) & 0x1U) == 0x00U)
                return i;
        }

        // ??
        return 0;
    }

#if EIJIS_SEMIAUTOCALL
    public int findNearestPocketFromBall(int ballId)
    {
        int pocketId = -1;
        Vector3 ballPos = ballsP[ballId];
        float abs_x = Mathf.Abs(ballPos.x);
        float abs_z_n = Mathf.Abs(ballPos.z) * findNearestPocket_n;
        bool nearSide = (abs_x + abs_z_n < findNearestPocket_x);
        if (ballPos.z < 0)
        {
            if (nearSide)
            {
                pocketId = 5;
            }
            else if (ballPos.x < 0)
            {
                pocketId = 3;
            }
            else
            {
                pocketId = 1;
            }
        }
        else
        {
            if (nearSide)
            {
                pocketId = 4;
            }
            else if (ballPos.x < 0)
            {
                pocketId = 2;
            }
            else
            {
                pocketId = 0;
            }
        }

        return pocketId;
    }

    public uint findEasiestBallAndPocket(uint field)
    {
        Vector3[][] matrix = new Vector3[16][];
        for (int i = 0; i < matrix.Length; i++)
        {
            matrix[i] = new Vector3[pocketLocations.Length];
            for (int j = 0; j < matrix[i].Length; j++)
            {
                matrix[i][j] = Vector3.positiveInfinity;
            }
        }

        int[] matrixIndexPosListByCondition = new int[findEasiestBallAndPocketConditions.Length];
        int[][] matrixIndexListByCondition = new int[findEasiestBallAndPocketConditions.Length][];
        for (int i = 0; i < matrixIndexListByCondition.Length; i++)
        {
            matrixIndexPosListByCondition[i] = 0;
            matrixIndexListByCondition[i] = new int[matrix.Length * matrix[0].Length];
            for (int j = 0; j < matrixIndexListByCondition[i].Length; j++)
            {
                matrixIndexListByCondition[i][j] = -1;
            }
        }

        for (int i = 1; i < 16; i++)
        {
            if (((field >> i) & 0x1U) == 0x1U)
            {
                continue;
            }

            Vector3 cue2target = ballsP[i] - ballsP[0];
            float c2tRad = -Mathf.Atan2(cue2target.z, cue2target.x);
            float c2tDeg = c2tRad * Mathf.Rad2Deg;
            float c2tSqrMagnitude = Vector3.SqrMagnitude(cue2target);

            for (int j = 0; j < pocketLocations.Length; j++)
            {
                Vector3 target2pocket = pocketLocations[j] - ballsP[i];
                float t2pRad = -Mathf.Atan2(target2pocket.z, target2pocket.x);
                float t2pDeg = t2pRad * Mathf.Rad2Deg;
                float t2pSqrMagnitude = Vector3.SqrMagnitude(target2pocket);
#if EIJIS_DEBUG_SEMIAUTO_CALL_SIDE
                if (debugLogFlg && 4 <= j) _LogInfo($"  t{i} to side{j} t2pDeg = {t2pDeg}");
#endif
                if (4 <= j && ((t2pDeg < 0 &&(t2pDeg < -135 || -45 < t2pDeg)) || (0 <= t2pDeg &&(t2pDeg < 45 || 135 < t2pDeg))))
                {
#if EIJIS_DEBUG_SEMIAUTO_CALL_SIDE
                    if (debugLogFlg) _LogInfo($"  side pocket skip");
#endif
                    continue;
                }

                float degDiff = c2tDeg - t2pDeg;
                if (degDiff < 0)
                {
                    degDiff = -degDiff;
                }
                if (180 < degDiff)
                {
                    degDiff = 360 - degDiff;
                }
                
                // x:deg, y:t2p, z:c2t
                Vector3 p = matrix[i][j] = new Vector3(degDiff, t2pSqrMagnitude, c2tSqrMagnitude);
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                // if (debugLogFlg) _LogInfo($"  matrix[{i}][{j}]] = {p.x}, {p.y}, {p.z}");
#endif

                 for (int k = 0; k < findEasiestBallAndPocketConditions.Length; k++)
                {
                    Vector3 c = findEasiestBallAndPocketConditions[k];
                    if (p.x < c.x && p.y < c.y && p.z < c.z)
                    {
                        matrixIndexListByCondition[k][matrixIndexPosListByCondition[k]++] = (i * 16) + j;
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                        // if (debugLogFlg) _LogInfo($"    k = {k},  (i * 16) + j = {matrixIndexListByCondition[k][matrixIndexPosListByCondition[k]-1]}");
#endif
                        break;
                    }
                }
            }
        }

#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
        if (debugLogFlg)
        {
            for (int i = 0; i < matrixIndexPosListByCondition.Length; i++)
            {
                _LogInfo($"  matrixIndexPosListByCondition[{i}] = {matrixIndexPosListByCondition[i]}");
                for (int j = 0; j < matrixIndexListByCondition[i].Length; j++)
                {
                    if (matrixIndexListByCondition[i][j] < 0) break;
                    _LogInfo($"  matrixIndexListByCondition[{i}][{j}] = {matrixIndexListByCondition[i][j]}");
                }
            }
        }
#endif

        int easiestBallId = -1;
        int easiestPocketId = -1;
        for (int i = 0; i < matrixIndexListByCondition.Length; i++)
        {
            int nearestBallId = -1;
            int nearestPocketId = -1;
            float minSqrMagnitude = float.MaxValue;
            for (int j = 0; j < matrixIndexListByCondition[i].Length; j++)
            {
                int materixIndex = matrixIndexListByCondition[i][j];
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                // if (debugLogFlg) _LogInfo($"  materixIndex = {materixIndex}, matrixIndexListByCondition[{i}][{j}]] = {matrixIndexListByCondition[i][j]}");
#endif
                if (materixIndex < 0 || matrixIndexPosListByCondition[i] <= j)
                {
                    continue;
                }

                int ballId = materixIndex / 16;
                int pocketId = materixIndex % 16;
                Vector3 p = matrix[ballId][pocketId];
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                if (debugLogFlg) _LogInfo($"  materixIndex = {materixIndex}, matrixIndexListByCondition[{i}][{j}]] = {matrixIndexListByCondition[i][j]}");
                if (debugLogFlg) _LogInfo($"  matrix[ballId = {ballId}][pocketId = {pocketId}]] = {p.x}, {p.y}, {p.z}");
#endif
                
                float sqrMagnitude = p.z;
                if (0 <= sqrMagnitude && sqrMagnitude < minSqrMagnitude)
                {
                    minSqrMagnitude = sqrMagnitude;
                    nearestBallId = ballId;
                    nearestPocketId = pocketId;
                }
            }

            if (0 <= nearestBallId && 0 <= nearestPocketId)
            {
                easiestBallId = nearestBallId;
                easiestPocketId = nearestPocketId;
                break;
            }
        }
        
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
        if (debugLogFlg) _LogInfo($"  easiestBallId = {easiestBallId}, easiestPocketId = {easiestPocketId}");
#endif

        return  ((easiestPocketId < 0 ? 0xFFFFu : (uint)easiestPocketId) << 16) | (easiestBallId < 0 ? 0xFFFFu : (uint)easiestBallId);
    }
    
#endif
#if UNITY_EDITOR
    public void DBG_DrawBallMask(uint ballMask)
    {
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if ((ballsPocketedLocal & (1 << i)) > 0) { continue; }
            if ((ballMask & (1 << i)) == 0) { continue; }
            Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.white, 3f);
        }
    }

    public void DBG_TestObjVisible()
    {
        uint redmask = 0;
#if EIJIS_SNOOKER15REDS
        for (int i = 0; i < SNOOKER_REDS_COUNT; i++)
#else
        for (int i = 0; i < 6; i++)
#endif
        {
            redmask += ((uint)1 << break_order_sixredsnooker[i]);
        }
        // DBG_DrawBallMask(redmask);
        switch (objVisible(redmask))
        {
            case 0:
                _LogInfo("A Red ball CAN be seen");
                break;
            case 1:
                _LogInfo("A Red ball can be seen on ONE side");
                break;
            case 2:
                _LogInfo("A Red ball can NOT be seen");
                break;
        }
    }
#endif

#if EIJIS_MANY_BALLS
    int[] objVisible_blockingBalls = new int[MAX_BALLS * 2];
#else
    int[] objVisible_blockingBalls = new int[32];
#endif
    int objVisible_blockingBalls_len;
    int objVisible(uint objMask)
    {
        int mostVisible = 2;
#if EIJIS_MANY_BALLS
        objVisible_blockingBalls = new int[MAX_BALLS * 2];
        for (int i = 0; i < (MAX_BALLS * 2); i++) objVisible_blockingBalls[i] = -1;
#else
        objVisible_blockingBalls = new int[32];
        for (int i = 0; i < 32; i++) objVisible_blockingBalls[i] = -1;
#endif
        objVisible_blockingBalls_len = 0;
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if ((objMask & (1 << i)) > 0)
            {
                int ballvis = ballBlocked(0, i, true);
                // if (ballvis == 1)
                // { Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.red, 3f); }
                // if (ballvis == 0)
                // { Debug.DrawRay(balls[0].transform.parent.TransformPoint(ballsP[i]), Vector3.up * .3f, Color.white, 3f); }
                if (mostVisible > ballvis)
                {
                    mostVisible = ballvis;
                }
                if (mostVisible == 0)
                {
                    break;
                }
                objVisible_blockingBalls[objVisible_blockingBalls_len] = ballBlocked_blockingBalls[0];
                objVisible_blockingBalls_len++;
                objVisible_blockingBalls[objVisible_blockingBalls_len] = ballBlocked_blockingBalls[1];
                objVisible_blockingBalls_len++;
            }
        }
        return mostVisible;
    }
    int[] ballBlocked_blockingBalls = new int[2];
    int ballBlocked(int from, int to, bool ignoreReds)
    {
        ballBlocked_blockingBalls = new int[2] { -1, -1 };
        Vector3 center = (ballsP[from] + ballsP[to]) / 2;
        float cenMag = (ballsP[from] - center).magnitude;

        Vector2 out1 = Vector3.zero, out2 = Vector3.zero, out3 = Vector3.zero, out4 = Vector3.zero,
            circle1, circle2, center2;
        circle1 = new Vector2(ballsP[from].x, ballsP[from].z);
        circle2 = new Vector2(ballsP[to].x, ballsP[to].z);
        // float Ball1Rad = k_BALL_RADIUS;
        // float Ball2Rad = k_BALL_RADIUS;
        center2 = new Vector2(center.x, center.z);

        FindCircleCircleIntersections(center2, cenMag, circle1, k_BALL_DIAMETRE /* Ball1Rad + Ball2Rad */, out out1, out out2);
        FindCircleCircleIntersections(center2, cenMag, circle2, k_BALL_DIAMETRE /* Ball1Rad + Ball2Rad */, out out3, out out4);

        Vector3 ipoint1 = new Vector3(out1.x, ballsP[from].y, out1.y);
        Vector3 ipoint2 = new Vector3(out2.x, ballsP[from].y, out2.y);
        Vector3 ipoint3 = new Vector3(out3.x, ballsP[from].y, out3.y);
        Vector3 ipoint4 = new Vector3(out4.x, ballsP[from].y, out4.y);

        Vector3 innerTanPoint1 = ballsP[from] + (ipoint1 - ballsP[from]).normalized * k_BALL_RADIUS /* Ball1Rad */;
        Vector3 innerTanPoint2 = ballsP[from] + (ipoint2 - ballsP[from]).normalized * k_BALL_RADIUS /* Ball1Rad */;
        Vector3 innerTanPoint3 = ballsP[to] + (ipoint3 - ballsP[to]).normalized * k_BALL_RADIUS /* Ball2Rad */;
        Vector3 innerTanPoint4 = ballsP[to] + (ipoint4 - ballsP[to]).normalized * k_BALL_RADIUS /* Ball2Rad */;

        Vector3 innerTanPoint1_oposite = innerTanPoint1 - ballsP[from];
        innerTanPoint1_oposite = ballsP[from] - innerTanPoint1_oposite;
        Vector3 innerTanPoint2_oposite = innerTanPoint2 - ballsP[from];
        innerTanPoint2_oposite = ballsP[from] - innerTanPoint2_oposite;

        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint1), balls[0].transform.parent.TransformDirection(innerTanPoint3 - innerTanPoint1), Color.red, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint2), balls[0].transform.parent.TransformDirection(innerTanPoint4 - innerTanPoint2), Color.blue, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint2_oposite), balls[0].transform.parent.TransformDirection(innerTanPoint4 - innerTanPoint2), Color.blue, 10);
        // Debug.DrawRay(balls[0].transform.parent.TransformPoint(innerTanPoint1_oposite), balls[0].transform.parent.TransformDirection(innerTanPoint3 - innerTanPoint1), Color.red, 10);

        float NearestBlockL = float.MaxValue;
        float NearestBlockR = float.MaxValue;

        float distTo = (ballsP[from] - ballsP[to]).magnitude;
        bool blockedLeft = false;
        bool blockedRight = false;
        // left
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == from) { continue; }
            if (i == to) { continue; }
            if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
            if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
            float distToThis = (ballsP[from] - ballsP[i]).magnitude;
            if (distToThis > distTo) { continue; }
            if (_phy_ray_sphere(innerTanPoint1, innerTanPoint3 - innerTanPoint1, ballsP[i]))
            {
                blockedLeft = true;
                if (NearestBlockL > distToThis)
                { NearestBlockL = distToThis; }
                ballBlocked_blockingBalls[0] = i;
            }
        }
        // right
#if EIJIS_MANY_BALLS
        for (int i = 0; i < MAX_BALLS; i++)
#else
        for (int i = 0; i < 16; i++)
#endif
        {
            if (i == from) { continue; }
            if (i == to) { continue; }
            if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
            if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
            float distToThis = (ballsP[from] - ballsP[i]).magnitude;
            if (distToThis > distTo) { continue; }
            if (_phy_ray_sphere(innerTanPoint2, innerTanPoint4 - innerTanPoint2, ballsP[i]))
            {
                blockedRight = true;
                if (NearestBlockR > distToThis)
                { NearestBlockR = distToThis; }
                ballBlocked_blockingBalls[1] = i;
            }
        }
        // right + ball width
        if (!blockedRight)
        {
#if EIJIS_MANY_BALLS
            for (int i = 0; i < MAX_BALLS; i++)
#else
            for (int i = 0; i < 16; i++)
#endif
            {
                if (i == from) { continue; }
                if (i == to) { continue; }
                if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
                if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
                float distToThis = (ballsP[from] - ballsP[i]).magnitude;
                if (distToThis > distTo) { continue; }
                if (_phy_ray_sphere(innerTanPoint2_oposite, innerTanPoint4 - innerTanPoint2, ballsP[i]))
                {
                    blockedRight = true;
                    if (NearestBlockR > distToThis)
                    { NearestBlockR = distToThis; }
                    ballBlocked_blockingBalls[1] = i;
                }
            }
        }
        // left + ball width
        if (!blockedLeft)
        {
#if EIJIS_MANY_BALLS
            for (int i = 0; i < MAX_BALLS; i++)
#else
            for (int i = 0; i < 16; i++)
#endif
            {
                if (i == from) { continue; }
                if (i == to) { continue; }
                if ((0x1U << i & ballsPocketedLocal) != 0U) { continue; }
                if (ignoreReds && sixredsnooker_ballpoints[i] == 1) { continue; }
                float distToThis = (ballsP[from] - ballsP[i]).magnitude;
                if (distToThis > distTo) { continue; }
                if (_phy_ray_sphere(innerTanPoint1_oposite, innerTanPoint3 - innerTanPoint1, ballsP[i]))
                {
                    blockedLeft = true;
                    if (NearestBlockL > distToThis)
                    { NearestBlockL = distToThis; }
                    ballBlocked_blockingBalls[0] = i;
                }
            }
        }
        // 0 = fully visible, 1 = left OR right blocked, 2 = both blocked
        int blockedLeft_i = blockedLeft ? 1 : 0;
        int blockedRight_i = blockedRight ? 1 : 0;
        return blockedLeft_i + blockedRight_i;
    }

    // Found on Unity Forums. Thanks to QuincyC.
    // Find the points where the two circles intersect.
    private void FindCircleCircleIntersections(Vector2 c0, float r0, Vector2 c1, float r1, out Vector2 intersection1, out Vector2 intersection2)
    {
        // Find the distance between the centers.
        float dx = c0.x - c1.x;
        float dy = c0.y - c1.y;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        if (Mathf.Abs(dist - (r0 + r1)) < 0.00001)
        {
            intersection1 = Vector2.Lerp(c0, c1, r0 / (r0 + r1));
            intersection2 = intersection1;
        }

        // See how many solutions there are.
        if (dist > r0 + r1)
        {
            // No solutions, the circles are too far apart.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else if (dist < Mathf.Abs(r0 - r1))
        {
            // No solutions, one circle contains the other.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else if ((dist == 0) && (r0 == r1))
        {
            // No solutions, the circles coincide.
            intersection1 = new Vector2(float.NaN, float.NaN);
            intersection2 = new Vector2(float.NaN, float.NaN);
        }
        else
        {
            // Find a and h.
            float a = (r0 * r0 -
                        r1 * r1 + dist * dist) / (2 * dist);
            float h = Mathf.Sqrt(r0 * r0 - a * a);

            // Find P2.
            float cx2 = c0.x + a * (c1.x - c0.x) / dist;
            float cy2 = c0.y + a * (c1.y - c0.y) / dist;

            // Get the points P3.
            intersection1 = new Vector2(
                (float)(cx2 + h * (c1.y - c0.y) / dist),
                (float)(cy2 - h * (c1.x - c0.x) / dist));
            intersection2 = new Vector2(
                (float)(cx2 - h * (c1.y - c0.y) / dist),
                (float)(cy2 + h * (c1.x - c0.x) / dist));

        }
    }

    //copy of method from StandardPhysicsManager
    bool _phy_ray_sphere(Vector3 start, Vector3 dir, Vector3 sphere)
    {
        float k_BALL_RSQR = k_BALL_RADIUS * k_BALL_RADIUS;
        Vector3 nrm = dir.normalized;
        Vector3 h = sphere - start;
        float lf = Vector3.Dot(nrm, h);
        float s = k_BALL_RSQR - Vector3.Dot(h, h) + lf * lf;

        if (s < 0.0f) return false;

        s = Mathf.Sqrt(s);

        if (lf < s)
        {
            if (lf + s >= 0)
            {
                s = -s;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private void setBallPickupActive(int ballId, bool active)
    {
        Transform pickup = balls[ballId].transform.GetChild(0);

        pickup.gameObject.SetActive(active);
        pickup.GetComponent<SphereCollider>().enabled = active;
        ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).pickupable = active;
        if (!active) ((VRC_Pickup)pickup.GetComponent(typeof(VRC_Pickup))).Drop();
    }

    private void refreshBallPickups()
    {
        bool canUsePickup = isMyTurn() && isPracticeMode && gameLive;

        uint ball_bit = 0x1u;
        for (int i = 0; i < balls.Length; i++)
        {
            if ((canUsePickup || (i == 0 && isReposition)) && gameLive && canPlayLocal && (ballsPocketedLocal & ball_bit) == 0x0u)
            {
                setBallPickupActive(i, true);
            }
            else
            {
                setBallPickupActive(i, false);
            }
            ball_bit <<= 1;
        }
    }

    private void setFoulPickupEnabled(bool enabled)
    {
        markerObj.SetActive(enabled);
        if (enabled)
        {
            setBallPickupActive(0, true);
        }
        else if (!isPracticeMode)
        {
            setBallPickupActive(0, false);
        }
    }

    private void tickTimer()
    {
        if (gameLive && timerRunning && canPlayLocal)
        {
            float timeRemaining = timerLocal - (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
            float timePercentage = timeRemaining >= 0.0f ? 1.0f - (timeRemaining / timerLocal) : 0.0f;

            if (!localPlayerDistant)
            {
                graphicsManager._SetTimerPercentage(timePercentage);
            }

            if (timeRemaining < 0.0f)
            {
                onLocalTimerEnd();
            }
        }
    }

#if EIJIS_SEMIAUTOCALL
    private void tickSemiAutoCall()
    {
#if EIJIS_DEBUG_SEMIAUTO_CALL
        // if (debugFlg) return;
#endif
        if (!isMyTurn() && 0 < semiAutoCalledTimeBall)
        {
            semiAutoCalledTimeBall = 0;
            return;
        }
        
        bool isOnBreakShot = colorTurnLocal;
#if EIJIS_10BALL
        if ((is8Ball || is9Ball || is10Ball) && gameLive && canPlayLocal && isMyTurn() && !isOnBreakShot && requireCallShotLocal)
#else
        if ((is8Ball || is9Ball) && gameLive && canPlayLocal && isMyTurn() && !isOnBreakShot && requireCallShotLocal)
#endif
        {
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
            if (debugLogFlg) _LogInfo($"TKCH SEMIAUTO_CALL semiAutoCallBall = {semiAutoCallBallLocal}, semiAutoCalledTimeBall = {semiAutoCalledTimeBall}, calledBallId = {calledBallId}");
            if (debugLogFlg) _LogInfo($"                   semiAutoCallPocket = {semiAutoCallPocketLocal}, semiAutoCalledPocket = {semiAutoCalledPocket}, calledPocketId = {calledPocketId}, calledBalls = {calledBallsLocal:X4}");
#endif
            int target = -1;
            int pocketId = -1;

            if (semiAutoCallLocal && ((semiAutoCalledTimeBall <= 0 && calledBallId < 0) ||
                                      (!semiAutoCalledPocket && calledPocketId < 0 && 0 < calledBallsLocal)))
            {
                uint findBalls = ballsPocketedLocal;
#if EIJIS_10BALL
                if (is9Ball || is10Ball)
#else
                if (is9Ball)
#endif
                {
                    findBalls = ~(0x1u << findLowestUnpocketedBall(ballsPocketedLocal));
                }
                else if (is8Ball)
                {
                    if (isTableOpenLocal)
                    {
                        findBalls = ballsPocketedLocal | ~0xFFFFFFFCu;
                    }
                    else
                    {
                        uint bmask = (0x1FCu << ((int)(teamIdLocal ^ teamColorLocal) * 7));
                        bool isSetComplete = (ballsPocketedLocal & bmask) == bmask;
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                        if (debugLogFlg) _LogInfo($"  teamIdLocal = {teamIdLocal}, teamColorLocal = {teamColorLocal}, ^ = {teamIdLocal ^ teamColorLocal}");
                        if (debugLogFlg) _LogInfo($"  findBalls = {ballsPocketedLocal:X4}");
#endif
                        findBalls = isSetComplete ? ~0x2u : ballsPocketedLocal | ~(0xFFFF0000 | bmask);
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                        if (debugLogFlg) _LogInfo($"  findBalls = {findBalls:X4}");
#endif
                    }
                }
                uint pocketAndBall = findEasiestBallAndPocket(findBalls);
                if (pocketAndBall != 0xFFFFFFFF)
                {
                    target = (int)(pocketAndBall & 0xFFFFu);
                    pocketId = (int)((pocketAndBall >> 16) & 0xFFFFu);
                }
            }
            
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC || EIJIS_DEBUG_NEXT_BREAK
            if (debugLogFlg) _LogInfo($"  target(final) = {target}");
#endif
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC || EIJIS_DEBUG_SEMIAUTO_CALL_SIDE || EIJIS_DEBUG_NEXT_BREAK
            // debugLogFlg = false;
#endif
            
            if (0 < target)
            {
                float elapsedSeconds = (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
                
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC
                if (0.1f < elapsedSeconds && elapsedSeconds < 0.12f)
                {
                    _LogInfo($"  semiAutoCalledBall = {semiAutoCalledBall}, calledBallId = {calledBallId}, target = {target}");
                }
#endif

                if (semiAutoCallLocal && semiAutoCalledTimeBall <= 0 && calledBallId < 0)
                {
#if EIJIS_DEBUG_SEMIAUTO_CALL
                    _LogInfo($"  elapsedSeconds = {elapsedSeconds}");
#endif
                    if (0.4f < elapsedSeconds)
                    {
#if EIJIS_DEBUG_SEMIAUTO_CALL
                        _LogInfo($"  elapsedSeconds = {elapsedSeconds}, call _TriggerOtherBallHit(target = {target})");
#endif
                        _TriggerOtherBallHit(target, true);
                        // semiAutoCalledTimeBall = elapsedSeconds;
                        semiAutoCalledTimeBall = 0.4f;
                    }
                }
            }
                
            if (semiAutoCallLocal && !semiAutoCalledPocket && calledPocketId < 0 && 0 < calledBallsLocal)
            {
                float elapsedSeconds = (Networking.GetServerTimeInMilliseconds() - timerStartLocal) / 1000.0f;
#if EIJIS_DEBUG_SEMIAUTO_CALL
                _LogInfo($"  elapsedSeconds = {elapsedSeconds}");
#endif
                if (0.4f + semiAutoCalledTimeBall < elapsedSeconds)
                {
#if EIJIS_DEBUG_SEMIAUTO_CALL
                    _LogInfo($"  elapsedSeconds = {elapsedSeconds}, call _TriggerPocketHit(pocketId = {pocketId}, TRUE)");
#endif
                    _TriggerPocketHit(pocketId, true);
                    semiAutoCalledPocket = true;
#if EIJIS_DEBUG_SEMIAUTO_CALL
                    debugFlg = false;
#endif
                }
            }
            
#if EIJIS_DEBUG_SEMIAUTO_CALL_FINDLOGIC || EIJIS_DEBUG_SEMIAUTO_CALL_SIDE || EIJIS_DEBUG_NEXT_BREAK
            debugLogFlg = false;
#endif
        }
    }

#endif
    public bool isMyTurn()
    {
        return localPlayerId >= 0 && (localTeamId == teamIdLocal || (isPracticeMode && isPlayer));
    }

    public bool _AllPlayersOffline()
    {
        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == -1) continue;

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerIDsLocal[i]);
            if (Utilities.IsValid(player))
            {
                return false;
            }
        }

        return true;
    }

#if EIJIS_ISSUE_FIX // 2本目のキュー座標がリセットされない場合がある （ issue #3 の修正）
    public bool _TeamPlayersOffline(uint teamId)
    {
        for (int i = 0; i < 4; i++)
        {
            if (teamId != (uint)(i & 0x1u)) continue;
            if (playerIDsLocal[i] == -1) continue;

            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerIDsLocal[i]);
            if (Utilities.IsValid(player))
            {
                return false;
            }
        }

        return true;
    }

#endif
    public VRCPlayerApi _GetPlayerByName(string name)
    {
        VRCPlayerApi[] onlinePlayers = VRCPlayerApi.GetPlayers(new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]);
        for (int playerId = 0; playerId < onlinePlayers.Length; playerId++)
        {
            if (onlinePlayers[playerId].displayName == name)
            {
                return onlinePlayers[playerId];
            }
        }
        return null;
    }

    public void _IndicateError()
    {
        graphicsManager._FlashTableError();
    }

    public void _IndicateSuccess()
    {
        graphicsManager._FlashTableLight();
    }

    public string _SerializeGameState()
    {
        return networkingManager._EncodeGameState();
    }

    public void _LoadSerializedGameState(string gameState)
    {
        // no loading on top of other people's games
        if (!_IsPlayer(Networking.LocalPlayer)) return;

        networkingManager._OnLoadGameState(gameState);
        // practiceManager._Record();
    }

    public object[] _SerializeInMemoryState()
    {
        Vector3[] positionClone = new Vector3[ballsP.Length];
        Array.Copy(ballsP, positionClone, ballsP.Length);
        byte[] scoresClone = new byte[fbScoresLocal.Length];
        Array.Copy(fbScoresLocal, scoresClone, fbScoresLocal.Length);
#if EIJIS_PUSHOUT
        return new object[14]
#else
        return new object[13]
#endif
        {
            positionClone, ballsPocketedLocal, scoresClone, gameModeLocal, teamIdLocal, foulStateLocal, isTableOpenLocal, teamColorLocal, fourBallCueBallLocal,
            turnStateLocal, networkingManager.cueBallVSynced, networkingManager.cueBallWSynced, colorTurnLocal
#if EIJIS_PUSHOUT
            , pushOutStateLocal
#endif
        };
    }

    public void _LoadInMemoryState(object[] state, int stateIdLocal)
    {
        networkingManager._ForceLoadFromState(
            stateIdLocal,
            (Vector3[])state[0], (uint)state[1], (byte[])state[2], (uint)state[3], (uint)state[4], (uint)state[5], (bool)state[6], (uint)state[7], (uint)state[8],
            (byte)state[9], (Vector3)state[10], (Vector3)state[11], (bool)state[12]
#if EIJIS_PUSHOUT
            , (byte)state[13]
#endif
        );
    }

    public bool _AreInMemoryStatesEqual(object[] a, object[] b)
    {
        Vector3[] posA = (Vector3[])a[0];
        Vector3[] posB = (Vector3[])b[0];
        for (int i = 0; i < ballsP.Length; i++) if (posA[i] != posB[i]) return false;

        byte[] scoresA = (byte[])a[2];
        byte[] scoresB = (byte[])b[2];
        for (byte i = 0; i < fbScoresLocal.Length; i++) if (scoresA[i] != scoresB[i]) return false;

        for (byte i = 0; i < a.Length; i++) if (i != 0 && i != 2 && !a[i].Equals(b[i])) return false;

        return true;
    }

    public bool _IsModerator(VRCPlayerApi player)
    {
        return Array.IndexOf(moderators, player.displayName) != -1;
    }

    public int _GetPlayerSlot(VRCPlayerApi who, int[] playerlist)
    {
        if (who == null) return -1;

        for (int i = 0; i < 4; i++)
        {
            if (playerlist[i] == who.playerId)
            {
                return i;
            }
        }

        return -1;
    }

    public bool _IsPlayer(VRCPlayerApi who)
    {
        if (who == null) return false;
        if (who.isLocal && localPlayerId >= 0) return true;

        for (int i = 0; i < 4; i++)
        {
            if (playerIDsLocal[i] == who.playerId)
            {
                return true;
            }
        }

        return false;
    }

    private bool stringArrayEquals(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool intArrayEquals(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private bool vector3ArrayEquals(Vector3[] a, Vector3[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
#if EIJIS_SNOOKER15REDS

    public static uint SoftwareFallback(uint value)
    {
        const uint c1 = 0x55555555u;
        const uint c2 = 0x33333333u;
        const uint c3 = 0x0F0F0F0Fu;
        const uint c4 = 0x01010101u;

        value -= (value >> 1) & c1;
        value = (value & c2) + ((value >> 2) & c2);
        value = (((value + (value >> 4)) & c3) * c4) >> 24;

        return value;
    }
#endif
#endregion

    #region MiscFunction

    public bool _CanUseTableSkin(string owner, int skin)
    {
        if (tableSkinHook == null) return false;

        tableSkinHook.SetProgramVariable("inOwner", owner);
        tableSkinHook.SetProgramVariable("inSkin", skin);
        tableSkinHook.SendCustomEvent("_CanUseTableSkin");

        return (bool)tableSkinHook.GetProgramVariable("outCanUse");
    }

    //public bool _CanUseCueSkin(int owner, int skin)
    //{
    //    if (cueSkinHook == null) return false;

    //    cueSkinHook.SetProgramVariable("inOwner", owner);
    //    cueSkinHook.SetProgramVariable("inSkin", skin);
    //    cueSkinHook.SendCustomEvent("_CanUseCueSkin");

    //    return (bool)cueSkinHook.GetProgramVariable("outCanUse");
    //}
    public int _CanUseCueSkin(int owner, int skin)   //改了改
    {
        if (tableHook == null) return 0;

        tableHook.SetProgramVariable("inOwner", owner);
        tableHook.SetProgramVariable("inSkin", skin);
        tableHook.SendCustomEvent("_CanUseCueSkin");

        return (int)tableHook.GetProgramVariable("outCanUse");
    }


    public void checkDistanceLoop()
    {
#if EIJIS_ISSUE_FIX
        if (ReferenceEquals(null, Networking.LocalPlayer)) return;
#endif
        if (checkingDistant)
            SendCustomEventDelayedSeconds(nameof(checkDistanceLoop), 1f);
        else
            return;

        checkDistanceLoD();
    }

    public void checkDistanceLoD()
    {
        bool nowDistant = (Vector3.Distance(Networking.LocalPlayer.GetPosition(), transform.position) > LoDDistance) && !noLOD
        && !(networkingManager.gameStateSynced == 2 && Networking.IsOwner(networkingManager.gameObject));
        if (nowDistant == localPlayerDistant) { return; }
        if (isPlayer)
        {
            localPlayerDistant = false;
            return;
        }
        else
        {
            localPlayerDistant = nowDistant;
        }
        if (networkingManager.delayedDeserialization)
        {
            networkingManager.OnDeserialization();
        }
        setLOD();
    }

    private void setLOD()
    {
        for (int i = 0; i < cueControllers.Length; i++) cueControllers[i]._RefreshRenderer();
        balls[0].transform.parent.gameObject.SetActive(!localPlayerDistant);
        debugger.SetActive(!localPlayerDistant);
        menuManager._RefreshLobby();
        graphicsManager._UpdateLOD();
    }

    private string[] getPlayerNames(int[] PlayerIDs)
    {
        if (PlayerIDs == null)
            return null;

        Debug.Log("[SCM] IDlen" + PlayerIDs.Length);
        //返回值数组
        string[] ret = new string[PlayerIDs.Length];

        for (int i = 0; i < PlayerIDs.Length; i++)
        {
            //获取玩家API对象
            VRCPlayerApi Tmp = VRCPlayerApi.GetPlayerById(PlayerIDs[i]);

            //存储玩家名到String数组
            if (Tmp != null)
            {
                if (Tmp.IsValid())
                {
                    ret[i] = Tmp.displayName;
                }
                else
                {
                    ret[i] = "";
                }
            }
        }
        Debug.Log("[SCM] IDDATA" + ret[0] + ";" + ret[1]);
        return ret;
    }

    #endregion

    #region Debugger
    const string LOG_LOW = "<color=\"#ADADAD\">";
    const string LOG_ERR = "<color=\"#B84139\">";
    const string LOG_WARN = "<color=\"#DEC521\">";
    const string LOG_YES = "<color=\"#69D128\">";
    const string LOG_END = "</color>";
#if HT8B_DEBUGGER
    public void _Log(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogYes(string msg)
    {
        _log(LOG_YES + msg + LOG_END);
    }
    public void _LogWarn(string msg)
    {
        _log(LOG_WARN + msg + LOG_END);
    }
    public void _LogError(string msg)
    {
        _log(LOG_ERR + msg + LOG_END);
    }
    public void _LogInfo(string msg)
    {
        _log(LOG_LOW + msg + LOG_END);
    }
    public void _RedrawDebugger()
    {
        redrawDebugger();
    }
#else
public void _Log(string msg) { }
public void _LogYes(string msg) { }
public void _LogInfo(string msg) { }
public void _LogWarn(string msg) { }
public void _LogError(string msg) { }
public void _RedrawDebugger() { }
#endif

    public void _BeginPerf(int id)
    {
        perfStart[id] = Time.realtimeSinceStartup;
    }

    public void _EndPerf(int id)
    {
        perfTimings[id] += Time.realtimeSinceStartup - perfStart[id];
        perfCounters[id]++;
    }

    private void _log(string ln)
    {
#if EIJIS_TABLE_LABEL
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>" + logLabel + "] " + ln);
#else
        Debug.Log("[<color=\"#B5438F\">BilliardsModule</color>] " + ln);
#endif

#if EIJIS_TABLE_LABEL
        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule" + logLabel + "</color>] " + ln + "\n";
#else
        LOG_LINES[LOG_PTR++] = "[<color=\"#B5438F\">BilliardsModule</color>] " + ln + "\n";
#endif
        LOG_LEN++;

        if (LOG_PTR >= LOG_MAX)
        {
            LOG_PTR = 0;
        }

        if (LOG_LEN > LOG_MAX)
        {
            LOG_LEN = LOG_MAX;
        }

        redrawDebugger();
    }

    private void redrawDebugger()
    {
#if EIJIS_TABLE_LABEL
        string output = "BilliardsModule " + VERSION + " [" + logLabel + " ] ";
#else
        string output = "BilliardsModule ";
#endif

        // Add information about game state:
        output += Networking.IsOwner(Networking.LocalPlayer, networkingManager.gameObject) ?
           "<color=\"#95a2b8\">net(</color> <color=\"#4287F5\">OWNER</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">net(</color> <color=\"#678AC2\">RECVR</color> <color=\"#95a2b8\">)</color> ";

        output += isLocalSimulationRunning ?
           "<color=\"#95a2b8\">sim(</color> <color=\"#4287F5\">ACTIVE</color> <color=\"#95a2b8\">)</color> " :
           "<color=\"#95a2b8\">sim(</color> <color=\"#678AC2\">PAUSED</color> <color=\"#95a2b8\">)</color> ";

        VRCPlayerApi currentOwner = Networking.GetOwner(networkingManager.gameObject);
        output += "<color=\"#95a2b8\">owner(</color> <color=\"#4287F5\">" + (Utilities.IsValid(currentOwner) ? currentOwner.displayName + ":" + currentOwner.playerId : "[null]") + "/" + teamIdLocal + "</color> <color=\"#95a2b8\">)</color> ";

        if (currentPhysicsManager)
        {
            output += "Physics: " + (string)currentPhysicsManager.GetProgramVariable("PHYSICSNAME");
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        for (int i = 0; i < PERF_MAX; i++)
        {
            output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (perfCounters[i] > 0 ? perfTimings[i] * 1e6 / perfCounters[i] : 0).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
            // to not average them (see values from this frame)
            // requires changing _EndPerf() to be = instead of +=
            // output += "<color=\"#95a2b8\">" + perfNames[i] + "(</color> " + (/*perfCounters[i] > 0 ? */ perfTimings[i] * 1e6 /* / perfCounters[i] : 0 */).ToString("F2") + "µs <color=\"#95a2b8\">)</color> ";
        }

        output += "\n---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n";

        // Update display 
        for (int i = 0; i < LOG_LEN; i++)
        {
            output += LOG_LINES[(LOG_MAX + LOG_PTR - LOG_LEN + i) % LOG_MAX];
        }

        ltext.text = output;
    }
    #endregion
}
