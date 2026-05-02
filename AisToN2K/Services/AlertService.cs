namespace AisToN2K.Services
{
    /// <summary>
    /// Tracks per-vessel alert firing state for the two-phase cooldown.
    /// Phase 1: Fire immediately on first sighting.
    /// Phase 2: Fire once more after repeatDelay (2 min default).
    /// Then silent until vessel goes unheard for absenceThreshold (5 min default), which resets the cycle.
    /// </summary>
    internal class AlertCooldownState
    {
        public DateTime LastFired { get; set; }
        public DateTime LastHeard { get; set; }
        public int FireCount { get; set; }
    }

    /// <summary>
    /// Manages vessel alerts: add/remove/clear, persistence, and fire-with-cooldown.
    /// Runtime matching is MMSI-based. Name is stored for display only.
    /// </summary>
    public class AlertService
    {
        private readonly object _lock = new();
        private readonly AlertStore _store;
        private readonly List<AlertEntry> _alerts;
        private readonly Dictionary<int, AlertCooldownState> _cooldownState = new();
        private readonly TimeSpan _repeatDelay;
        private readonly TimeSpan _absenceThreshold;

        /// <summary>
        /// Fired when an alert triggers (on UI thread marshaling is caller's responsibility).
        /// Args: (mmsi, vesselName)
        /// </summary>
        public event EventHandler<(int Mmsi, string? Name)>? AlertTriggered;

        public AlertService(AlertStore? store = null, TimeSpan? repeatDelay = null, TimeSpan? absenceThreshold = null)
        {
            _store = store ?? AlertStore.CreateInMemory();
            _alerts = _store.Load();
            _repeatDelay = repeatDelay ?? TimeSpan.FromMinutes(2);
            _absenceThreshold = absenceThreshold ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Add an alert for a vessel by MMSI. Name is optional (for display).
        /// Returns false if already alerted.
        /// </summary>
        public bool AddAlert(int mmsi, string? name = null)
        {
            lock (_lock)
            {
                if (_alerts.Any(a => a.Mmsi == mmsi))
                    return false;

                _alerts.Add(new AlertEntry
                {
                    Mmsi = mmsi,
                    Name = name,
                    CreatedAt = DateTime.UtcNow
                });
            }
            _store.Save(GetAlertsSnapshot());
            return true;
        }

        /// <summary>
        /// Remove an alert by MMSI. Returns false if not found.
        /// </summary>
        public bool RemoveAlert(int mmsi)
        {
            bool removed;
            lock (_lock)
            {
                removed = _alerts.RemoveAll(a => a.Mmsi == mmsi) > 0;
                if (removed)
                    _cooldownState.Remove(mmsi);
            }
            if (removed)
                _store.Save(GetAlertsSnapshot());
            return removed;
        }

        /// <summary>
        /// Clear all alerts.
        /// </summary>
        public void ClearAlerts()
        {
            lock (_lock)
            {
                _alerts.Clear();
                _cooldownState.Clear();
            }
            _store.Save(GetAlertsSnapshot());
        }

        /// <summary>
        /// Get all active alerts.
        /// </summary>
        public List<AlertEntry> GetAlerts()
        {
            lock (_lock)
            {
                return _alerts.Select(a => new AlertEntry
                {
                    Mmsi = a.Mmsi,
                    Name = a.Name,
                    CreatedAt = a.CreatedAt
                }).ToList();
            }
        }

        /// <summary>
        /// Check if a vessel has an active alert (fast lookup for log highlighting).
        /// </summary>
        public bool IsAlerted(int mmsi)
        {
            lock (_lock)
            {
                return _alerts.Any(a => a.Mmsi == mmsi);
            }
        }

        /// <summary>
        /// Check if a vessel should trigger an alert now (two-phase cooldown).
        /// Phase 1: Fires immediately on first sighting.
        /// Phase 2: Fires once more after repeatDelay (2 min).
        /// Then silent until vessel is absent for absenceThreshold (5 min) and reappears.
        /// Also updates lastHeard tracking for absence detection.
        /// </summary>
        public bool CheckAndFire(int mmsi, string? vesselName = null)
        {
            bool shouldFire = false;
            lock (_lock)
            {
                if (!_alerts.Any(a => a.Mmsi == mmsi))
                    return false;

                var now = DateTime.UtcNow;

                if (!_cooldownState.TryGetValue(mmsi, out var state))
                {
                    // First ever sighting — fire immediately
                    _cooldownState[mmsi] = new AlertCooldownState
                    {
                        LastFired = now,
                        LastHeard = now,
                        FireCount = 1
                    };
                    shouldFire = true;
                }
                else
                {
                    // Check if vessel was absent long enough to reset the cycle
                    if (now - state.LastHeard >= _absenceThreshold)
                    {
                        state.LastFired = now;
                        state.LastHeard = now;
                        state.FireCount = 1;
                        shouldFire = true;
                    }
                    else
                    {
                        // Update last heard
                        state.LastHeard = now;

                        // Check if the 2-minute repeat is due
                        if (state.FireCount == 1 && now - state.LastFired >= _repeatDelay)
                        {
                            state.LastFired = now;
                            state.FireCount = 2;
                            shouldFire = true;
                        }
                        // FireCount >= 2: no more fires until absence resets the cycle
                    }
                }
            }

            if (shouldFire)
            {
                AlertTriggered?.Invoke(this, (mmsi, vesselName));
            }

            return shouldFire;
        }

        /// <summary>
        /// Update the display name for an existing alert (when we learn a vessel's name).
        /// </summary>
        public void UpdateAlertName(int mmsi, string name)
        {
            bool updated = false;
            lock (_lock)
            {
                var alert = _alerts.FirstOrDefault(a => a.Mmsi == mmsi);
                if (alert != null && alert.Name != name)
                {
                    alert.Name = name;
                    updated = true;
                }
            }
            if (updated)
                _store.Save(GetAlertsSnapshot());
        }

        public int AlertCount
        {
            get { lock (_lock) { return _alerts.Count; } }
        }

        private List<AlertEntry> GetAlertsSnapshot()
        {
            lock (_lock)
            {
                return _alerts.Select(a => new AlertEntry
                {
                    Mmsi = a.Mmsi,
                    Name = a.Name,
                    CreatedAt = a.CreatedAt
                }).ToList();
            }
        }
    }
}
