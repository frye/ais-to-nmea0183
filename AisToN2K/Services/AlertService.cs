namespace AisToN2K.Services
{
    /// <summary>
    /// Manages vessel alerts: add/remove/clear, persistence, and fire-with-cooldown.
    /// Runtime matching is MMSI-based. Name is stored for display only.
    /// </summary>
    public class AlertService
    {
        private readonly object _lock = new();
        private readonly AlertStore _store;
        private readonly List<AlertEntry> _alerts;
        private readonly Dictionary<int, DateTime> _lastFired = new();
        private readonly TimeSpan _cooldown;

        /// <summary>
        /// Fired when an alert triggers (on UI thread marshaling is caller's responsibility).
        /// Args: (mmsi, vesselName)
        /// </summary>
        public event EventHandler<(int Mmsi, string? Name)>? AlertTriggered;

        public AlertService(AlertStore? store = null, TimeSpan? cooldown = null)
        {
            _store = store ?? AlertStore.CreateInMemory();
            _alerts = _store.Load();
            _cooldown = cooldown ?? TimeSpan.FromMinutes(5);
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
                    _lastFired.Remove(mmsi);
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
                _lastFired.Clear();
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
        /// Check if a vessel should trigger an alert now (respects cooldown).
        /// If it fires, invokes AlertTriggered event and returns true.
        /// </summary>
        public bool CheckAndFire(int mmsi, string? vesselName = null)
        {
            bool shouldFire = false;
            lock (_lock)
            {
                if (!_alerts.Any(a => a.Mmsi == mmsi))
                    return false;

                if (_lastFired.TryGetValue(mmsi, out var lastTime))
                {
                    if (DateTime.UtcNow - lastTime < _cooldown)
                        return false;
                }

                _lastFired[mmsi] = DateTime.UtcNow;
                shouldFire = true;
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
