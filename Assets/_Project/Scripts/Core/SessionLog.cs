using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CyberQuest.Core
{
    /// <summary>
    /// Timestamped decision and room log for the research write-up. Never scored.
    /// Written as JSON to persistentDataPath/sessions at the end of a session.
    /// </summary>
    public static class SessionLog
    {
        [Serializable]
        public class Entry
        {
            public float realSeconds;
            public string gameTime;
            public string kind;
            public string detail;
        }

        [Serializable]
        public class RoomDwell
        {
            public string room;
            public int entries;
            public float seconds;
        }

        [Serializable]
        private class Document
        {
            public string sessionId;
            public string startedUtc;
            public List<Entry> entries = new List<Entry>();
            public List<RoomDwell> rooms = new List<RoomDwell>();
            public string ending;
            public int score;
            public List<string> completionOrder = new List<string>();
            public string finalState;
        }

        private static Document _doc = NewDoc();

        /// <summary>Supplies (real seconds, game clock text). Set by GameDirector.</summary>
        public static Func<(float, string)> Clock = () => (Time.realtimeSinceStartup, "");

        public static IReadOnlyList<Entry> Entries => _doc.entries;

        public static string SessionId => _doc.sessionId;

        public static string StartedUtc => _doc.startedUtc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            _doc = NewDoc();
            PhysicalSecurityTelemetryLogger.ResetSession();
        }

        private static Document NewDoc() => new Document
        {
            sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            startedUtc = DateTime.UtcNow.ToString("o"),
        };

        public static void Decision(string detail) => Add("decision", detail);

        public static void Event(string detail) => Add("event", detail);

        public static void Add(string kind, string detail)
        {
            var (real, game) = Clock();
            _doc.entries.Add(new Entry { realSeconds = real, gameTime = game, kind = kind, detail = detail });
        }

        public static void AddRoomTime(string room, float seconds, bool newEntry)
        {
            var dwell = _doc.rooms.Find(r => r.room == room);
            if (dwell == null)
            {
                dwell = new RoomDwell { room = room };
                _doc.rooms.Add(dwell);
            }
            dwell.seconds += seconds;
            if (newEntry)
            {
                dwell.entries++;
            }
        }

        public static IReadOnlyList<RoomDwell> Rooms => _doc.rooms;

        public static string ToJson(GameState state, EndingType ending, int score)
        {
            _doc.ending = ending.ToString();
            _doc.score = score;
            _doc.completionOrder = state.completionOrder.ConvertAll(s => s.ToString());
            _doc.finalState = JsonUtility.ToJson(state);
            return JsonUtility.ToJson(_doc, true);
        }

        /// <summary>Writes the session JSON and returns its path (null if writing failed).</summary>
        public static string Write(GameState state, EndingType ending, int score)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "sessions");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"session_{_doc.sessionId}.json");
                File.WriteAllText(path, ToJson(state, ending, score));
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SessionLog] Could not write session log: " + e.Message);
                return null;
            }
        }
    }
}
