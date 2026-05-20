using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModelConfiguration))]
public class ModelConfigurationEditor : Editor
{
    bool bShowCollisionModel = true;
    bool bShowNpcPockets = false;
    static GUIStyle styleHeader;
    static GUIStyle styleError;
    static GUIStyle styleWarning;
    static bool gui_resource_ready = false;

    CollisionVisualizer cdata_displayTarget;
    private static void DrawError(string szError, GUIStyle style)
    {
        GUILayout.BeginVertical("GroupBox");
        GUILayout.Label(szError, style);
        GUILayout.EndVertical();
    }

    private static bool Material_ht8b_supports(ref Material mat)
    {
        bool isFullSupport = true;

        if (!mat.HasProperty("_EmissionColor"))
        {
            DrawError($"[!] Shader '{mat.shader.name}' does not have property: _EmissionColor", styleError);
            isFullSupport = false;
        }

        if (!mat.HasProperty("_Color"))
        {
            DrawError($"Shader {mat.shader.name} does not have property: _Color", styleWarning);
        }

        return isFullSupport;
    }

    private static void Ht8bUIGroup(string szHeader)
    {
        GUILayout.BeginVertical("HelpBox");
        GUILayout.Label(szHeader, styleHeader);
    }

    private static bool Ht8bUIGroupMitButton(string szHeader, string szButton)
    {
        GUILayout.BeginVertical("HelpBox");
        GUILayout.BeginHorizontal();
        GUILayout.Label(szHeader, styleHeader);
        bool b = GUILayout.Button(szButton);
        GUILayout.EndHorizontal();

        return b;
    }

    private static void Ht8bUIGroupEnd()
    {
        GUILayout.EndVertical();
    }

    private static void gui_resource_init()
    {
        styleHeader = new GUIStyle()
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        styleWarning = new GUIStyle()
        {
            wordWrap = true
        };

        styleError = new GUIStyle()
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        gui_resource_ready = true;
    }

    public override void OnInspectorGUI()
    {
        if (!gui_resource_ready)
        {
            gui_resource_init();
        }
        ModelConfiguration _editor = (ModelConfiguration)target;

        base.DrawDefaultInspector();

        ModelData data = _editor.data;

        if (data != null)
        {
            Ht8bUIGroup("Collision info");

            if (!cdata_displayTarget)
            {
                Transform table = null;
                if (_editor.transform.parent)
                    if (_editor.transform.parent.parent)
                        table = _editor.transform.parent.parent.Find("intl.balls");
                if (table)
                {
                    Transform refiner = table.Find("__table_refiner__");
                    if (refiner)
                    {
                        cdata_displayTarget = _editor.transform.parent.parent.Find("intl.balls").Find("__table_refiner__").gameObject.GetComponent<CollisionVisualizer>();
                    }
                }
            }
            if (cdata_displayTarget == null) { return; }

            this.bShowCollisionModel = EditorGUILayout.Toggle("Draw collision model", this.cdata_displayTarget.gameObject.activeSelf);
            this.cdata_displayTarget.gameObject.SetActive(this.bShowCollisionModel);

            bShowNpcPockets = EditorGUILayout.Toggle("Show NPC pocket targets", bShowNpcPockets);
            if (bShowNpcPockets)
                this.cdata_displayTarget.gameObject.SetActive(false);

            Ht8bUIGroupEnd();
            sendValuesToVisualizerAndUpdateView(data);
        }
    }
    float lastUpdate;
    void sendValuesToVisualizerAndUpdateView(ModelData data)
    {
        // Same conversions as in BilliardsModule.setTableModel()
        this.cdata_displayTarget.tableWidth = data.tableWidth * .5f;
        this.cdata_displayTarget.tableHeight = data.tableHeight * .5f;
        this.cdata_displayTarget.k_BALL_RADIUS = (data.bs_BallDiameter * .5f) / 1000f;
        this.cdata_displayTarget.pocketWidthCorner = data.pocketWidthCorner;
        this.cdata_displayTarget.pocketHeightCorner = data.pocketHeightCorner;
        this.cdata_displayTarget.pocketRadiusSide = data.pocketRadiusSide;
        this.cdata_displayTarget.pocketDepthSide = data.pocketDepthSide;
        this.cdata_displayTarget.cushionRadius = data.cushionRadius;
        this.cdata_displayTarget.pocketInnerRadiusCorner = data.pocketInnerRadiusCorner;
        this.cdata_displayTarget.pocketInnerRadiusSide = data.pocketInnerRadiusSide;
        this.cdata_displayTarget.cornerPocket = data.cornerPocket;
        this.cdata_displayTarget.sidePocket = data.sidePocket;
        this.cdata_displayTarget.facingAngleCorner = data.facingAngleCorner;
        this.cdata_displayTarget.facingAngleSide = data.facingAngleSide;
        this.cdata_displayTarget.k_RAIL_HEIGHT_UPPER = data.railHeightUpper;
        this.cdata_displayTarget.k_RAIL_HEIGHT_LOWER = data.railHeightLower;
        this.cdata_displayTarget.k_RAIL_DEPTH_WIDTH = data.railDepthWidth;
        this.cdata_displayTarget.k_RAIL_DEPTH_HEIGHT = data.railDepthHeight;

        this.cdata_displayTarget.baulkLine = -((data.tableWidth * .5f) - data.baulkLine);
        this.cdata_displayTarget.blackSpot = (data.tableWidth * .5f) - data.blackSpot;
        this.cdata_displayTarget.semiCircleRadius = data.semiCircleRadius;
        this.cdata_displayTarget.pinkSpot = (data.tableWidth * .5f) - data.pinkSpot;

        Transform table_artwork = data.transform.Find("table_artwork");
        Transform tableSurface = table_artwork.transform.Find(".TABLE_SURFACE");
        if (tableSurface)
        { this.cdata_displayTarget.table_Surface = tableSurface; }
        SceneView.RepaintAll();
    }

