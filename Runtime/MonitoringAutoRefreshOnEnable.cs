using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Minimal helper that triggers the monitoring cache to refresh when this GameObject becomes enabled.
    /// 
    /// Add this component to any prefab/root that may start disabled (or be enabled later),
    /// so monitored members are picked up without requiring a scene reload.
    /// </summary>
    [DefaultExecutionOrder(-10_000)]
    public sealed class MonitoringAutoRefreshOnEnable : MonoBehaviour
    {
        private void OnEnable()
        {
            // Incremental refresh; avoids rebuilding everything and is duplicate-safe.
            MonitoringHost.RefreshTargets(clear: false);
        }
    }
}

