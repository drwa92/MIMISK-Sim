using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MIMISKDashboardTrajectoryMap : MaskableGraphic
{
    [Header("Ground Truth References")]
    public Transform droneRoot;
    public Transform miniRovRoot;
    public MonoBehaviour lowLevelTether;

    [Header("Auto Find")]
    public bool autoFindReferences = true;
    public float autoFindPeriodS = 1.0f;
    public string droneObjectName = "Drone";
    public string miniRovObjectName = "MiniROV";

    [Header("Map")]
    public bool mapEnabled = true;
    public bool autoCenter = true;
    public float mapRangeM = 10.0f;
    public float gridSpacingM = 1.0f;

    [Header("Trail Sampling")]
    public bool drawTrails = true;
    public float samplePeriodS = 0.15f;
    public float minSampleDistanceM = 0.02f;
    public int maxTrailSamples = 900;

    [Header("Visual")]
    public Color backgroundColor = new Color(0.0f, 0.035f, 0.045f, 0.98f);
    public Color gridColor = new Color(0.18f, 0.42f, 0.46f, 0.50f);
    public Color axisColor = new Color(0.45f, 0.85f, 0.95f, 0.60f);

    public Color droneTrailColor = new Color(0.20f, 1.0f, 0.25f, 1.0f);
    public Color rovTrailColor = new Color(1.0f, 0.84f, 0.05f, 1.0f);
    public Color tetherColor = new Color(1.0f, 0.68f, 0.05f, 1.0f);

    public Color droneMarkerColor = new Color(0.10f, 1.0f, 0.32f, 1.0f);
    public Color rovMarkerColor = new Color(1.0f, 0.84f, 0.05f, 1.0f);

    public float tetherWidthPx = 2.5f;
    public float trailWidthPx = 2.5f;
    public float markerRadiusPx = 7.0f;
    public float droneTriangleSizePx = 12.0f;

    [Header("Legacy Compatibility - Ignored")]
    public float plannedPathWidthPx = 2.0f;

    [Header("Runtime")]
    public Vector3 mapCenterWorld;
    public Vector3 droneWorld;
    public Vector3 miniRovWorld;
    public Vector3 tetherStartWorld;
    public Vector3 tetherEndWorld;

    public float droneRovHorizontalDistanceM;
    public int droneTrailCount;
    public int rovTrailCount;
    public string mapStatus = "idle";

    private readonly List<Vector3> droneTrail = new List<Vector3>();
    private readonly List<Vector3> rovTrail = new List<Vector3>();

    private float sampleTimerS;
    private float autoFindTimerS;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        AutoFindReferencesNow();
    }

    private void Update()
    {
        if (!mapEnabled)
        {
            return;
        }

        if (autoFindReferences)
        {
            autoFindTimerS += Time.unscaledDeltaTime;

            if (autoFindTimerS >= autoFindPeriodS)
            {
                autoFindTimerS = 0.0f;
                AutoFindReferencesNow();
            }
        }

        RefreshRuntimeValues();
        SampleTrails();
        SetVerticesDirty();
    }

    [ContextMenu("Auto Find References Now")]
    public void AutoFindReferencesNow()
    {
        if (droneRoot == null)
        {
            droneRoot = FindSceneTransform(droneObjectName);
        }

        if (miniRovRoot == null)
        {
            miniRovRoot = FindSceneTransform(miniRovObjectName);
        }

        if (lowLevelTether == null)
        {
            lowLevelTether = FindBehaviourByTypeName("MIMISKDroneCoreTetherManager");
        }

        mapStatus =
            "refs: drone=" + NameOf(droneRoot) +
            ", rov=" + NameOf(miniRovRoot) +
            ", tether=" + NameOf(lowLevelTether);
    }

    [ContextMenu("Clear Trails")]
    public void ClearTrails()
    {
        droneTrail.Clear();
        rovTrail.Clear();
        droneTrailCount = 0;
        rovTrailCount = 0;
        SetVerticesDirty();
    }

    public void SetReferences(
        Transform drone,
        Transform rov,
        MonoBehaviour tether)
    {
        if (drone != null)
        {
            droneRoot = drone;
        }

        if (rov != null)
        {
            miniRovRoot = rov;
        }

        if (tether != null)
        {
            lowLevelTether = tether;
        }

        AutoFindReferencesNow();
    }

    // Backward-compatible overload in case an older gadget script still calls four arguments.
    public void SetReferences(
        Transform drone,
        Transform rov,
        MonoBehaviour tether,
        MonoBehaviour ignoredRovController)
    {
        SetReferences(drone, rov, tether);
    }

    [ContextMenu("Refresh Map Now")]
    public void RefreshMapNow()
    {
        AutoFindReferencesNow();
        RefreshRuntimeValues();
        SetVerticesDirty();
    }

    private void RefreshRuntimeValues()
    {
        if (droneRoot != null)
        {
            droneWorld = droneRoot.position;
        }

        if (miniRovRoot != null)
        {
            miniRovWorld = miniRovRoot.position;
        }

        Vector3 fallbackStart =
            droneRoot != null
                ? droneRoot.position
                : Vector3.zero;

        Vector3 fallbackEnd =
            miniRovRoot != null
                ? miniRovRoot.position
                : fallbackStart;

        tetherStartWorld =
            ReadVector3(lowLevelTether, "tetherStartWorld", fallbackStart);

        tetherEndWorld =
            ReadVector3(lowLevelTether, "tetherEndWorld", fallbackEnd);

        if (droneRoot != null && miniRovRoot != null)
        {
            Vector2 a =
                new Vector2(droneWorld.x, droneWorld.z);

            Vector2 b =
                new Vector2(miniRovWorld.x, miniRovWorld.z);

            droneRovHorizontalDistanceM =
                Vector2.Distance(a, b);
        }

        if (autoCenter)
        {
            if (droneRoot != null && miniRovRoot != null)
            {
                mapCenterWorld =
                    0.5f * (droneWorld + miniRovWorld);
            }
            else if (droneRoot != null)
            {
                mapCenterWorld =
                    droneWorld;
            }
            else if (miniRovRoot != null)
            {
                mapCenterWorld =
                    miniRovWorld;
            }
        }

        mapStatus =
            "GT map: drone=" + NameOf(droneRoot) +
            ", rov=" + NameOf(miniRovRoot) +
            ", d=" + droneRovHorizontalDistanceM.ToString("F2") + " m";
    }

    private void SampleTrails()
    {
        if (!drawTrails)
        {
            return;
        }

        sampleTimerS += Time.unscaledDeltaTime;

        if (sampleTimerS < samplePeriodS)
        {
            return;
        }

        sampleTimerS = 0.0f;

        if (droneRoot != null)
        {
            AddTrailSample(droneTrail, droneWorld);
        }

        if (miniRovRoot != null)
        {
            AddTrailSample(rovTrail, miniRovWorld);
        }

        droneTrailCount =
            droneTrail.Count;

        rovTrailCount =
            rovTrail.Count;
    }

    private void AddTrailSample(List<Vector3> trail, Vector3 p)
    {
        if (trail.Count > 0)
        {
            float d =
                Vector3.Distance(
                    trail[trail.Count - 1],
                    p
                );

            if (d < minSampleDistanceM)
            {
                return;
            }
        }

        trail.Add(p);

        while (trail.Count > maxTrailSamples)
        {
            trail.RemoveAt(0);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (!mapEnabled)
        {
            return;
        }

        RefreshRuntimeValues();

        Rect r =
            GetPixelAdjustedRect();

        AddRect(vh, r, backgroundColor);
        DrawGrid(vh, r);

        if (drawTrails)
        {
            DrawTrail(vh, r, droneTrail, droneTrailColor, trailWidthPx);
            DrawTrail(vh, r, rovTrail, rovTrailColor, trailWidthPx);
        }

        DrawTether(vh, r);
        DrawMarkers(vh, r);
    }

    private void DrawGrid(VertexHelper vh, Rect r)
    {
        float pxPerM =
            PixelsPerMeter(r);

        if (pxPerM <= 0.001f)
        {
            return;
        }

        int count =
            Mathf.CeilToInt(mapRangeM / Mathf.Max(0.1f, gridSpacingM));

        for (int i = -count; i <= count; i++)
        {
            float offset =
                i * gridSpacingM * pxPerM;

            Color c =
                i == 0
                    ? axisColor
                    : gridColor;

            AddLine(
                vh,
                new Vector2(r.center.x + offset, r.yMin),
                new Vector2(r.center.x + offset, r.yMax),
                i == 0 ? 1.8f : 1.0f,
                c
            );

            AddLine(
                vh,
                new Vector2(r.xMin, r.center.y + offset),
                new Vector2(r.xMax, r.center.y + offset),
                i == 0 ? 1.8f : 1.0f,
                c
            );
        }
    }

    private void DrawTether(VertexHelper vh, Rect r)
    {
        Vector3 start =
            lowLevelTether != null
                ? tetherStartWorld
                : droneWorld;

        Vector3 end =
            lowLevelTether != null
                ? tetherEndWorld
                : miniRovWorld;

        if (start == Vector3.zero && end == Vector3.zero)
        {
            return;
        }

        AddLine(
            vh,
            WorldToMapLocal(r, start),
            WorldToMapLocal(r, end),
            tetherWidthPx,
            tetherColor
        );
    }

    private void DrawTrail(
        VertexHelper vh,
        Rect r,
        List<Vector3> trail,
        Color c,
        float width)
    {
        if (trail == null || trail.Count < 2)
        {
            return;
        }

        for (int i = 1; i < trail.Count; i++)
        {
            AddLine(
                vh,
                WorldToMapLocal(r, trail[i - 1]),
                WorldToMapLocal(r, trail[i]),
                width,
                c
            );
        }
    }

    private void DrawMarkers(VertexHelper vh, Rect r)
    {
        if (droneRoot != null)
        {
            DrawTriangle(
                vh,
                WorldToMapLocal(r, droneWorld),
                droneTriangleSizePx,
                droneRoot.eulerAngles.y,
                droneMarkerColor
            );
        }

        if (miniRovRoot != null)
        {
            DrawCircle(
                vh,
                WorldToMapLocal(r, miniRovWorld),
                markerRadiusPx,
                rovMarkerColor
            );
        }
    }

    private Vector2 WorldToMapLocal(Rect r, Vector3 p)
    {
        float pxPerM =
            PixelsPerMeter(r);

        float dx =
            p.x - mapCenterWorld.x;

        float dz =
            p.z - mapCenterWorld.z;

        return
            new Vector2(
                r.center.x + dx * pxPerM,
                r.center.y + dz * pxPerM
            );
    }

    private float PixelsPerMeter(Rect r)
    {
        return
            Mathf.Min(r.width, r.height) /
            Mathf.Max(0.1f, 2.0f * mapRangeM);
    }

    private void AddRect(VertexHelper vh, Rect r, Color c)
    {
        int idx =
            vh.currentVertCount;

        UIVertex v =
            UIVertex.simpleVert;

        v.color = c;

        v.position = new Vector3(r.xMin, r.yMin);
        vh.AddVert(v);

        v.position = new Vector3(r.xMin, r.yMax);
        vh.AddVert(v);

        v.position = new Vector3(r.xMax, r.yMax);
        vh.AddVert(v);

        v.position = new Vector3(r.xMax, r.yMin);
        vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    private void AddLine(
        VertexHelper vh,
        Vector2 a,
        Vector2 b,
        float width,
        Color c)
    {
        Vector2 d =
            b - a;

        if (d.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector2 n =
            new Vector2(-d.y, d.x).normalized *
            width *
            0.5f;

        int idx =
            vh.currentVertCount;

        UIVertex v =
            UIVertex.simpleVert;

        v.color = c;

        v.position = a + n;
        vh.AddVert(v);

        v.position = a - n;
        vh.AddVert(v);

        v.position = b - n;
        vh.AddVert(v);

        v.position = b + n;
        vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    private void DrawCircle(
        VertexHelper vh,
        Vector2 center,
        float radius,
        Color c)
    {
        int segments =
            24;

        int start =
            vh.currentVertCount;

        UIVertex v =
            UIVertex.simpleVert;

        v.color = c;
        v.position = center;
        vh.AddVert(v);

        for (int i = 0; i <= segments; i++)
        {
            float a =
                2.0f * Mathf.PI * i / segments;

            v.position =
                center +
                new Vector2(
                    Mathf.Cos(a) * radius,
                    Mathf.Sin(a) * radius
                );

            vh.AddVert(v);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(start, start + i, start + i + 1);
        }
    }

    private void DrawTriangle(
        VertexHelper vh,
        Vector2 center,
        float size,
        float yawDeg,
        Color c)
    {
        float yaw =
            -yawDeg * Mathf.Deg2Rad;

        Vector2 f =
            new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));

        Vector2 right =
            new Vector2(f.y, -f.x);

        Vector2 p0 =
            center + f * size;

        Vector2 p1 =
            center - f * size * 0.75f + right * size * 0.60f;

        Vector2 p2 =
            center - f * size * 0.75f - right * size * 0.60f;

        int idx =
            vh.currentVertCount;

        UIVertex v =
            UIVertex.simpleVert;

        v.color = c;

        v.position = p0;
        vh.AddVert(v);

        v.position = p1;
        vh.AddVert(v);

        v.position = p2;
        vh.AddVert(v);

        vh.AddTriangle(idx, idx + 1, idx + 2);
    }

    private Vector3 ReadVector3(
        object target,
        string member,
        Vector3 fallback)
    {
        object value =
            GetMember(target, member);

        if (value is Vector3)
        {
            return (Vector3)value;
        }

        return fallback;
    }

    private object GetMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        Type type =
            target.GetType();

        FieldInfo field =
            type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo prop =
            type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (prop != null && prop.CanRead)
        {
            return prop.GetValue(target, null);
        }

        return null;
    }

    private Transform FindSceneTransform(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] transforms =
            Resources.FindObjectsOfTypeAll<Transform>();

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t =
                transforms[i];

            if (t == null ||
                t.gameObject == null ||
                !t.gameObject.scene.IsValid())
            {
                continue;
            }

            if (t.gameObject.name == objectName)
            {
                return t;
            }
        }

        return null;
    }

    private MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b =
                behaviours[i];

            if (b == null ||
                b.gameObject == null ||
                !b.gameObject.scene.IsValid())
            {
                continue;
            }

            if (b.GetType().Name == typeName)
            {
                return b;
            }
        }

        return null;
    }

    private string NameOf(UnityEngine.Object o)
    {
        return o != null ? o.name : "missing";
    }
}