    // Draw NPC pocket target positions in Scene view
    void OnSceneGUI()
    {
        if (!bShowNpcPockets) return;
        ModelData data = ((ModelConfiguration)target).data;
        if (data == null) return;

        Vector3 corner = data.cornerPocket;
        Vector3 side = data.sidePocket;
        float cornerInnerR = data.pocketInnerRadiusCorner;
        float sideInnerR = data.pocketInnerRadiusSide;
        Transform tableXform = data.transform;
        // Find table surface Y from table_mesh child
        Transform tableMesh = tableXform.Find("table_artwork")?.Find("table_mesh");
        float surfaceY = tableMesh != null ? tableMesh.position.y : 0.8606015f;

        // NPC pocket targets: offset toward OPPOSITE side pocket (same as PracticeManager._InitPockets)
        Vector3[] localTargets = new Vector3[6];
        // C0 (top-right +x,+z) → toward S5 (bottom 0,-z)
        localTargets[0] = corner + (new Vector3(0, 0, -side.z) - corner).normalized * cornerInnerR;
        // C1 (bottom-right +x,-z) → toward S4 (top 0,+z)
        Vector3 c1 = new Vector3(corner.x, corner.y, -corner.z);
        localTargets[1] = c1 + (new Vector3(0, 0, side.z) - c1).normalized * cornerInnerR;
        // C2 (top-left -x,+z) → toward S5 (bottom 0,-z)
        Vector3 c2 = new Vector3(-corner.x, corner.y, corner.z);
        localTargets[2] = c2 + (new Vector3(0, 0, -side.z) - c2).normalized * cornerInnerR;
        // C3 (bottom-left -x,-z) → toward S4 (top 0,+z)
        Vector3 c3 = new Vector3(-corner.x, corner.y, -corner.z);
        localTargets[3] = c3 + (new Vector3(0, 0, side.z) - c3).normalized * cornerInnerR;
        // S4 (top 0,+z) → toward center
        localTargets[4] = side + (Vector3.zero - side).normalized * sideInnerR;
        // S5 (bottom 0,-z) → toward center
        Vector3 s5 = new Vector3(side.x, side.y, -side.z);
        localTargets[5] = s5 + (Vector3.zero - s5).normalized * sideInnerR;

        // Apply corner-to-side offset (opposite side pocket)
        Vector3[] adjusted = new Vector3[6];
        for (int i = 0; i < 6; i++) adjusted[i] = localTargets[i];
        adjusted[0] = Vector3.Lerp(localTargets[0], localTargets[5], data.cornerToSideOffset); // C0(+x,+z)→S5(0,-z)
        adjusted[1] = Vector3.Lerp(localTargets[1], localTargets[4], data.cornerToSideOffset); // C1(+x,-z)→S4(0,+z)
        adjusted[2] = Vector3.Lerp(localTargets[2], localTargets[5], data.cornerToSideOffset); // C2(-x,+z)→S5(0,-z)
        adjusted[3] = Vector3.Lerp(localTargets[3], localTargets[4], data.cornerToSideOffset); // C3(-x,-z)→S4(0,+z)

        // Convert all local positions to world space, with table surface Y
        Vector3[] worldTargets = new Vector3[6];
        Vector3[] worldAdjusted = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = tableXform.TransformPoint(localTargets[i]);
            worldTargets[i] = new Vector3(p.x, surfaceY, p.z);
            Vector3 a = tableXform.TransformPoint(adjusted[i]);
            worldAdjusted[i] = new Vector3(a.x, surfaceY, a.z);
        }

