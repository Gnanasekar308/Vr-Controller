using System.Collections.Generic;
using UnityEngine;

namespace MediSimVR
{
    // Demo mode OFF with the Medisim PC server: links the classroom (meetings, students) to the real student stations.
    // There is no student sign-in on the stations yet, so every station takes a free "seat" of the sample class:
    // seat k = DemoConfig.Quests[k] and its student. The sample meetings then list the real stations instead of
    // q1, q2 ..., and a new meeting can be created with them. A station keeps its seat on this phone (PlayerPrefs).
    // To replace later: the station reports the student who signed in on it (name + ID), see HANDOVER 6.4.
    public static class ClassroomStations
    {
        private const string SeatKey = "msvr_seat_";

        private static readonly Dictionary<string, string> studentOfStation = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> stationOfStudent = new Dictionary<string, string>();
        private static readonly HashSet<int> usedSeats = new HashSet<int>();

        public static int Count { get { return studentOfStation.Count; } }

        // Classroom.Reset (app start / demo switch)
        public static void Reset()
        {
            studentOfStation.Clear();
            stationOfStudent.Clear();
            usedSeats.Clear();
        }

        // "" = not a bound station
        public static string StudentOf(string stationId)
        {
            string s;
            return stationId != null && studentOfStation.TryGetValue(stationId, out s) ? s : "";
        }

        public static string StationOf(string studentId)
        {
            string s;
            return studentId != null && stationOfStudent.TryGetValue(studentId, out s) ? s : "";
        }

        // A station appeared on the server. false = already bound, or every seat of the sample class is taken.
        public static bool Bind(string stationId, string stationName)
        {
            if (string.IsNullOrEmpty(stationId) || studentOfStation.ContainsKey(stationId)) return false;
            var seats = DemoConfig.Quests;
            int seat = PlayerPrefs.GetInt(SeatKey + stationId, -1);
            if (seat < 0 || seat >= seats.Length || usedSeats.Contains(seat))
            {
                seat = -1;
                for (int i = 0; i < seats.Length; i++) if (!usedSeats.Contains(i)) { seat = i; break; }
            }
            if (seat < 0)
            {
                Debug.LogWarning("[MediSimVR] Station " + stationName + " has no seat: the sample class has only " + seats.Length + " students");
                return false;
            }

            usedSeats.Add(seat);
            PlayerPrefs.SetInt(SeatKey + stationId, seat);
            string studentId = seats[seat].StudentId ?? "";
            studentOfStation[stationId] = studentId;
            if (studentId.Length > 0) stationOfStudent[studentId] = stationId;

            // the meetings that had this seat's sample Quest now have the real station
            foreach (var m in Classroom.Meetings)
            {
                int i = m.QuestIds.IndexOf(seats[seat].Id);
                if (i >= 0) m.QuestIds[i] = stationId;
            }
            Debug.Log("[MediSimVR] Station " + stationName + " = seat " + (seat + 1) + " (" + Classroom.StudentLabel(studentId) + ")");
            Classroom.MetricsChanged();   // the screens refresh
            return true;
        }
    }
}
