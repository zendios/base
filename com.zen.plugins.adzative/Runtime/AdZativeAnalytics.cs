using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Base.Ads
{
    /// <summary>Funnel counters for one key (a slot, or an ad unit).</summary>
    public sealed class AdZativeFunnelStats
    {
        public int Requests, Fills, LoadFailures, Expired, Discarded, Opportunities, ReadyAtOpportunity, Shows, Refreshes, Impressions, Clicks, Rewards;
        public int VideoLoads, PodStarts, PodNexts, PodCompleted;
        public long RevenueMicros;
        private readonly List<long> _latencies = new List<long>(64);

        public float FillRate => Requests == 0 ? 0 : (float)Fills / Requests;
        /// <summary>Preload KPI: share of opportunities where an ad was already READY.</summary>
        public float ReadyRate => Opportunities == 0 ? 0 : (float)ReadyAtOpportunity / Opportunities;
        /// <summary>Load → show: loaded ads that were actually shown (1 - waste).</summary>
        public float ShowRate => Fills == 0 ? 0 : (float)(Shows + Refreshes) / Fills;
        public float Ctr => Impressions == 0 ? 0 : (float)Clicks / Impressions;
        public double Ecpm => Impressions == 0 ? 0 : RevenueMicros / 1e6 / Impressions * 1000.0;
        /// <summary>North Star: revenue per 1,000 eligible opportunities.</summary>
        public double RevenuePerMilleOpportunities => Opportunities == 0 ? 0 : RevenueMicros / 1e6 / Opportunities * 1000.0;
        public long LatencyP50 => Percentile(0.50);
        public long LatencyP95 => Percentile(0.95);

        internal void AddLatency(long ms)
        {
            if (_latencies.Count >= 256) _latencies.RemoveAt(0);
            _latencies.Add(ms);
        }

        private long Percentile(double p)
        {
            if (_latencies.Count == 0) return 0;
            var sorted = _latencies.OrderBy(v => v).ToList();
            int idx = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(idx, sorted.Count - 1))];
        }
    }

    /// <summary>
    /// Two views of the same funnel: per SLOT (opportunity → show → impression → paid) and per AD UNIT
    /// (request → fill → shown → paid), so a slot with two IDs shows which ID earns and which one wastes.
    /// Forward AdZativeSDK.OnEvent to Firebase/BI for the server-side version.
    /// </summary>
    public static class AdZativeAnalytics
    {
        private static readonly Dictionary<string, AdZativeFunnelStats> BySlot = new Dictionary<string, AdZativeFunnelStats>();
        private static readonly Dictionary<string, AdZativeFunnelStats> ByAdUnit = new Dictionary<string, AdZativeFunnelStats>();

        public static AdZativeFunnelStats Slot(string slotId) => Get(BySlot, slotId);
        public static AdZativeFunnelStats AdUnit(string adUnitId) => Get(ByAdUnit, adUnitId);

        private static AdZativeFunnelStats Get(Dictionary<string, AdZativeFunnelStats> d, string k)
        {
            if (!d.TryGetValue(k, out var s)) d[k] = s = new AdZativeFunnelStats();
            return s;
        }

        internal static void Track(in AdZativeEvent e)
        {
            var p = e.Payload;
            var unit = string.IsNullOrEmpty(p.ad_unit) ? null : AdUnit(p.ad_unit);
            var slot = string.IsNullOrEmpty(e.SlotId) ? null : Slot(e.SlotId);
            switch (e.Name)
            {
                case "load_request": if (unit != null) unit.Requests++; break;
                case "loaded": if (unit != null) { unit.Fills++; unit.AddLatency(p.latency_ms); if (p.media_type == "VIDEO") unit.VideoLoads++; } break;
                case "discarded": if (unit != null) unit.Discarded++; if (slot != null) slot.Discarded++; break;
                case "replaced": if (slot != null) slot.Refreshes++; break;
                case "pod_started": if (slot != null) slot.PodStarts++; break;
                case "pod_next": if (slot != null) slot.PodNexts++; break;
                case "closed": if (slot != null && p.pod_size > 1 && p.ads_watched >= p.pod_size) slot.PodCompleted++; break;
                case "load_failed": if (unit != null) unit.LoadFailures++; break;
                case "expired": if (unit != null) unit.Expired++; break;
                case "opportunity": if (slot != null) { slot.Opportunities++; if (p.ready) slot.ReadyAtOpportunity++; } break;
                case "shown": if (slot != null) slot.Shows++; if (unit != null) unit.Shows++; break;
                case "refreshed": if (slot != null) slot.Refreshes++; if (unit != null) unit.Refreshes++; break;
                case "impression": if (slot != null) slot.Impressions++; if (unit != null) unit.Impressions++; break;
                case "clicked": if (slot != null) slot.Clicks++; if (unit != null) unit.Clicks++; break;
                case "paid":
                    if (slot != null) slot.RevenueMicros += p.value_micros;
                    if (unit != null) unit.RevenueMicros += p.value_micros;
                    break;
                case "reward_earned": if (slot != null) slot.Rewards++; break;
            }
        }

        public static string Report()
        {
            var sb = new StringBuilder("— slots —\n");
            foreach (var kv in BySlot)
            {
                var s = kv.Value;
                sb.AppendLine($"{kv.Key}: opp={s.Opportunities} ready={s.ReadyRate:P0} imp={s.Impressions} refresh={s.Refreshes} " +
                              (s.PodStarts > 0 ? $"pods={s.PodStarts} next={s.PodNexts} full={s.PodCompleted} " : "") +
                              $"ctr={s.Ctr:P1} eCPM=${s.Ecpm:F2} rev/1k opp=${s.RevenuePerMilleOpportunities:F2}");
            }
            sb.AppendLine("— ad units —");
            foreach (var kv in ByAdUnit)
            {
                var s = kv.Value;
                string id = kv.Key.Length > 14 ? "…" + kv.Key.Substring(kv.Key.Length - 12) : kv.Key;
                sb.AppendLine($"{id}: req={s.Requests} fill={s.FillRate:P0} show={s.ShowRate:P0} expired={s.Expired} discarded={s.Discarded} video={s.VideoLoads} " +
                              $"eCPM=${s.Ecpm:F2} lat p50={s.LatencyP50}ms p95={s.LatencyP95}ms");
            }
            return sb.ToString();
        }
    }
}