        Vector3[] worldCorners = new Vector3[4];
        Vector3 w0 = tableXform.TransformPoint(corner);
        worldCorners[0] = new Vector3(w0.x, surfaceY, w0.z);
        Vector3 w1 = tableXform.TransformPoint(new Vector3(corner.x, corner.y, -corner.z));
        worldCorners[1] = new Vector3(w1.x, surfaceY, w1.z);
        Vector3 w2 = tableXform.TransformPoint(new Vector3(-corner.x, corner.y, corner.z));
        worldCorners[2] = new Vector3(w2.x, surfaceY, w2.z);
        Vector3 w3 = tableXform.TransformPoint(new Vector3(-corner.x, corner.y, -corner.z));
        worldCorners[3] = new Vector3(w3.x, surfaceY, w3.z);
        Vector3 ws1 = tableXform.TransformPoint(side);
        Vector3 worldSide1 = new Vector3(ws1.x, surfaceY, ws1.z);
        Vector3 ws2 = tableXform.TransformPoint(new Vector3(side.x, side.y, -side.z));
        Vector3 worldSide2 = new Vector3(ws2.x, surfaceY, ws2.z);

        // White = pocket centers
        Handles.color = Color.white;
        for (int i = 0; i < 4; i++)
        {
            Handles.DrawSolidDisc(worldCorners[i], Vector3.up, 0.02f);
            Handles.Label(worldCorners[i] + Vector3.up * 0.02f, $"C{i}");
        }
        Handles.DrawSolidDisc(worldSide1, Vector3.up, 0.02f);
        Handles.Label(worldSide1 + Vector3.up * 0.02f, "S4");
        Handles.DrawSolidDisc(worldSide2, Vector3.up, 0.02f);
        Handles.Label(worldSide2 + Vector3.up * 0.02f, "S5");

        // Green = original T-points
        Handles.color = Color.green;
        for (int i = 0; i < 6; i++)
        {
            Handles.DrawSolidDisc(worldTargets[i], Vector3.up, 0.020f);
            Handles.Label(worldTargets[i] + Vector3.up * 0.020f, $"T{i}");
        }

        // Cyan = adjusted T-points
        Handles.color = Color.cyan;
        for (int i = 0; i < 6; i++)
        {
            Handles.DrawSolidDisc(worldAdjusted[i], Vector3.up, 0.025f);
            Handles.Label(worldAdjusted[i] + Vector3.up * 0.025f, $"T{i}'");
        }

        // Yellow lines: pocket center → original T-point
        Handles.color = Color.yellow;
        for (int i = 0; i < 4; i++)
            Handles.DrawLine(worldCorners[i], worldTargets[i]);
        Handles.DrawLine(worldSide1, worldTargets[4]);
        Handles.DrawLine(worldSide2, worldTargets[5]);

        SceneView.RepaintAll();
    }
    void OnEnable()
    {
        ModelConfiguration _editor = (ModelConfiguration)target;
        if (!cdata_displayTarget)
        {
            if (_editor.transform.parent)
                if (_editor.transform.parent.parent)
                    cdata_displayTarget = _editor.transform.parent.parent.Find("intl.balls").Find("__table_refiner__").gameObject.GetComponent<CollisionVisualizer>();
            if (cdata_displayTarget == null)
            {
                return;
            }
        }
        ModelData data = _editor.data;
        cdata_displayTarget.gameObject.SetActive(this.bShowCollisionModel);
        cdata_displayTarget.drawTable();
        sendValuesToVisualizerAndUpdateView(data);
    }
    void OnDisable()
    {
        if (cdata_displayTarget)
        {
            cdata_displayTarget.gameObject.SetActive(false);
        }
    }
}