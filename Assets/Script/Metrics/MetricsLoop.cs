using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MetricsLoop : MonoBehaviour
{
    private static MetricsLoop _instance;
    private int _nextId = 1;

    private readonly List<JitterSession> _sessions = new List<JitterSession>(8);

    private sealed class JitterSession
    {
        public int Id;
        public string Name;
        public MetricsContext Context;
        public DateTime StartTime;
        public List<float> Samples = new List<float>(256);
        public bool Active = true;
    }

    public static MetricsLoop Ensure()
    {
        if (_instance == null)
        {
            var go = new GameObject("MetricsLoop");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MetricsLoop>();
        }
        return _instance;
    }

    public int StartJitter(string name, MetricsContext ctx)
    {
        var s = new JitterSession
        {
            Id = _nextId++,
            Name = name,
            Context = ctx,
            StartTime = DateTime.Now,
        };
        _sessions.Add(s);
        return s.Id;
    }

    public void EndJitter(int id)
    {
        var idx = _sessions.FindIndex(x => x.Id == id);
        if (idx < 0) return;
        var s = _sessions[idx];
        s.Active = false;
        _sessions.RemoveAt(idx);

        int count = s.Samples.Count;
        double avg = 0, p95 = 0, max = 0;
        if (count > 0)
        {
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                var v = (double)s.Samples[i];
                sum += v;
                if (v > max) max = v;
            }
            avg = sum / count;
            var copy = new List<float>(s.Samples);
            copy.Sort();
            int k = Mathf.Clamp(Mathf.CeilToInt((copy.Count - 1) * 0.95f), 0, copy.Count - 1);
            p95 = copy[k];
        }

        var ev = new JitterCompletedEvent
        {
            Name = s.Name,
            Count = count,
            AvgMs = avg,
            P95Ms = p95,
            MaxMs = max,
            StartTime = s.StartTime,
            EndTime = DateTime.Now,
            Context = s.Context,
        };
        MetricsHub.Instance.RecordJitter(ev);
    }

    private void Update()
    {
        if (_sessions.Count == 0) return;
        float dtMs = Time.unscaledDeltaTime * 1000f;
        for (int i = 0; i < _sessions.Count; i++)
        {
            var s = _sessions[i];
            if (!s.Active) continue;
            s.Samples.Add(dtMs);
        }
    }
}
