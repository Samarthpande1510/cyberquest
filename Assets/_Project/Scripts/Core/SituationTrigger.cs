using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Base class for every situation (CLAUDE.md 4.2). Each one decides
    /// independently when it is ready: an earliest time, a fallback time, a
    /// "requires any N situations completed" count, an optional spatial trigger
    /// and an optional situation-specific condition. Ready means
    /// (time >= earliest) and (fallback reached, or every configured primary
    /// condition holds). Scheduled situations also need the GameDirector to grant
    /// the floor; physical ones (USB, workstation) never block it.
    /// </summary>
    public abstract class SituationTrigger : MonoBehaviour
    {
        [SerializeField] protected SituationId id;
        [SerializeField] protected float earliestTime;
        [Tooltip("Real seconds after shift start; < 0 disables the fallback.")]
        [SerializeField] protected float fallbackTime = -1f;
        [SerializeField] protected int requiresCompleted;
        [SerializeField] protected Transform spatialTarget;
        [SerializeField] protected float spatialRadius = 2f;
        [SerializeField] protected bool directorScheduled = true;
        [SerializeField] protected bool ignoresCooldown;
        [Tooltip("Hand the director back after this many seconds without a decision (situation stays open). <= 0 = never.")]
        [SerializeField] protected float releaseAfter = 120f;

        public SituationId Id => id;
        public bool IgnoresCooldown => ignoresCooldown;
        public bool DirectorScheduled => directorScheduled;
        public bool Triggered { get; private set; }
        public bool Resolved { get; private set; }
        public float TriggeredAt { get; private set; }

        protected GameState State => GameState.Current;
        protected GameDirector Director => GameDirector.Instance;

        /// <summary>Situation-specific primary condition (e.g. "QR was scanned").</summary>
        protected virtual bool HasCustomCondition => false;
        protected virtual bool CustomCondition() => true;

        protected abstract void OnTriggered();
        protected virtual void OnActiveUpdate() { }

        public void Configure(SituationId situation, float earliest, float fallback, int completed, Transform spatial,
            float radius, bool scheduled, bool skipCooldown, float release)
        {
            id = situation;
            earliestTime = earliest;
            fallbackTime = fallback;
            requiresCompleted = completed;
            spatialTarget = spatial;
            spatialRadius = radius;
            directorScheduled = scheduled;
            ignoresCooldown = skipCooldown;
            releaseAfter = release;
        }

        protected virtual void Update()
        {
            var director = Director;
            if (director == null || !director.Running)
            {
                return;
            }

            if (!Triggered)
            {
                if (!director.LockedOut && ConditionsMet(director) && (!directorScheduled || director.TryBegin(this)))
                {
                    BeginNow(director);
                }
                return;
            }

            if (Resolved)
            {
                return;
            }
            if (directorScheduled && releaseAfter > 0f && director.Active == this && director.Elapsed - TriggeredAt > releaseAfter)
            {
                SessionLog.Event($"{id}: no decision after {releaseAfter:0}s - director released");
                director.Release(this);
            }
            OnActiveUpdate();
        }

        public bool ConditionsMet(GameDirector director)
        {
            float t = director.Elapsed;
            if (t < earliestTime)
            {
                return false;
            }
            if (fallbackTime >= 0f && t >= fallbackTime)
            {
                return true;
            }

            bool any = false;
            bool primary = true;
            if (requiresCompleted > 0)
            {
                any = true;
                primary &= State.CompletedCount >= requiresCompleted;
            }
            if (spatialTarget != null)
            {
                any = true;
                primary &= PlayerWithin(spatialTarget.position, spatialRadius);
            }
            if (HasCustomCondition)
            {
                any = true;
                primary &= CustomCondition();
            }
            return any ? primary : fallbackTime < 0f;
        }

        /// <summary>Debug / test entry point: starts the situation immediately.</summary>
        public void ForceTrigger()
        {
            if (Triggered)
            {
                return;
            }
            var director = Director;
            if (directorScheduled && director != null)
            {
                director.ForceBegin(this);
            }
            BeginNow(director);
        }

        private void BeginNow(GameDirector director)
        {
            Triggered = true;
            TriggeredAt = director != null ? director.Elapsed : 0f;
            SessionLog.Event($"{id} triggered");
            OnTriggered();
        }

        /// <summary>Records the decision, marks the situation complete and frees the director.</summary>
        protected void Complete(string decision)
        {
            if (Resolved)
            {
                return;
            }
            Resolved = true;
            State.MarkCompleted(id);
            SessionLog.Decision($"{id}: {decision}");
            Director?.Release(this);
        }

        /// <summary>Ends the situation without an outcome (e.g. nobody answered the intercom).</summary>
        protected void Abandon(string reason)
        {
            if (Resolved)
            {
                return;
            }
            Resolved = true;
            SessionLog.Event($"{id}: {reason}");
            Director?.Release(this);
        }

        public static bool PlayerWithin(Vector3 point, float radius)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return false;
            }
            Vector3 d = cam.transform.position - point;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }
    }
}
