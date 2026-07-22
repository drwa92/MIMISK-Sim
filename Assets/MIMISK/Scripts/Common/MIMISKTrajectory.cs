using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public struct MIMISKTrajectoryPoint
{
    public float time;
    public Vector3 position;
    public Vector3 velocity;
    public float yawRad;
    public bool hasVelocity;
    public bool hasYaw;
}

[Serializable]
public class MIMISKTrajectory
{
    public string name = "trajectory";
    public List<MIMISKTrajectoryPoint> points = new List<MIMISKTrajectoryPoint>();

    public bool IsValid
    {
        get { return points != null && points.Count > 0; }
    }

    public float Duration
    {
        get
        {
            if (!IsValid)
            {
                return 0.0f;
            }

            return Mathf.Max(0.0f, points[points.Count - 1].time - points[0].time);
        }
    }

    public static MIMISKTrajectory FromText(string text, string trajectoryName)
    {
        MIMISKTrajectory trajectory = new MIMISKTrajectory();
        trajectory.name = string.IsNullOrEmpty(trajectoryName) ? "trajectory" : trajectoryName;

        if (string.IsNullOrEmpty(text))
        {
            return trajectory;
        }

        string[] lines =
            text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("#"))
            {
                continue;
            }

            string[] tokens =
                line.Split(new[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 4)
            {
                continue;
            }

            float[] values =
                new float[tokens.Length];

            bool allNumeric = true;

            for (int k = 0; k < tokens.Length; k++)
            {
                if (!float.TryParse(
                        tokens[k],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out values[k]))
                {
                    allNumeric = false;
                    break;
                }
            }

            if (!allNumeric)
            {
                // Header row.
                continue;
            }

            MIMISKTrajectoryPoint p =
                new MIMISKTrajectoryPoint();

            p.time = values[0];
            p.position = new Vector3(values[1], values[2], values[3]);
            p.velocity = Vector3.zero;
            p.yawRad = 0.0f;
            p.hasVelocity = false;
            p.hasYaw = false;

            if (values.Length >= 7)
            {
                p.velocity = new Vector3(values[4], values[5], values[6]);
                p.hasVelocity = true;
            }

            if (values.Length >= 8)
            {
                p.yawRad = values[7];
                p.hasYaw = true;
            }

            trajectory.points.Add(p);
        }

        trajectory.points.Sort(
            delegate (MIMISKTrajectoryPoint a, MIMISKTrajectoryPoint b)
            {
                return a.time.CompareTo(b.time);
            }
        );

        return trajectory;
    }

    public MIMISKTrajectoryPoint Evaluate(float missionTime)
    {
        if (!IsValid)
        {
            return new MIMISKTrajectoryPoint();
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        if (missionTime <= points[0].time)
        {
            return points[0];
        }

        int last = points.Count - 1;

        if (missionTime >= points[last].time)
        {
            return points[last];
        }

        for (int i = 0; i < last; i++)
        {
            MIMISKTrajectoryPoint a = points[i];
            MIMISKTrajectoryPoint b = points[i + 1];

            if (missionTime >= a.time && missionTime <= b.time)
            {
                float dt = Mathf.Max(0.0001f, b.time - a.time);
                float u = Mathf.Clamp01((missionTime - a.time) / dt);

                MIMISKTrajectoryPoint r =
                    new MIMISKTrajectoryPoint();

                r.time = missionTime;
                r.position = Vector3.Lerp(a.position, b.position, u);
                r.velocity = Vector3.Lerp(a.velocity, b.velocity, u);
                r.hasVelocity = a.hasVelocity || b.hasVelocity;

                r.yawRad =
                    Mathf.LerpAngle(
                        a.yawRad * Mathf.Rad2Deg,
                        b.yawRad * Mathf.Rad2Deg,
                        u
                    ) * Mathf.Deg2Rad;

                r.hasYaw = a.hasYaw || b.hasYaw;

                return r;
            }
        }

        return points[last];
    }
}
